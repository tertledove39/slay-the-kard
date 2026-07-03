using Godot;
using System;
using System.Collections.Generic;

public partial class Store : Control
{
    private cardBase_[] _cards = new cardBase_[7];
    private Label[] _priceLabels = new Label[7];
    private bool _awaitingDeckSelect;
    private Control _deckOverlay;
    private static readonly Color ColorCantAfford = new(1.0f, 0.27f, 0.0f);
    private static readonly Color ColorDiscount = new(0.2f, 0.5f, 1.0f);
    private const int RefreshCost = 5;
    private const int StoreCardCount = 7;
    private const float CardWidth = 180f;
    private const float CardHeight = 240f;
    private const float DeckScale = 0.75f;
    private const string CardScenePath = "res://bin/cardbase.tscn";
    private static readonly string[] CardNodeNames = { "card1", "card2", "card3", "card4", "card5", "card6", "card7" };

    public override void _Ready()
    {
        for (int i = 0; i < StoreCardCount; i++)
        {
            _cards[i] = GetNodeOrNull<cardBase_>(CardNodeNames[i]);
            if (_cards[i] != null) _cards[i].MouseFilter = MouseFilterEnum.Ignore;
        }
        var bg = GetNodeOrNull<TextureRect>("backGround");
        if (bg != null) bg.MouseFilter = MouseFilterEnum.Stop;
        var backBtn = GetNodeOrNull<Button>("Back");
        if (backBtn != null)
        {
            backBtn.Text = "返回";
            backBtn.Size = new Vector2(100, 44);
            var vs = GetViewport().GetVisibleRect().Size;
            backBtn.Position = new Vector2(vs.X - 120, vs.Y - 60);
        }
        if (BattleStateManager.StoreCurrentSlots == null) BattleStateManager.InitializeStoreSlots();
        DisplayCards();
    }

    private void DisplayCards()
    {
        var slots = BattleStateManager.StoreCurrentSlots;
        if (slots == null) return;
        for (int i = 0; i < StoreCardCount && i < slots.Count; i++)
        {
            var slot = slots[i];
            if (_cards[i] == null) continue;
            var cardData = BattleStateManager.GetCachedCard(slot.CardId);
            if (cardData == null) continue;
            _cards[i].SetCardInformation(cardData);
            _cards[i].SetIsFriend(IsFriend.friend);
            _cards[i].Modulate = slot.IsSold ? new Color(0.3f, 0.3f, 0.3f, 0.5f) : Colors.White;
            CreateOrUpdatePriceLabel(i);
        }
    }

    private void CreateOrUpdatePriceLabel(int index)
    {
        var slot = BattleStateManager.StoreCurrentSlots[index];
        if (_priceLabels[index] == null)
        {
            _priceLabels[index] = new Label();
            _priceLabels[index].HorizontalAlignment = HorizontalAlignment.Center;
            _priceLabels[index].AddThemeFontSizeOverride("font_size", 20);
            AddChild(_priceLabels[index]);
        }
        int price = slot.EffectivePrice;
        _priceLabels[index].Text = slot.IsDiscounted ? $"{slot.OriginalPrice} -> {price}" : price.ToString();
        var cardRect = _cards[index].GetRect();
        _priceLabels[index].Position = new Vector2(cardRect.Position.X + (cardRect.Size.X - 100) / 2, cardRect.Position.Y + cardRect.Size.Y + 5);
        _priceLabels[index].Size = new Vector2(100, 28);
    }

    public override void _Process(double delta) => UpdatePriceColors();

