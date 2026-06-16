using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 战后卡牌奖励选择界面：显示3组卡牌（每组5张），玩家可选一组替换卡组中的5张
/// </summary>
public partial class PostBattleReward : CanvasLayer
{
    private battlefield_ _bf;
    private Player _player;
    private List<CardData> _chosenGroup;
    private readonly HashSet<cardBase_> _selectedToRemove = new();

    private const float CardDisplayScale = 0.75f; // 奖励组选择界面卡牌缩放
    private const float CardDisplayWidth = 180f;
    private const float CardDisplayHeight = 240f;
    private const float DeckReplaceScale = 0.75f;  // 替换界面卡牌缩放（与组选择一致）
    private const int GroupsCount = 3;
    private const int CardsPerGroup = 5;
    private const int MaxSwapCards = 5;

    // 每种稀有度在牌组中的最大拥有数量（按卡牌ID计）
    private static readonly Dictionary<Rarity, int> RarityMaxCopies = new()
    {
        { Rarity.Common, 4 },
        { Rarity.Rare, 3 },
        { Rarity.Epic, 2 },
        { Rarity.Legendary, 1 },
    };

    /// <summary>
    /// 静态入口：创建实例并执行奖励流程
    /// </summary>
    public static async Task Show(battlefield_ bf, Player player)
    {
        var reward = new PostBattleReward();
        reward.Layer = 2;
        bf.AddChild(reward);
        await reward.Run(bf, player);
        reward.QueueFree();
    }

    private async Task Run(battlefield_ bf, Player player)
    {
        _bf = bf;
        _player = player;

        // 步骤1: 生成3组奖励卡牌
        var groups = GenerateRewardGroups();
        if (groups == null || groups.Count < GroupsCount)
        {
            GD.Print("PostBattleReward: 无法生成足够的奖励组");
            return;
        }

        // 步骤2: 显示组选择界面
        _chosenGroup = await ShowGroupSelection(groups);
        if (_chosenGroup == null)
            return; // 玩家跳过

        // 步骤3: 显示卡组替换界面
        var cardsToRemove = await ShowDeckReplaceUI();
        if (cardsToRemove == null || cardsToRemove.Count != MaxSwapCards)
            return; // 取消

        // 步骤4: 执行替换
        PerformDeckSwap(cardsToRemove);
    }

    // ============================ 卡牌生成 ============================

    /// <summary>
    /// 从卡池中随机生成3组各5张不重复的非总部/非不可获得卡牌。
    /// 按稀有度过滤：若牌组中某卡已达到稀有度上限，则该卡不会出现在奖励中。
    /// </summary>
    private List<List<CardData>> GenerateRewardGroups()
    {
        var rnd = new Random();
        var allCards = _bf.GetCardMaganer().GetAllCards();

        // 统计当前牌组中各卡牌ID的数量
        var deckCounts = new Dictionary<string, int>();
        foreach (var c in _player.ReadMyDeck())
        {
            if (!string.IsNullOrEmpty(c.id))
            {
                deckCounts.TryGetValue(c.id, out int count);
                deckCounts[c.id] = count + 1;
            }
        }

        // 过滤：非HQ、非不可获得，且加入后不超过稀有度上限
        var validCards = allCards
            .Where(c => c.IsHq == HQ.normalCard && c.Rarity != Rarity.Unobtainable)
            .Where(c =>
            {
                if (!RarityMaxCopies.TryGetValue(c.Rarity, out int maxCopies))
                    return true; // 未配置稀有度限制的默认允许
                deckCounts.TryGetValue(c.Id, out int currentCount);
                return currentCount < maxCopies;
            })
            .ToList();

        // 如果过滤后卡牌不足，回退为不限稀有度（保障始终能生成奖励）
        if (validCards.Count < CardsPerGroup)
        {
            GD.Print($"[PostBattleReward] 稀有度过滤后仅{validCards.Count}张可用卡，回退为不限稀有度");
            validCards = allCards
                .Where(c => c.IsHq == HQ.normalCard && c.Rarity != Rarity.Unobtainable)
                .ToList();
        }

        // 允许重复以满足数量要求
        while (validCards.Count < GroupsCount * CardsPerGroup)
            validCards.Add(validCards[rnd.Next(validCards.Count)]);

        var shuffled = validCards.OrderBy(_ => rnd.Next()).ToList();
        var groups = new List<List<CardData>>();
        int idx = 0;
        for (int g = 0; g < GroupsCount; g++)
        {
            var group = new List<CardData>();
            for (int c = 0; c < CardsPerGroup && idx < shuffled.Count; c++, idx++)
                group.Add(shuffled[idx]);
            groups.Add(group);
        }
        return groups;
    }

