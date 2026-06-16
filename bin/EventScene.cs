using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 事件场景界面：左侧事件图(400x600)、上方标题栏、右侧可滚动文字、下方选项按钮
/// </summary>
public partial class EventScene : CanvasLayer
{
    private EventData _event;
    private string _areaName;

    private const float ImageWidth = 400f;
    private const float ImageHeight = 600f;
    private const float DeckReplaceScale = 0.75f;
    private const float CardWidth = 180f;
    private const float CardHeight = 240f;
    private const int MaxSwapCards = 1;

    public static async Task Show(Node parent, EventData eventData, string areaName)
    {
        var scene = new EventScene { Layer = 2 };
        parent.AddChild(scene);
        await scene.Run(eventData, areaName);
        scene.QueueFree();
    }

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
        if (ResourceLoader.Exists(_event.Image))
            imgRect.Texture = ResourceLoader.Load<Texture2D>(_event.Image);
        else
            imgRect.Color = new Color(0.3f, 0.3f, 0.3f);
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
        descLabel.Size = new Vector2(480, 0);
        descLabel.FitContent = true;
        descLabel.BbcodeEnabled = true;
        descLabel.Text = "[font_size=18]" + _event.Description + "[/font_size]";
        descLabel.AddThemeColorOverride("default_color", Colors.White);
        scroll.AddChild(descLabel);

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

        // 标记区域已完成，返回WorldMap
        BattleStateManager.MarkAreaCompleted(_areaName);
        GD.Print($"[EventScene] 事件完成，区域 {_areaName} 已标记");
        GetTree().ChangeSceneToFile("res://bin/worldMap.tscn");
    }

    // ============================ 效果执行 ============================

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
/// 事件数据
/// </summary>
public class EventData
{
    public string Id;
    public string Title;
    public string Image;
    public string Description;
    public List<EventChoice> Choices = new();
}

/// <summary>
/// 事件选项
/// </summary>
public class EventChoice
{
    public string Text;
    public string Effect;
}
