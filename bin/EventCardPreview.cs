using Godot;
using System.Collections.Generic;

/// <summary>
/// 事件选项的「会加入哪些卡」悬浮预览面板。
///
/// 布局与配色在 <c>bin/event_card_preview.tscn</c> 里（需求：UI 不写在代码里），
/// 本类只负责：清空上一批卡、按去重后的列表摆出真卡面、填数量说明、挪到按钮旁边。
///
/// **建卡与排版一律照抄 <c>ChooseSomeCard.BuildGrid()</c>**（弹窗里排卡的标准写法），
/// 不自己发明：
///   - 容器是**普通 Control**，不是 HBoxContainer —— 容器类会强行接管子节点的
///     Position 与 Size，卡会被撑到容器大小（这里踩过一次，整张卡糊满屏幕）；
///   - 顺序固定为「钉锚点 → 原生尺寸 → 缩放 → 入树 → SetCardInformation → Position」，
///     顺序颠倒会让字体测量拿到错误的尺寸；
///   - 缩放而非改 Size：卡面内部各控件的坐标是按 180x240 摆的，改 Size 只会让布局错位。
///
/// 同名卡（如 [snowstorm] 的 20 张「埋伏」）**去重后只画一张**，数量写进 Caption。
/// 实测 event.ini 里一个选项最多只有 2 种不同的卡，面板按此留位。
/// </summary>
public partial class EventCardPreview : Control
{
    /// <summary>预览卡的缩放。与 ChooseSomeCard 的网格同值，保持观感一致。</summary>
    private const float CardScale = 0.9f;

    /// <summary>卡与卡之间的横向间距（像素，按缩放后的宽度算）。</summary>
    private const float GapX = 20f;

    private const string CardScenePath = "res://bin/cardbase.tscn";

    private Panel _background;
    private Control _cards;
    private Label _caption;
    private PackedScene _cardScene;

    public override void _Ready()
    {
        _background = GetNodeOrNull<Panel>("Background");
        _cards = GetNodeOrNull<Control>("Background/Cards");
        _caption = GetNodeOrNull<Label>("Background/Caption");
        _cardScene = ResourceLoader.Load<PackedScene>(CardScenePath);

        Visible = false;
    }

    /// <summary>在 <paramref name="anchor"/> 上方显示这批卡。列表为空时直接隐藏。</summary>
    public void Show(List<(CardData card, int count)> cards, Control anchor)
    {
        if (cards == null || cards.Count == 0) { Hide(); return; }
        if (_cards == null) return;

        ClearCards();

        float cardW = cardBase_.DesignSize.X * CardScale;
        float totalW = cards.Count * cardW + (cards.Count - 1) * GapX;
        float x = (_cards.Size.X - totalW) / 2f;

        var names = new List<string>();
        foreach (var (data, count) in cards)
        {
            AddCardFace(data, x);
            x += cardW + GapX;

            names.Add(count > 1 ? $"{data.Name} ×{count}" : data.Name);
        }

        if (_caption != null) _caption.Text = "将加入卡组：" + string.Join("、", names);

        Position = ResolvePosition(anchor);
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
        ClearCards();
    }

    /// <summary>
    /// 建一张只读的卡面。**顺序照抄 ChooseSomeCard.BuildGrid()**：
    /// 钉锚点 → 原生尺寸 → 缩放 → 入树 → SetCardInformation → 最后 Position。
    ///
    /// 唯一的差别是不设 PivotOffset：那边设它是为了让 hover 动画绕中心缩放，
    /// 本面板不缩放动画，留着反而会让卡面视觉位置偏移半个缩放差。
    /// </summary>
    private void AddCardFace(CardData data, float x)
    {
        var card = _cardScene?.Instantiate() as cardBase_;
        if (card == null) return;

        card.SetAnchorsPreset(LayoutPreset.TopLeft);
        card.Size = cardBase_.DesignSize;
        card.Scale = new Vector2(CardScale, CardScale);
        _cards.AddChild(card);
        card.SetCardInformation(data);
        card.SetIsFriend(IsFriend.friend);
        card.Position = new Vector2(x, 0);

        // 预览是只读的：不吃鼠标，否则会把按钮的 hover 抢掉，预览自己关不掉
        card.MouseFilter = MouseFilterEnum.Ignore;
        card.ZIndex = 10;
    }

    /// <summary>把面板摆在按钮正上方；贴边时夹回屏幕内。</summary>
    private Vector2 ResolvePosition(Control anchor)
    {
        Vector2 size = _background?.Size ?? new Vector2(392, 284);
        Vector2 view = GetViewportRect().Size;

        Vector2 pos = anchor.GlobalPosition + new Vector2(0, -size.Y - 8);
        pos.X = Mathf.Clamp(pos.X, 8, Mathf.Max(8, view.X - size.X - 8));
        pos.Y = Mathf.Clamp(pos.Y, 8, Mathf.Max(8, view.Y - size.Y - 8));
        return pos;
    }

    private void ClearCards()
    {
        if (_cards == null) return;
        foreach (Node child in _cards.GetChildren())
        {
            _cards.RemoveChild(child);
            child.QueueFree();
        }
    }
}
