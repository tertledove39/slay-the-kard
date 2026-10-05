using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 起手换牌界面：把起手牌平铺在屏幕前，玩家点选要换掉的牌，确认后这些牌洗回牌库
/// 并补抽等量张，新牌在原位展示后再随全部手牌一起归位。
/// 规则参考炉石/Kards：可换任意张（一张不换或全换都行），只有一次换牌机会。
/// </summary>
public partial class MulliganScreen : Control
{
    private const string ScenePath = "res://bin/mulligan_screen.tscn";
    /// <summary>选中标记用的占位图。assest 下没有叉号素材，先用 dead.png 顶替。</summary>
    private const string MarkerTexturePath = "res://assest/dead.png";
    private const int OverlayLayer = int.MaxValue;

    // 卡牌沿用 cardbase.tscn 的设计尺寸 180x240，靠 Scale 放大，
    // 避免改动 Size 后卡内布局被拉伸。
    private const float CardW = 180f;
    private const float CardH = 240f;
    private const float CardScale = 1.2f;
    private const float GapX = 28f;
    private const float RowCenterY = 400f;
    private const float MarkerSize = 96f;
    private const float FlyInDuration = 0.22f;
    private const float FlyInStaggerSeconds = 0.03f;
    /// <summary>补抽完成后新牌停留的时间，让玩家看清换到了什么</summary>
    private const float RevealHoldSeconds = 0.9f;

    private readonly List<cardBase_> _cards = new();
    private readonly HashSet<cardBase_> _selected = new();
    private readonly Dictionary<cardBase_, TextureRect> _markers = new();
    /// <summary>
    /// 本界面加到卡牌上的子节点（点击层与标记）。必须显式记录后逐个释放，
    /// 不能按类型遍历卡牌子节点删除——卡牌自身的美术资源也是 TextureRect。
    /// </summary>
    private readonly List<Node> _overlays = new();

    private battlefield_ _field;
    private Player _player;
    private Control _cardLayer;
    private Button _confirmButton;
    private Label _countLabel;
    private TaskCompletionSource<bool> _confirmRequested;

    /// <summary>
    /// 显示换牌界面并走完整个换牌流程。调用方负责在调用前后锁定/解锁战场操作。
    /// </summary>
    public static async Task ShowAsync(battlefield_ field, Player player)
    {
        if (field == null || player == null) return;
        if (field.GetTree()?.Root == null) return;
        if (player.GetCardsInHand().Count == 0) return;

        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        if (scene?.Instantiate() is not MulliganScreen screen) return;

        var layer = new CanvasLayer { Layer = OverlayLayer };
        field.GetTree().Root.AddChild(layer);
        layer.AddChild(screen);
        await screen.Run(field, player);
        layer.QueueFree();
    }

    private async Task Run(battlefield_ field, Player player)
    {
        _field = field;
        _player = player;
        _cardLayer = GetNode<Control>("CardLayer");
        _confirmButton = GetNode<Button>("ConfirmButton");
        _countLabel = GetNode<Label>("CountLabel");

        _cards.AddRange(player.GetCardsInHand());
        if (_cards.Count == 0) return;

        AdoptCards();
        await DealIn();

        _confirmRequested = new TaskCompletionSource<bool>();
        _confirmButton.Pressed += OnConfirmPressed;
        UpdateCountLabel();
        await _confirmRequested.Task;

        _confirmButton.Disabled = true;
        try
        {
            await ApplyMulligan();
        }
        finally
        {
            // 必须先把手牌还给战场：覆盖层随后会被释放，挂在下面的卡牌会一起被销毁
            ReleaseCards();
            player.RefreshMyHand();
        }
    }

    /// <summary>
    /// 把真实手牌移进本界面接管显示。与 ShowCardChoice 相同的 reparent 做法：
    /// 卡牌 `_Ready` 已跑过，reparent 不会重置其状态。
    /// </summary>
    private void AdoptCards()
    {
        foreach (var card in _cards)
            if (card != null) PrepareCard(card);
    }

    /// <summary>统一设置卡牌在本界面中的显示状态，并挂上点击层与选中标记</summary>
    private void PrepareCard(cardBase_ card)
    {
        card.Reparent(_cardLayer);
        card.Size = new Vector2(CardW, CardH);
        card.PivotOffset = new Vector2(CardW / 2f, CardH / 2f);
        card.ResetVisualsInstant();
        card.Rotation = 0f;
        card.Scale = new Vector2(CardScale, CardScale);
        card.ZIndex = 10;
        card.Visible = true;
        // 卡牌自身不接收鼠标，点击交给覆盖其上的透明层
        card.MouseFilter = Control.MouseFilterEnum.Ignore;

        AttachClickLayer(card);
        AttachMarker(card);
    }

