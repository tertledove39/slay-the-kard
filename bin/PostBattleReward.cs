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
    private PackedScene _cardRes;
    private List<CardData> _chosenGroup;
    private readonly HashSet<cardBase_> _selectedToRemove = new();

    private const float CardDisplayScale = 0.55f;
    private const float CardDisplayWidth = 180f;
    private const float CardDisplayHeight = 240f;
    private const float DeckReplaceScale = 0.58f; // 替换界面卡牌缩放略大于奖励界面
    private const int GroupsCount = 3;
    private const int CardsPerGroup = 5;
    private const int MaxSwapCards = 5;

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
        _cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");

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
    /// 从卡池中随机生成3组各5张不重复的非总部/非不可获得卡牌
    /// </summary>
    private List<List<CardData>> GenerateRewardGroups()
    {
        var rnd = new Random();
        var allCards = _bf.GetCardMaganer().GetAllCards();
        var validCards = allCards
            .Where(c => c.IsHq == HQ.normalCard && c.Rarity != Rarity.Unobtainable)
            .ToList();

        if (validCards.Count < GroupsCount * CardsPerGroup)
        {
            // 允许重复以满足数量要求
            while (validCards.Count < GroupsCount * CardsPerGroup)
                validCards.Add(validCards[rnd.Next(validCards.Count)]);
        }

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
            var cards = CreateGroupCardRow(groups[g], startX, gy);
            foreach (var c in cards) AddChild(c);

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
    /// 在一行中横向创建一组5张缩小版卡牌显示
    /// </summary>
    private List<cardBase_> CreateGroupCardRow(List<CardData> group, float startX, float startY)
    {
        var result = new List<cardBase_>();
        float scaledW = CardDisplayWidth * CardDisplayScale;
        float gap = 4f;
        for (int i = 0; i < group.Count; i++)
        {
            var card = _cardRes.Instantiate() as cardBase_;
            card.SetCardInformation(group[i]);
            card.SetIsFriend(IsFriend.friend);
            card.Scale = new Vector2(CardDisplayScale, CardDisplayScale);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.ZIndex = 50;
            card.Position = new Vector2(startX + i * (scaledW + gap), startY);
            card.RefreshState();
            result.Add(card);
        }
        return result;
    }

    // ============================ 卡组替换界面 ============================

    /// <summary>
    /// 显示玩家卡组，选择最多5张卡牌替换。卡牌按费用→名称→攻击力排序，与牌库展示一致。
    /// 点击检测通过背景GuiInput+全局矩形判断，避免子控件拦截点击。
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

        // 卡组网格展示：按费用→名称→攻击力降序排序（与DisplayCard一致）
        var deck = _player.ReadMyDeck();
        var sortedDeck = deck
            .OrderBy(c => c.ReadCost())
            .ThenBy(c => c.name)
            .ThenByDescending(c => c.ReadAttack())
            .ToList();

        var cardDisplays = new List<cardBase_>();
        var highlightRects = new List<ColorRect>();
        float cardW = CardDisplayWidth * DeckReplaceScale;
        float cardH = CardDisplayHeight * DeckReplaceScale;
        int cols = 7;
        float gridStartX = (viewSize.X - cols * (cardW + 8)) / 2;
        float gridStartY = 100;

        for (int i = 0; i < sortedDeck.Count; i++)
        {
            var deckCard = sortedDeck[i];
            int col = i % cols;
            int row = i / cols;

            var card = CreateCardFromSource(deckCard);
            card.Scale = new Vector2(DeckReplaceScale, DeckReplaceScale);
            card.Position = new Vector2(gridStartX + col * (cardW + 8), gridStartY + row * (cardH + 4));
            card.MouseFilter = Control.MouseFilterEnum.Ignore; // 忽略点击，由背景统一处理
            card.ZIndex = 50;
            AddChild(card);
            cardDisplays.Add(card);

            // 高亮框
            var highlight = new ColorRect();
            highlight.Position = card.Position;
            highlight.Size = new Vector2(cardW, cardH);
            highlight.Color = new Color(0, 0, 0, 0);
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
            highlight.ZIndex = 49;
            AddChild(highlight);
            highlightRects.Add(highlight);
        }

        // 通过背景GuiInput统一处理点击（避免卡牌子控件拦截）
        bg.GuiInput += (e) =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                for (int i = 0; i < cardDisplays.Count; i++)
                {
                    if (cardDisplays[i].GetGlobalRect().HasPoint(mb.GlobalPosition))
                    {
                        ToggleCardSelection(sortedDeck[i], highlightRects[i], countLabel);
                        break;
                    }
                }
            }
        };

        // 确认按钮
        float btnY = gridStartY + ((sortedDeck.Count - 1) / cols + 1) * (cardH + 4) + 20;
        var confirmBtn = new Button();
        confirmBtn.Text = $"确认替换（需选{MaxSwapCards}张）";
        confirmBtn.Position = new Vector2(viewSize.X / 2 - 90, btnY);
        confirmBtn.Size = new Vector2(180, 44);
        confirmBtn.Disabled = true;
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
            var newCard = _cardRes.Instantiate() as cardBase_;
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

    private cardBase_ CreateCardFromSource(cardBase_ source)
    {
        var card = _cardRes.Instantiate() as cardBase_;
        // 尝试从CardMaganer获取完整数据，否则手动构建
        var cd = _bf.GetCardMaganer().GetCard(source.id);
        if (cd != null)
        {
            card.SetCardInformation(cd);
        }
        else
        {
            card.name = source.name;
            card.id = source.id;
            card.description = source.description;
            card.cardType = source.cardType;
            card.rarity = source.rarity;
            card.traits = source.traits;
            card.cost = source.ReadCost();
            card.attack = source.ReadAttack();
            card.defence = source.ReadDefence();
            card.effect = source.effect;
        }
        card.SetIsFriend(IsFriend.friend);
        card.ZIndex = 50;
        return card;
    }
}