    private void UpdatePriceColors()
    {
        var slots = BattleStateManager.StoreCurrentSlots;
        if (slots == null) return;
        int points = BattleStateManager.MaterialPoints;
        for (int i = 0; i < StoreCardCount && i < slots.Count; i++)
        {
            if (_priceLabels[i] == null) continue;
            var slot = slots[i];
            _priceLabels[i].AddThemeColorOverride("font_color",
                slot.IsSold ? Colors.Gray :
                points < slot.EffectivePrice ? ColorCantAfford :
                slot.IsDiscounted ? ColorDiscount : Colors.White);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_awaitingDeckSelect) return;
        if (@event is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
        var slots = BattleStateManager.StoreCurrentSlots;
        if (slots == null) return;
        for (int i = 0; i < StoreCardCount && i < slots.Count; i++)
        {
            if (slots[i].IsSold || _cards[i] == null) continue;
            if (_cards[i].GetGlobalRect().HasPoint(mb.GlobalPosition)) { TryBuyCard(i); break; }
        }
    }

    private void TryBuyCard(int index)
    {
        var slot = BattleStateManager.StoreCurrentSlots[index];
        int price = slot.EffectivePrice;
        if (BattleStateManager.MaterialPoints < price) return;
        BattleStateManager.MaterialPoints -= price;
        slot.IsSold = true;
        UpdateMaterialPointsLabel();
        if (_cards[index] != null) _cards[index].Modulate = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        var cardData = BattleStateManager.GetCachedCard(slot.CardId);
        if (cardData != null) { _awaitingDeckSelect = true; ShowDeckSelection(cardData); }
    }

    private void ShowDeckSelection(CardData purchasedCard)
    {
        _deckOverlay = BuildDeckOverlay(purchasedCard, (selected) =>
        {
            if (selected >= 0 && selected < BattleStateManager.DeckCardIds.Count)
            {
                var oldId = BattleStateManager.DeckCardIds[selected];
                BattleStateManager.DeckCardIds[selected] = purchasedCard.Id;
                GD.Print($"[Store] 替换卡组第{selected}张: {oldId} -> {purchasedCard.Id}");
            }
            if (_deckOverlay != null) _deckOverlay.QueueFree();
            _deckOverlay = null;
            _awaitingDeckSelect = false;
        });
        AddChild(_deckOverlay);
        GD.Print($"[Store] 卡组选择界面已显示");
    }

    private Control BuildDeckOverlay(CardData purchasedCard, Action<int> onSelect)
    {
        var overlay = new Control();
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.ZIndex = 100;
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.85f);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        overlay.AddChild(bg);
        var vs = GetViewport().GetVisibleRect().Size;
        var title = new Label();
        title.Text = $"选择一张卡替换为 {purchasedCard.Name}";
        title.Position = new Vector2(vs.X / 2 - 300, 20);
        title.Size = new Vector2(600, 40);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", Colors.Gold);
        overlay.AddChild(title);
        BuildDeckGrid(overlay, onSelect, vs);
        var cancelBtn = new Button();
        cancelBtn.Text = "取消";
        cancelBtn.Position = new Vector2(vs.X / 2 - 50, vs.Y - 60);
        cancelBtn.Size = new Vector2(100, 44);
        cancelBtn.ZIndex = 60;
        cancelBtn.Pressed += () => onSelect(-1);
        overlay.AddChild(cancelBtn);
        return overlay;
    }

    private void BuildDeckGrid(Control parent, Action<int> onSelect, Vector2 vs)
    {
        var deckIds = BattleStateManager.DeckCardIds;
        if (deckIds == null || deckIds.Count == 0) return;
        float cardW = CardWidth * DeckScale, cardH = CardHeight * DeckScale;
        const int cols = 7;
        float gapX = 8, gapY = 4;
        int rows = (deckIds.Count + cols - 1) / cols;
        float gridW = cols * cardW + (cols - 1) * gapX;
        float startX = (vs.X - gridW) / 2;
        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(0, 70);
        scroll.Size = new Vector2(vs.X, vs.Y - 140);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        parent.AddChild(scroll);
        var container = new Control();
        container.CustomMinimumSize = new Vector2(vs.X, rows * (cardH + gapY) + 20);
        scroll.AddChild(container);
        var cardScene = ResourceLoader.Load<PackedScene>(CardScenePath);
        float pfX = CardWidth / 2f * (1f - DeckScale), pfY = CardHeight / 2f * (1f - DeckScale);
        for (int i = 0; i < deckIds.Count; i++)
        {
            int col = i % cols, row = i / cols;
            float x = startX + col * (cardW + gapX), y = 10 + row * (cardH + gapY);
            var cd = BattleStateManager.GetCachedCard(deckIds[i]);
            if (cd == null) continue;
            var card = cardScene.Instantiate() as cardBase_;
            card.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            card.Size = new Vector2(CardWidth, CardHeight);
            container.AddChild(card);
            card.SetCardInformation(cd);
            card.SetIsFriend(IsFriend.friend);
            card.Scale = new Vector2(DeckScale, DeckScale);
            card.Position = new Vector2(x, y);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.ZIndex = 10;
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
                    onSelect(idx);
            };
            container.AddChild(click);
        }
    }

    private void UpdateMaterialPointsLabel()
    {
        var pointNum = GetTree().CurrentScene.GetNodeOrNull<Label>("pointNum");
        if (pointNum != null) pointNum.Text = BattleStateManager.MaterialPoints.ToString();
    }

    void _on_texture_button_pressed()
    {
        if (_awaitingDeckSelect) return;
        if (BattleStateManager.MaterialPoints < RefreshCost) { GD.Print("[Store] 物资点不足"); return; }
        BattleStateManager.MaterialPoints -= RefreshCost;
        UpdateMaterialPointsLabel();
        BattleStateManager.RefreshStoreSlots();
        DisplayCards();
    }

    void _on_back_pressed()
    {
        if (GetParent() is CanvasLayer layer) layer.QueueFree();
        else QueueFree();
    }
}
