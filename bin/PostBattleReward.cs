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
    private const float HighlightBorderMargin = 3f; // 高亮框比卡牌多出的边框宽度

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
    /// 稀有度概率权重：Common 50%, Rare 30%, Epic 15%, Legendary 5%
    /// </summary>
    private static readonly (Rarity rarity, double weight)[] RarityWeights = new[]
    {
        (Rarity.Common, 0.50),
        (Rarity.Rare, 0.30),
        (Rarity.Epic, 0.15),
        (Rarity.Legendary, 0.05),
    };

    /// <summary>
    /// 稀有度回退顺序：优先常见卡
    /// </summary>
    private static readonly Rarity[] RarityFallbackOrder = { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

    /// <summary>
    /// 生成3组各5张的奖励卡牌。逐槽位按稀有度加权概率抽取，
    /// 实时检查每种卡在(牌组+当前奖励组)内的总数不超过该稀有度上限。
    /// </summary>
    private List<List<CardData>> GenerateRewardGroups()
    {
        var rnd = new Random();
        var allCards = _bf.GetCardMaganer().GetAllCards()
            .Where(c => c.IsHq == HQ.normalCard && c.Rarity != Rarity.Unobtainable)
            .ToList();

        if (allCards.Count == 0)
            return new List<List<CardData>>();

        var cardsByRarity = allCards.GroupBy(c => c.Rarity)
            .ToDictionary(g => g.Key, g => g.ToList());

        var deckCounts = new Dictionary<string, int>();
        foreach (var c in _player.ReadMyDeck())
        {
            if (!string.IsNullOrEmpty(c.id))
            {
                deckCounts.TryGetValue(c.id, out int cnt);
                deckCounts[c.id] = cnt + 1;
            }
        }

        var groups = new List<List<CardData>>();

        for (int g = 0; g < GroupsCount; g++)
        {
            var group = new List<CardData>();
            var groupCounts = new Dictionary<string, int>();

            for (int s = 0; s < CardsPerGroup; s++)
            {
                Rarity targetRarity = PickRarityByWeight(rnd);
                CardData chosen = TryPickCard(cardsByRarity, targetRarity, deckCounts, groupCounts, rnd);

                if (chosen == null)
                {
                    foreach (var fb in RarityFallbackOrder)
                    {
                        if (fb == targetRarity) continue;
                        chosen = TryPickCard(cardsByRarity, fb, deckCounts, groupCounts, rnd);
                        if (chosen != null) break;
                    }
                }

                if (chosen == null)
                {
                    var allValid = allCards.Where(card =>
                    {
                        int max = RarityMaxCopies.GetValueOrDefault(card.Rarity, int.MaxValue);
                        int dCnt = deckCounts.GetValueOrDefault(card.Id, 0);
                        int gCnt = groupCounts.GetValueOrDefault(card.Id, 0);
                        return dCnt + gCnt < max;
                    }).ToList();
                    chosen = allValid.Count > 0 ? allValid[rnd.Next(allValid.Count)] : allCards[rnd.Next(allCards.Count)];
                }

                group.Add(chosen);
                groupCounts.TryGetValue(chosen.Id, out int gc);
                groupCounts[chosen.Id] = gc + 1;
            }

            groups.Add(group);
        }

        return groups;
    }

    private static Rarity PickRarityByWeight(Random rnd)
    {
        double totalWeight = RarityWeights.Sum(w => w.weight);
        double roll = rnd.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var (rarity, weight) in RarityWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return rarity;
        }
        return RarityWeights.Last().rarity;
    }

    private static CardData TryPickCard(
        Dictionary<Rarity, List<CardData>> cardsByRarity,
        Rarity rarity,
        Dictionary<string, int> deckCounts,
        Dictionary<string, int> groupCounts,
        Random rnd)
    {
        if (!cardsByRarity.TryGetValue(rarity, out var pool) || pool.Count == 0)
            return null;

        int max = RarityMaxCopies.GetValueOrDefault(rarity, int.MaxValue);
        var valid = pool.Where(card =>
        {
            int dCnt = deckCounts.GetValueOrDefault(card.Id, 0);
            int gCnt = groupCounts.GetValueOrDefault(card.Id, 0);
            return dCnt + gCnt < max;
        }).ToList();

        return valid.Count == 0 ? null : valid[rnd.Next(valid.Count)];
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
        var title = MakeLabel("选择一组卡牌奖励", 30, Colors.Gold);
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
    /// 确保先入树、重置锚点、再设置信息，避免模板锚点导致渲染错位。
    /// </summary>
    private List<cardBase_> CreateGroupCardRow(List<CardData> group, float startX, float startY, Node parent)
    {
        var result = new List<cardBase_>();
        float scaledW = CardDisplayWidth * CardDisplayScale;
        float gap = 4f;
        for (int i = 0; i < group.Count; i++)
        {
            var card = _bf.GetCardMaganer().GetCardTemplate().Duplicate() as cardBase_;
            // 重置模板继承的锚点，切换为固定位置/尺寸模式
            card.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            card.Size = new Vector2(CardDisplayWidth, CardDisplayHeight);
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
        var title = MakeLabel($"选择{MaxSwapCards}张要替换的卡牌", 26, Colors.Gold);
        title.Position = new Vector2(viewSize.X / 2 - 250, 20);
        title.Size = new Vector2(500, 40);
        AddChild(title);

        var countLabel = MakeLabel($"已选: 0/{MaxSwapCards}", 20, Colors.White);
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

            // 先重置模板锚点→入树→再设置信息，确保渲染位置与高亮框对齐
            var card = _bf.GetCardMaganer().GetCardTemplate().Duplicate() as cardBase_;
            card.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            card.Size = new Vector2(CardDisplayWidth, CardDisplayHeight);
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

            // 因卡牌pivot在中心(90,120)，缩放后视觉左上角偏移，高亮框/点击覆盖需同步偏移
            Vector2 cvOff = GetCardPivotOffset(DeckReplaceScale);

            // 高亮框比卡牌大一圈（边框外扩），置于卡牌下方避免遮挡但边缘可见
            var highlight = new ColorRect();
            var hlPos = new Vector2(x, y) + cvOff;
            var hlSize = new Vector2(cardW, cardH) + new Vector2(HighlightBorderMargin * 2, HighlightBorderMargin * 2);
            highlight.Position = hlPos - new Vector2(HighlightBorderMargin, HighlightBorderMargin);
            highlight.Size = hlSize;
            highlight.Color = new Color(0, 0, 0, 0);
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
            highlight.ZIndex = 9;
            cardContainer.AddChild(highlight);
            highlightRects.Add(highlight);

            // 透明点击覆盖层（位置需加上pivot偏移以对齐卡牌视觉位置）
            var clickArea = new ColorRect();
            clickArea.Position = new Vector2(x, y) + cvOff;
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
        countLabel.Text = $"已选: {_selectedToRemove.Count}/{MaxSwapCards}";
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
            newCard.SetAnchorsPreset(Control.LayoutPreset.TopLeft); // 重置模板锚点
            newCard.Size = new Vector2(CardDisplayWidth, CardDisplayHeight);
            newCard.SetCardInformation(cardData);
            newCard.SetIsFriend(IsFriend.friend);
            deck.Add(newCard);
        }

        // 同步持久化卡组ID列表：移除被替换的卡，加入新卡
        var toRemoveIds = cardsToRemove.Select(c => c.id).ToList();
        foreach (var id in toRemoveIds)
            BattleStateManager.DeckCardIds.Remove(id);
        foreach (var cardData in _chosenGroup)
            BattleStateManager.DeckCardIds.Add(cardData.Id);
        GD.Print($"[PostBattleReward] DeckCardIds已更新，当前{ BattleStateManager.DeckCardIds.Count}张卡");
    }

    /// <summary>
    /// 卡牌pivot位于中心(90,120)，缩放后视觉左上角偏移 = pivot * (1 - Scale)。
    /// 高亮框/点击覆盖层需加上此偏移才能与卡牌视觉位置对齐。
    /// 参考battlefield_中 arrowOffset = Vector2(90, 120) 的偏移设计。
    /// </summary>
    private static Vector2 GetCardPivotOffset(float scale)
    {
        float pivotX = CardDisplayWidth / 2f;
        float pivotY = CardDisplayHeight / 2f;
        return new Vector2(pivotX * (1f - scale), pivotY * (1f - scale));
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
