using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 事件场景界面：在WorldMap或ChooseMission上以CanvasLayer叠加显示。
/// 布局：左侧400x600事件配图、上方金色标题栏、右侧可滚动剧情描述、下方选项按钮。
/// 玩家选择选项后执行对应效果（如replaceCard/replaceRandomCard），完成时标记区域并返回世界地图。
/// </summary>
public partial class EventScene : CanvasLayer
{
    /// <summary>当前展示的事件数据，包含标题、描述、配图路径和选项列表</summary>
    private EventData _event;
    /// <summary>触发此事件的区域名，完成后标记为已完成</summary>
    private string _areaName;

    /// <summary>左侧事件配图宽度（像素）</summary>
    private const float ImageWidth = 400f;
    /// <summary>左侧事件配图高度（像素）</summary>
    private const float ImageHeight = 600f;
    /// <summary>卡组选择界面中卡牌的缩放比例</summary>
    private const float DeckReplaceScale = 0.75f;
    /// <summary>卡牌显示宽度（像素）</summary>
    private const float CardWidth = 180f;
    /// <summary>卡牌显示高度（像素）</summary>
    private const float CardHeight = 240f;
    /// <summary>替换卡牌时最多允许替换的张数</summary>
    private const int MaxSwapCards = 1;

    /// <summary>
    /// 静态入口：在parent节点上创建EventScene叠加层并运行事件流程。
    /// 流程完成后自动QueueFree清理。
    /// </summary>
    /// <param name="parent">父节点（通常是当前场景的根Control）</param>
    /// <param name="eventData">从event.ini加载的事件数据</param>
    /// <param name="areaName">触发此事件的区域名，用于完成后标记</param>
    public static async Task Show(Node parent, EventData eventData, string areaName)
    {
        var scene = new EventScene { Layer = 2 };
        parent.AddChild(scene);
        await scene.Run(eventData, areaName);
        scene.QueueFree();
        if (parent is WorldMap wm)
            wm.DismissChooseMission();
    }

    /// <summary>
    /// 运行事件的主流程：绘制背景遮罩→左侧配图→标题栏→右侧可滚动描述→选项按钮→等待玩家选择→执行效果→标记完成→返回世界地图。
    /// 整个流程异步执行，玩家选择前场景处于等待状态。
    /// </summary>
    private async Task Run(EventData eventData, string areaName)
    {
        _event = eventData;
        _areaName = areaName;

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.75f);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(bg);

        var viewSize = GetViewport().GetVisibleRect().Size;
        float totalW = ImageWidth + 40 + 500; // 图片 + 间距 + 文字区
        float startX = (viewSize.X - totalW) / 2;
        float startY = 60;

        // --- 左侧事件图片 ---
        var imgRect = new TextureRect();
        imgRect.Position = new Vector2(startX, startY);
        imgRect.Size = new Vector2(ImageWidth, ImageHeight);
        imgRect.StretchMode = TextureRect.StretchModeEnum.Scale;
        if (Godot.FileAccess.FileExists(_event.Image))
            imgRect.Texture = ResourceLoader.Load<Texture2D>(_event.Image);
        else
            imgRect.SelfModulate = new Color(0.3f, 0.3f, 0.3f);
        AddChild(imgRect);

        // --- 上方标题栏 ---
        var titleBg = new ColorRect();
        titleBg.Position = new Vector2(startX, startY - 44);
        titleBg.Size = new Vector2(totalW, 40);
        titleBg.Color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        AddChild(titleBg);

        float textX = startX + ImageWidth + 40;
        var titleLabel = new Label();
        titleLabel.Text = _event.Title;
        titleLabel.Position = titleBg.Position;
        titleLabel.Size = titleBg.Size;
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        titleLabel.AddThemeColorOverride("font_color", Colors.Gold);
        AddChild(titleLabel);

        // --- 右侧可滚动文字 ---
        float textAreaH = ImageHeight - 200; // 留出下方选项空间
        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(textX, startY);
        scroll.Size = new Vector2(500, textAreaH);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        AddChild(scroll);

        var descLabel = new RichTextLabel();
        descLabel.BbcodeEnabled = true;
        descLabel.FitContent = true;
        descLabel.CustomMinimumSize = new Vector2(480, 0);
        descLabel.AddThemeColorOverride("default_color", Colors.White);
        // 必须先AddChild再设Text，否则节点不在树中无法正确测量字体大小
        scroll.AddChild(descLabel);
        descLabel.Text = "[font_size=18]" + _event.Description + "[/font_size]";

