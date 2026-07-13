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

    private const float CardDisplayScale = 0.75f; // 奖励组选择界面卡牌缩放
    private const float CardDisplayWidth = 180f;
    private const float CardDisplayHeight = 240f;
    private const int GroupsCount = 3;
    private const int CardsPerGroup = 5;
    private const int MaxSwapCards = 5;
    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;

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
        foreach (var child in GetChildren())
            child.QueueFree();
        var idsToRemove = await ChooseSomeCard.Show(this, MaxSwapCards, $"选择{MaxSwapCards}张要替换的卡牌");
        if (idsToRemove == null || idsToRemove.Count == 0)
            return; // 取消

        // 步骤4: 执行替换
        PerformDeckSwap(idsToRemove);
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
            btn.MouseEntered += () => AnimateButton(btn, HoverScale);
            btn.MouseExited += () => AnimateButton(btn, 1f);
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
        skipBtn.MouseEntered += () => AnimateButton(skipBtn, HoverScale);
        skipBtn.MouseExited += () => AnimateButton(skipBtn, 1f);
        skipBtn.Pressed += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(null); };
        AddChild(skipBtn);

        return await tcs.Task;
    }

    private static void AnimateButton(Button button, float scale)
    {
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
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

    // ============================ 卡组替换执行 ============================

    private void PerformDeckSwap(List<string> idsToRemove)
    {
        var deck = _player.ReadMyDeck();
        var toRemove = deck.Where(c => idsToRemove.Contains(c.id)).ToList();
        foreach (var card in toRemove)
            deck.Remove(card);

        foreach (var cardData in _chosenGroup)
        {
            var newCard = _bf.GetCardMaganer().GetCardTemplate().Duplicate() as cardBase_;
            newCard.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            newCard.Size = new Vector2(CardDisplayWidth, CardDisplayHeight);
            newCard.SetCardInformation(cardData);
            newCard.SetIsFriend(IsFriend.friend);
            deck.Add(newCard);
        }

        foreach (var id in idsToRemove)
            BattleStateManager.DeckCardIds.Remove(id);
        foreach (var cardData in _chosenGroup)
            BattleStateManager.DeckCardIds.Add(cardData.Id);
        GD.Print($"[PostBattleReward] DeckCardIds已更新，当前{BattleStateManager.DeckCardIds.Count}张卡");
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
