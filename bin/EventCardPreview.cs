using Godot;
using System.Collections.Generic;

/// <summary>
/// 事件选项的「会加入哪些卡」悬浮预览面板。
///
/// 布局与配色都在 <c>bin/event_card_preview.tscn</c> 里（需求：UI 不写在代码里），
/// 本类只负责：清空上一批卡、按去重后的列表实例化真卡面、填数量说明、摆到按钮旁边。
///
/// 同名卡（如 [snowstorm] 的 20 张「埋伏」）**去重后只画一张**，数量写进 Caption，
/// 否则 20 张卡会把屏幕铺满。
/// </summary>
public partial class EventCardPreview : Control
{
    /// <summary>预览面板的相对缩放。cardbase.tscn 的设计尺寸是 180x240，缩到 0.5 ≈ 90x120。</summary>
    private const float CardScale = 0.5f;

    /// <summary>最多画几张不同的卡；超出的只在说明里标数量，避免面板无限长。</summary>
    private const int MaxVisibleCards = 6;

    private const string CardScenePath = "res://bin/cardbase.tscn";

    private Panel _background;
    private HBoxContainer _cards;
    private Label _caption;
    private PackedScene _cardScene;

    public override void _Ready()
    {
        _background = GetNodeOrNull<Panel>("Background");
        _cards = GetNodeOrNull<HBoxContainer>("Background/Cards");
        _caption = GetNodeOrNull<Label>("Background/Caption");
        _cardScene = ResourceLoader.Load<PackedScene>(CardScenePath);

        Visible = false;
    }

    /// <summary>
    /// 在 <paramref name="anchor"/> 上方显示这批卡。列表为空时自动隐藏。
    /// </summary>
    public void Show(List<(CardData card, int count)> cards, Control anchor)
    {
        if (!Visible && cards.Count == 0) return;
        if (cards.Count == 0) { Hide(); return; }

        ClearCards();

        var names = new List<string>();
        for (int i = 0; i < cards.Count; i++)
        {
            names.Add(cards[i].count > 1 ? $"{cards[i].card.Name} ×{cards[i].count}" : cards[i].card.Name);
            if (i < MaxVisibleCards) _cards.AddChild(BuildCardFace(cards[i].card));
        }

        string suffix = cards.Count > MaxVisibleCards ? " …" : "";
        _caption.Text = "将加入卡组：" + string.Join("、", names) + suffix;

        Position = ResolvePosition(anchor);
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
        ClearCards();
    }

    /// <summary>把预览面板摆在按钮正上方；贴边时夹回屏幕内。</summary>
    private Vector2 ResolvePosition(Control anchor)
    {
        Vector2 size = _background?.Size ?? new Vector2(640, 196);
        Vector2 view = GetViewportRect().Size;

        Vector2 pos = anchor.GlobalPosition + new Vector2(0, -size.Y - 8);
        pos.X = Mathf.Clamp(pos.X, 8, Mathf.Max(8, view.X - size.X - 8));
        pos.Y = Mathf.Clamp(pos.Y, 8, Mathf.Max(8, view.Y - size.Y - 8));
        return pos;
    }

    private Control BuildCardFace(CardData data)
    {
        var card = _cardScene?.Instantiate() as cardBase_;
        if (card == null) return null;

        // 与别处建卡一致：先钉死尺寸再做任何布局，避免锚点尺寸随父节点乱跳。
        card.PinDesignSize();
        card.SetCardInformation(data);
        card.SetIsFriend(IsFriend.friend);

        // 预览是只读的：不吃鼠标、不参与战场逻辑。
        card.MouseFilter = MouseFilterEnum.Ignore;
        card.Scale = new Vector2(CardScale, CardScale);
        card.CustomMinimumSize = new Vector2(cardBase_.DesignSize.X * CardScale, cardBase_.DesignSize.Y * CardScale);

        return card;
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
