using Godot;
using System.Collections.Generic;

/// <summary>
/// 事件选项的「会加入哪些卡」悬浮预览：**只画卡，没有底板、没有边框、没有文字**。
///
/// 场景 <c>bin/event_card_preview.tscn</c> 只有一个 Control 根节点，卡直接挂上去——
/// 连中间容器都不要，少一层就少一处出错的地方（曾被 Container 类强行接管子节点尺寸
/// 而把卡撑满屏幕，见 docs/LOGIC.md「弹窗里排卡的标准写法」）。
///
/// 建卡与排版一律照抄 <c>ChooseSomeCard.BuildGrid()</c>：
///   钉锚点 → 原生尺寸 → 缩放 → 入树 → SetCardInformation → 最后 Position。
///
/// 同名卡（如 [snowstorm] 的 20 张「埋伏」）**去重后只画一张**——所以 20 张也只是一张卡。
/// </summary>
public partial class EventCardPreview : Control
{
    /// <summary>预览卡的缩放，与 ChooseSomeCard 的网格同值。</summary>
    private const float CardScale = 0.9f;

    /// <summary>卡与卡之间的横向间距（像素，按缩放后的宽度算）。</summary>
    private const float GapX = 20f;

    /// <summary>
    /// 最多画两张。实测 event.ini 里一个选项最多只有 2 种不同的卡
    /// （最多的那条是「拖拉机厂 + 预备役 ×2」），故场景与代码都按 2 张留位。
    /// </summary>
    private const int MaxCards = 2;

    private const string CardScenePath = "res://bin/cardbase.tscn";

    private PackedScene _cardScene;

    public override void _Ready()
    {
        _cardScene = ResourceLoader.Load<PackedScene>(CardScenePath);
        Visible = false;
    }

    /// <summary>在 <paramref name="anchor"/> 上方显示这批卡。列表为空时直接隐藏。</summary>
    public void Show(List<(CardData card, int count)> cards, Control anchor)
    {
        if (cards == null || cards.Count == 0) { Hide(); return; }

        ClearCards();

        int shown = Mathf.Min(cards.Count, MaxCards);
        if (cards.Count > MaxCards)
        {
            // 界面按 2 张留位且不显示文字，多出来的画不下。真出现就记一笔，别静默吞掉。
            GD.Print($"[EventCardPreview] 选项涉及 {cards.Count} 种不同的卡，预览只画前 {MaxCards} 种");
        }

        float cardW = cardBase_.DesignSize.X * CardScale;
        float totalW = shown * cardW + (shown - 1) * GapX;
        float x = (Size.X - totalW) / 2f;

        for (int i = 0; i < shown; i++)
        {
            AddCardFace(cards[i].card, x);
            x += cardW + GapX;
        }

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
        AddChild(card);
        card.SetCardInformation(data);
        card.SetIsFriend(IsFriend.friend);
        card.Position = new Vector2(x, 0);

        // 预览是只读的：不吃鼠标，否则会把按钮的 hover 抢掉，预览自己关不掉
        card.MouseFilter = MouseFilterEnum.Ignore;
        card.ZIndex = 10;
    }

    /// <summary>把预览摆在按钮正上方；贴边时夹回屏幕内。</summary>
    private Vector2 ResolvePosition(Control anchor)
    {
        Vector2 size = Size;
        Vector2 view = GetViewportRect().Size;

        Vector2 pos = anchor.GlobalPosition + new Vector2(0, -size.Y - 8);
        pos.X = Mathf.Clamp(pos.X, 8, Mathf.Max(8, view.X - size.X - 8));
        pos.Y = Mathf.Clamp(pos.Y, 8, Mathf.Max(8, view.Y - size.Y - 8));
        return pos;
    }

    private void ClearCards()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
    }
}