        // --- 选项按钮区 ---
        float choicesY = startY + textAreaH + 20;
        List<string> chosenEffect = new() { null }; // 闭包捕获用
        var tcs = new TaskCompletionSource<string>();

        for (int i = 0; i < _event.Choices.Count; i++)
        {
            var choice = _event.Choices[i];
            var btn = new Button();
            btn.Text = choice.Text;
            btn.Position = new Vector2(textX, choicesY + i * 50);
            btn.Size = new Vector2(480, 42);
            btn.AddThemeFontSizeOverride("font_size", 16);
            int idx = i;
            btn.Pressed += () =>
            {
                chosenEffect[0] = _event.Choices[idx].Effect;
                tcs.TrySetResult(_event.Choices[idx].Effect);
            };
            AddChild(btn);
        }

        string effect = await tcs.Task;
        if (string.IsNullOrEmpty(effect)) effect = "";

        // --- 执行效果 ---
        await ExecuteEffect(effect);

        // 标记区域已完成
        BattleStateManager.MarkAreaCompleted(_areaName);
        GD.Print($"[EventScene] 事件完成，区域 {_areaName} 已标记");
    }

    // ============================ 效果执行 ============================

    /// <summary>
    /// 解析并执行事件效果字符串。支持两种内置效果语法：
    /// replaceCard(卡牌ID)：让玩家从卡组中选择一张卡替换为指定卡；
    /// replaceRandomCard(卡牌ID)：随机替换卡组中的一张卡。
    /// 空字符串或"none"表示无效果，直接跳过。
    /// </summary>
    private async Task ExecuteEffect(string effect)
    {
        effect = effect.Trim();
        if (string.IsNullOrEmpty(effect) || effect == "none")
            return;

        if (effect.StartsWith("replaceCard(") && effect.EndsWith(")"))
        {
            string cardId = effect["replaceCard(".Length..^1];
            await DoReplaceCard(cardId, random: false);
        }
        else if (effect.StartsWith("replaceRandomCard(") && effect.EndsWith(")"))
        {
            string cardId = effect["replaceRandomCard(".Length..^1];
            await DoReplaceCard(cardId, random: true);
        }
    }

    /// <summary>
    /// 执行卡牌替换逻辑。random=false时弹出牌组选择UI让玩家手动挑选要替换的卡；
    /// random=true时随机从卡组中选一张替换。替换后同步更新DeckCardIds持久化列表。
    /// </summary>
    /// <param name="newCardId">新卡牌的ID（来自card.ini）</param>
    /// <param name="random">true=随机替换，false=让玩家手动选择</param>
    private async Task DoReplaceCard(string newCardId, bool random)
    {
        var deckIds = BattleStateManager.DeckCardIds;
        if (deckIds == null || deckIds.Count == 0) return;

        string oldCardId;
        if (random)
        {
            oldCardId = deckIds[new Random().Next(deckIds.Count)];
        }
        else
        {
            oldCardId = await ShowDeckPickUI();
            if (string.IsNullOrEmpty(oldCardId)) return;
        }

        int idx = deckIds.IndexOf(oldCardId);
        if (idx >= 0)
        {
            deckIds[idx] = newCardId;
            GD.Print($"[EventScene] 卡牌替换: {oldCardId} → {newCardId}");
        }
    }

    /// <summary>
    /// 显示牌组选择界面，让玩家挑选一张要替换的卡
    /// </summary>
    private async Task<string> ShowDeckPickUI()
    {
        // 清除事件UI
        foreach (var child in GetChildren())
            child.QueueFree();

        var tcs = new TaskCompletionSource<string>();
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.75f);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(bg);

        var viewSize = GetViewport().GetVisibleRect().Size;
        var title = new Label();
        title.Text = "选择一张要替换的卡牌";
        title.Position = new Vector2(viewSize.X / 2 - 200, 20);
        title.Size = new Vector2(400, 40);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", Colors.Gold);
        AddChild(title);

        float cardW = CardWidth * DeckReplaceScale;
        float cardH = CardHeight * DeckReplaceScale;
        float gapX = 8f; float gapY = 4f;
        const int cols = 7;
        var deckIds = BattleStateManager.DeckCardIds;
        int rows = (deckIds.Count + cols - 1) / cols;
        float gridW = cols * cardW + (cols - 1) * gapX;
        float gridStartX = (viewSize.X - gridW) / 2;

        var scrollContainer = new ScrollContainer();
        scrollContainer.Position = new Vector2(0, 70);
        scrollContainer.Size = new Vector2(viewSize.X, viewSize.Y - 130);
        scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        AddChild(scrollContainer);

        var cardContainer = new Control();
        cardContainer.CustomMinimumSize = new Vector2(viewSize.X, rows * (cardH + gapY) + 20);
        scrollContainer.AddChild(cardContainer);

        var cardScene = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        var chosenId = new List<string> { null };

        for (int i = 0; i < deckIds.Count; i++)
        {
            int col = i % cols, row = i / cols;
            float x = gridStartX + col * (cardW + gapX);
            float y = 10 + row * (cardH + gapY);
            string cid = deckIds[i];

            var card = cardScene.Instantiate() as cardBase_;
            card.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            card.Size = new Vector2(CardWidth, CardHeight);
            cardContainer.AddChild(card);
            var cd = BattleStateManager.GetCachedCard(cid);
            if (cd != null) card.SetCardInformation(cd);
            card.SetIsFriend(IsFriend.friend);
            card.Scale = new Vector2(DeckReplaceScale, DeckReplaceScale);
            card.Position = new Vector2(x, y);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.ZIndex = 10;

            // 高亮框外扩3px
            float pfX = CardWidth / 2f * (1f - DeckReplaceScale);
            float pfY = CardHeight / 2f * (1f - DeckReplaceScale);
            var hl = new ColorRect();
            hl.Position = new Vector2(x + pfX - 3, y + pfY - 3);
            hl.Size = new Vector2(cardW + 6, cardH + 6);
            hl.Color = new Color(0, 0, 0, 0);
            hl.MouseFilter = Control.MouseFilterEnum.Ignore;
            hl.ZIndex = 9;
            cardContainer.AddChild(hl);

            var click = new ColorRect();
            click.Position = new Vector2(x + pfX, y + pfY);
            click.Size = new Vector2(cardW, cardH);
            click.Color = new Color(0, 0, 0, 0);
            click.MouseFilter = Control.MouseFilterEnum.Stop;
            click.ZIndex = 20;
            int idx = i;
            click.GuiInput += (e) =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    chosenId[0] = deckIds[idx];
                    tcs.TrySetResult(deckIds[idx]);
                }
            };
            cardContainer.AddChild(click);
        }

        // 取消按钮
        var cancelBtn = new Button();
        cancelBtn.Text = "取消";
        cancelBtn.Position = new Vector2(viewSize.X / 2 - 50, viewSize.Y - 50);
        cancelBtn.Size = new Vector2(100, 36);
        cancelBtn.ZIndex = 60;
        cancelBtn.Pressed += () => tcs.TrySetResult(null);
        AddChild(cancelBtn);

        return await tcs.Task;
    }
}