    private void AttachClickLayer(cardBase_ card)
    {
        var catcher = new ColorRect { Color = new Color(0, 0, 0, 0) };
        catcher.SetAnchorsPreset(LayoutPreset.FullRect);
        catcher.ZIndex = 50;
        catcher.MouseFilter = MouseFilterEnum.Ignore;
        catcher.GuiInput += e =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                Toggle(card);
        };
        card.AddChild(catcher);
        _overlays.Add(catcher);
        // 延迟到本帧之后再接收输入，避免打开界面的那一次点击被立刻吃掉
        Callable.From(() => catcher.MouseFilter = MouseFilterEnum.Stop).CallDeferred();
    }

    private void AttachMarker(cardBase_ card)
    {
        var marker = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(MarkerTexturePath),
            Visible = false,
            Size = new Vector2(MarkerSize, MarkerSize),
            Position = new Vector2((CardW - MarkerSize) / 2f, (CardH - MarkerSize) / 2f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 51,
        };
        card.AddChild(marker);
        _markers[card] = marker;
        _overlays.Add(marker);
    }

    /// <summary>从屏幕左侧外依次飞入到平铺位置</summary>
    private async Task DealIn()
    {
        foreach (var card in _cards)
            if (card != null) card.Position = OffScreenLeft();

        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            if (card == null) continue;
            await card.MoveToPosition(SlotPosition(i), FlyInDuration);
            await ToSignal(GetTree().CreateTimer(FlyInStaggerSeconds), SceneTreeTimer.SignalName.Timeout);
        }
    }

    /// <summary>
    /// 第 index 张牌的落位。卡牌以自身中心为轴缩放，所以要把「视觉左上角」
    /// 换算回未缩放时的 position，否则整排会向左上偏。
    /// </summary>
    private Vector2 SlotPosition(int index)
    {
        float visualW = CardW * CardScale;
        float visualH = CardH * CardScale;
        float totalW = _cards.Count * visualW + (_cards.Count - 1) * GapX;
        float visualLeft = (GetViewportRect().Size.X - totalW) / 2f + index * (visualW + GapX);
        float visualTop = RowCenterY - visualH / 2f;
        return new Vector2(
            visualLeft + (CardScale - 1f) * (CardW / 2f),
            visualTop + (CardScale - 1f) * (CardH / 2f));
    }

    private Vector2 OffScreenLeft()
    {
        return new Vector2(-CardW * CardScale - 120f, SlotPosition(0).Y);
    }

    private void Toggle(cardBase_ card)
    {
        if (card == null) return;
        if (!_selected.Remove(card)) _selected.Add(card);
        if (_markers.TryGetValue(card, out var marker) && marker != null && IsInstanceValid(marker))
            marker.Visible = _selected.Contains(card);
        UpdateCountLabel();
    }

    private void UpdateCountLabel()
    {
        _countLabel.Text = _selected.Count == 0
            ? "已选 0 张，直接开局"
            : $"已选 {_selected.Count} 张，确认后洗回牌库并补抽";
    }

    private void OnConfirmPressed()
    {
        _confirmRequested?.TrySetResult(true);
    }

    /// <summary>换牌：选中的牌洗回牌库并补抽等量张，新牌补进原来的位置</summary>
    private async Task ApplyMulligan()
    {
        var returned = new List<cardBase_>();
        foreach (var card in _cards)
            if (card != null && _selected.Contains(card)) returned.Add(card);

        if (returned.Count == 0) return; // 一张不换

        var slots = new List<int>();
        foreach (var card in returned)
        {
            slots.Add(_cards.IndexOf(card));
            // 用「停到屏幕外的牌库位置」隐藏，不能设 Visible=false：
            // 那样没有任何路径会把它恢复，日后抽到手上就是一个空位。
            card.SetPosition(Player.DeckParkPosition);
        }
        ClearMarkers();

        var drawn = await _player.MulliganAsync(returned);

        // 换掉的牌已进牌库，先还回战场，否则会随覆盖层一起被释放
        foreach (var card in returned)
        {
            if (card == null || !IsInstanceValid(card)) continue;
            card.Scale = Vector2.One;
            card.Rotation = 0f;
            card.SetPosition(Player.DeckParkPosition);
            if (card.GetParent() != _field) card.Reparent(_field);
        }

        for (int i = 0; i < drawn.Count && i < slots.Count; i++)
        {
            var card = drawn[i];
            if (card == null) continue;
            PrepareCard(card);
            card.Position = OffScreenLeft();
            _cards[slots[i]] = card;
            await card.MoveToPosition(SlotPosition(slots[i]), FlyInDuration);
        }

        if (drawn.Count > 0)
            await ToSignal(GetTree().CreateTimer(RevealHoldSeconds), SceneTreeTimer.SignalName.Timeout);
    }

    /// <summary>只隐藏标记并清空选择，节点本身留给 ReleaseCards 统一释放，避免重复释放</summary>
    private void ClearMarkers()
    {
        foreach (var marker in _markers.Values)
            if (marker != null && IsInstanceValid(marker)) marker.Visible = false;
        _markers.Clear();
        _selected.Clear();
    }

    /// <summary>把仍在屏幕上的手牌还给战场，并摘掉本界面挂上去的子节点</summary>
    private void ReleaseCards()
    {
        foreach (var card in _cards)
        {
            if (card == null || !IsInstanceValid(card)) continue;
            card.Scale = Vector2.One;
            card.Rotation = 0f;
            if (card.GetParent() != _field) card.Reparent(_field);
        }

        foreach (var node in _overlays)
            if (node != null && IsInstanceValid(node)) node.QueueFree();
        _overlays.Clear();
        _markers.Clear();
    }
}