    // ============================ 组选择界面 ============================

    /// <summary>
    /// 显示3组卡牌供玩家选择一组（或跳过），每组5张横向排列，各组纵向堆叠
    /// </summary>
    private async Task<List<CardData>> ShowGroupSelection(List<List<CardData>> groups)
    {
        var tcs = new TaskCompletionSource<List<CardData>>();

        var bg = DarkBackground();
        AddChild(bg);

        // 标题
        var viewSize = GetViewport().GetVisibleRect().Size;
        var title = MakeLabel("[center]选择一组卡牌奖励[/center]", 30, Colors.Gold);
        title.Position = new Vector2(viewSize.X / 2 - 250, 30);
        title.Size = new Vector2(500, 50);
        AddChild(title);

        // 计算横向布局参数：5张卡牌一行
        float scaledW = CardDisplayWidth * CardDisplayScale;
        float scaledH = CardDisplayHeight * CardDisplayScale;
        float cardGap = 4f;
        float rowWidth = CardsPerGroup * scaledW + (CardsPerGroup - 1) * cardGap;
        float btnWidth = 130f;
        float btnHeight = 38f;
        float rowTotalWidth = rowWidth + 30 + btnWidth; // 卡牌行 + 间距 + 按钮
        float startX = (viewSize.X - rowTotalWidth) / 2;
        float groupGap = 10f;
        float groupHeight = scaledH + btnHeight + groupGap;
        float startY = 120;

        for (int g = 0; g < groups.Count; g++)
        {
            float gy = startY + g * groupHeight;
            var cards = CreateGroupCardRow(groups[g], startX, gy, this);

            // 选择按钮放在卡牌行右侧垂直居中
            var btn = new Button();
            btn.Text = $"选择第{g + 1}组";
            btn.Position = new Vector2(startX + rowWidth + 30, gy + (scaledH - btnHeight) / 2);
            btn.Size = new Vector2(btnWidth, btnHeight);
            int gi = g;
            btn.Pressed += () =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(groups[gi]);
            };
            AddChild(btn);
        }