/// <summary>
/// 事件数据模型：从event.ini解析得到的单个事件的全部信息。
/// 包含事件标识、标题、配图路径、剧情描述和最多4个选项。
/// </summary>
public class EventData
{
    /// <summary>事件唯一标识符，如 "supply_drop"、"ambush" 等，对应event.ini中的节名</summary>
    public string Id;
    /// <summary>事件标题，显示在界面上方金色标题栏中</summary>
    public string Title;
    /// <summary>事件配图的资源路径，如 "res://assest/event_supply.png"，显示在界面左侧</summary>
    public string Image;
    /// <summary>事件的剧情描述文本，支持BBCode格式，显示在右侧可滚动文字区</summary>
    public string Description;
    /// <summary>事件的选项列表（最多4个），每个选项包含显示文字和效果字符串</summary>
    public List<EventChoice> Choices = new();
}

/// <summary>
/// 事件选项模型：玩家在事件界面中可选择的单个选项。
/// 每个选项有一段描述文字和对应的效果指令字符串。
/// </summary>
public class EventChoice
{
    /// <summary>选项按钮上显示的文本，如"接受补给"、"继续前进"</summary>
    public string Text;
    /// <summary>选项的效果指令字符串，支持 replaceCard(id)、replaceRandomCard(id) 或 "none"</summary>
    public string Effect;
}