        // "跳过"按钮
        float btnAreaBottom = startY + GroupsCount * groupHeight;
        var skipBtn = new Button();
        skipBtn.Text = "跳过奖励";
        skipBtn.Position = new Vector2(viewSize.X / 2 - 70, btnAreaBottom + 20);
        skipBtn.Size = new Vector2(140, 44);
        skipBtn.Pressed += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(null); };
        AddChild(skipBtn);

        return await tcs.Task;
    }

    /// <summary>
    /// 在一行中横向创建一组5张缩小版卡牌显示。
    /// 确保先加入场景树再设置信息，避免字体/布局测量错误导致渲染错位。
    /// </summary>
    private List<cardBase_> CreateGroupCardRow(List<CardData> group, float startX, float startY, Node parent)
    {
        var result = new List<cardBase_>();
        float scaledW = CardDisplayWidth * CardDisplayScale;
        float gap = 4f;
        for (int i = 0; i < group.Count; i++)
        {
            var card = _bf.GetCardMaganer().GetCardTemplate().Duplicate() as cardBase_;
            parent.AddChild(card); // 先入场景树触发_Ready，确保字体测量可用
            card.SetCardInformation(group[i]); // SetCardInformation内部已调用RefreshState
            card.SetIsFriend(IsFriend.friend);
            card.Scale = new Vector2(CardDisplayScale, CardDisplayScale);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.ZIndex = 50;
            card.Position = new Vector2(startX + i * (scaledW + gap), startY);
            result.Add(card);
        }
        return result;
    }

    // ============================ 卡组替换界面 ============================

    /// <summary>
    /// 显示玩家卡组（可滚动），选择最多5张卡牌替换。
    /// 卡牌按费用→名称→攻击力排序（与牌库展示一致）。
    /// 使用透明ColorRect覆盖层处理点击，避免卡牌内部子控件拦截事件。
    /// </summary>
    private async Task<List<cardBase_>> ShowDeckReplaceUI()
    {
        // 清除旧UI
        foreach (var child in GetChildren())
            child.QueueFree();
        _selectedToRemove.Clear();

        var tcs = new TaskCompletionSource<List<cardBase_>>();

        var bg = DarkBackground();
        AddChild(bg);

        // 标题与计数
        var viewSize = GetViewport().GetVisibleRect().Size;
        var title = MakeLabel($"[center]选择{MaxSwapCards}张要替换的卡牌[/center]", 26, Colors.Gold);
        title.Position = new Vector2(viewSize.X / 2 - 250, 20);
        title.Size = new Vector2(500, 40);
        AddChild(title);

        var countLabel = MakeLabel($"[center]已选: 0/{MaxSwapCards}[/center]", 20, Colors.White);
        countLabel.Position = new Vector2(viewSize.X / 2 - 100, 58);
        countLabel.Size = new Vector2(200, 30);
        AddChild(countLabel);

        // 排序牌库：费用→名称→攻击力降序（与DisplayCard一致）
        var deck = _player.ReadMyDeck();
        var sortedDeck = deck
            .OrderBy(c => c.ReadCost())
            .ThenBy(c => c.name)
            .ThenByDescending(c => c.ReadAttack())
            .ToList();

        // 网格布局参数
        float cardW = CardDisplayWidth * DeckReplaceScale;
        float cardH = CardDisplayHeight * DeckReplaceScale;
        float cardGapX = 8f;
        float cardGapY = 4f;
        const int cols = 7;
        int rows = (sortedDeck.Count + cols - 1) / cols;
        float gridContentW = cols * cardW + (cols - 1) * cardGapX; // 网格实际宽度，用于居中
        float gridStartX = (viewSize.X - gridContentW) / 2;
        float gridContentH = rows * cardH + (rows - 1) * cardGapY + 20; // 内容总高度（含上下边距）

        // 滚动容器区域：从标题下方到底部按钮上方
        float scrollTop = 90;
        float scrollBottomPad = 70;
        var scrollContainer = new ScrollContainer();
        scrollContainer.Position = new Vector2(0, scrollTop);
        scrollContainer.Size = new Vector2(viewSize.X, viewSize.Y - scrollTop - scrollBottomPad);
        scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scrollContainer.FollowFocus = true;
        AddChild(scrollContainer);

        // 内容容器
        var cardContainer = new Control();
        cardContainer.CustomMinimumSize = new Vector2(viewSize.X, Mathf.Max(gridContentH, scrollContainer.Size.Y));
        scrollContainer.AddChild(cardContainer);

        // 创建卡牌显示、高亮框和透明点击覆盖层
        var cardDisplays = new List<cardBase_>();
        var highlightRects = new List<ColorRect>();

        for (int i = 0; i < sortedDeck.Count; i++)
        {
            var deckCard = sortedDeck[i];
            int col = i % cols;
            int row = i / cols;

            float x = gridStartX + col * (cardW + cardGapX);
            float y = 10 + row * (cardH + cardGapY);

            // 先加入场景树触发_Ready，再设置卡牌信息，确保字体测量/布局正确
            var card = _bf.GetCardMaganer().GetCardTemplate().Duplicate() as cardBase_;
            cardContainer.AddChild(card); // 先入树，originalScale记录为1.0

            var cd = _bf.GetCardMaganer().GetCard(deckCard.id);
            if (cd != null)
                card.SetCardInformation(cd); // SetCardInformation内部已调用RefreshState
            else
                card.SetCardInformation(new CardData
                {
                    Name = deckCard.name, Id = deckCard.id,
                    Attack = deckCard.ReadAttack(), Defense = deckCard.ReadDefence(),
                    Cost = deckCard.ReadCost(), CardType = deckCard.cardType,
                    Rarity = deckCard.rarity, IconPath = deckCard.IconPath,
                    Description = deckCard.description, Effect = deckCard.effect,
                    Traits = deckCard.traits, TargetType = deckCard.targetType,
                    IsHq = deckCard.isHq
                });
            card.SetIsFriend(IsFriend.friend);
            card.Scale = new Vector2(DeckReplaceScale, DeckReplaceScale);
            card.Position = new Vector2(x, y);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.ZIndex = 10;
            cardDisplays.Add(card);

            // 高亮框（置于卡牌下方）
            var highlight = new ColorRect();
            highlight.Position = new Vector2(x, y);
            highlight.Size = new Vector2(cardW, cardH);
            highlight.Color = new Color(0, 0, 0, 0);
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
            highlight.ZIndex = 9;
            cardContainer.AddChild(highlight);
            highlightRects.Add(highlight);

            // 透明点击覆盖层（无子节点，确保点击事件可靠捕获）
            var clickArea = new ColorRect();
            clickArea.Position = new Vector2(x, y);
            clickArea.Size = new Vector2(cardW, cardH);
            clickArea.Color = new Color(0, 0, 0, 0);
            clickArea.MouseFilter = Control.MouseFilterEnum.Stop;
            clickArea.ZIndex = 20;
            int idx = i;
            clickArea.GuiInput += (e) =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    ToggleCardSelection(sortedDeck[idx], highlightRects[idx], countLabel);
            };
            cardContainer.AddChild(clickArea);
        }

        // 确认按钮（固定在PostBattleReward中，不随滚动移动）
        var confirmBtn = new Button();
        confirmBtn.Text = $"确认替换（需选{MaxSwapCards}张）";
        confirmBtn.Position = new Vector2(viewSize.X / 2 - 90, viewSize.Y - 60);
        confirmBtn.Size = new Vector2(180, 44);
        confirmBtn.Disabled = true;
        confirmBtn.ZIndex = 60;
        AddChild(confirmBtn);

        confirmBtn.Pressed += () =>
        {
            if (_selectedToRemove.Count == MaxSwapCards && !tcs.Task.IsCompleted)
                tcs.SetResult(_selectedToRemove.ToList());
        };

        // 每帧更新按钮状态
        while (!tcs.Task.IsCompleted)
        {
            confirmBtn.Disabled = _selectedToRemove.Count != MaxSwapCards;
            confirmBtn.Text = $"确认替换（已选{_selectedToRemove.Count}/{MaxSwapCards}）";
            await Task.Delay(100);
        }

        return await tcs.Task;
    }

    private void ToggleCardSelection(cardBase_ card, ColorRect highlight, Label countLabel)
    {
        if (_selectedToRemove.Contains(card))
        {
            _selectedToRemove.Remove(card);
            highlight.Color = new Color(0, 0, 0, 0);
        }
        else if (_selectedToRemove.Count < MaxSwapCards)
        {
            _selectedToRemove.Add(card);
            highlight.Color = new Color(1f, 0.84f, 0, 0.4f);
        }
        countLabel.Text = $"[center]已选: {_selectedToRemove.Count}/{MaxSwapCards}[/center]";
    }

    // ============================ 卡组替换执行 ============================

    private void PerformDeckSwap(List<cardBase_> cardsToRemove)
    {
        var deck = _player.ReadMyDeck();

        foreach (var card in cardsToRemove)
            deck.Remove(card);

        foreach (var cardData in _chosenGroup)
        {
            var newCard = _bf.GetCardMaganer().GetCardTemplate().Duplicate() as cardBase_;
            newCard.SetCardInformation(cardData);
            newCard.SetIsFriend(IsFriend.friend);
            deck.Add(newCard);
        }

        // 同步到BattleStateManager
        if (BattleStateManager.IsCampaignMode)
            BattleStateManager.Deck = deck;
    }

    // ============================ UI辅助 ============================

    private ColorRect DarkBackground()
    {
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.75f);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        return bg;
    }

    private Label MakeLabel(string text, int fontSize, Color color)
    {
        var lb = new Label();
        lb.Text = text;
        lb.AddThemeColorOverride("font_color", color);
        lb.AddThemeFontSizeOverride("font_size", fontSize);
        lb.HorizontalAlignment = HorizontalAlignment.Center;
        return lb;
    }

}
