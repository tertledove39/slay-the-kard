using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Store : Control
{
    private cardBase_[] _cards = new cardBase_[7];
    private Label[] _priceLabels = new Label[7];
    private Vector2[] _cardPositions = new Vector2[7];
    private bool _awaitingDeckSelect;
    private int _hoveredCard = -1;
    private int _lastPricePoints = int.MinValue;
    private readonly bool[] _lastSoldStates = new bool[StoreCardCount];
    private readonly bool[] _lastDiscountStates = new bool[StoreCardCount];
    private readonly int[] _lastEffectivePrices = new int[StoreCardCount];
    private readonly bool[] _priceStateInitialized = new bool[StoreCardCount];
    private readonly Tween[] _cardTweens = new Tween[StoreCardCount];
    private static readonly Color ColorCantAfford = new(1.0f, 0.27f, 0.0f);
    private static readonly Color ColorDiscount = new(0.2f, 0.5f, 1.0f);
    private const int RefreshCost = 5;
    private const int StoreCardCount = 7;
    private const float CardRaise = 18f;
    private const float CardAnimationDuration = 0.12f;
    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;
    private static readonly string[] CardNodeNames = { "card1", "card2", "card3", "card4", "card5", "card6", "card7" };

    public override void _Ready()
    {
        for (int i = 0; i < StoreCardCount; i++)
        {
            _cards[i] = GetNodeOrNull<cardBase_>(CardNodeNames[i]);
            if (_cards[i] != null)
            {
                _cards[i].MouseFilter = MouseFilterEnum.Ignore;
                _cardPositions[i] = _cards[i].Position;
            }
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
            backBtn.MouseEntered += () => AnimateButton(backBtn, HoverScale);
            backBtn.MouseExited += () => AnimateButton(backBtn, 1f);
        }
        var refreshBtn = GetNodeOrNull<TextureButton>("refresh");
        if (refreshBtn != null)
        {
            refreshBtn.MouseEntered += () => AnimateButton(refreshBtn, HoverScale);
            refreshBtn.MouseExited += () => AnimateButton(refreshBtn, 1f);
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

    public override void _Process(double delta)
    {
        UpdatePriceColors();
        UpdateHoveredCard();
    }

    private void UpdateHoveredCard()
    {
        int hovered = -1;
        if (!_awaitingDeckSelect && BattleStateManager.StoreCurrentSlots != null)
        {
            var mousePosition = GetViewport().GetMousePosition();
            for (int i = 0; i < StoreCardCount; i++)
            {
                if (!BattleStateManager.StoreCurrentSlots[i].IsSold && _cards[i]?.GetGlobalRect().HasPoint(mousePosition) == true)
                {
                    hovered = i;
                    break;
                }
            }
        }
        SetHoveredCard(hovered);
    }

    private void SetHoveredCard(int index)
    {
        if (_hoveredCard == index) return;
        UpdateCardVisual(_hoveredCard, false);
        _hoveredCard = index;
        UpdateCardVisual(_hoveredCard, true);
    }

    private void UpdateCardVisual(int index, bool hovered)
    {
        if (index < 0 || index >= StoreCardCount || _cards[index] == null) return;
        _cards[index].SetHover(hovered);
        if (_cardTweens[index] != null && _cardTweens[index].IsValid()) _cardTweens[index].Kill();
        _cardTweens[index] = _cards[index].CreateTween();
        _cardTweens[index].TweenProperty(_cards[index], "position", _cardPositions[index] + new Vector2(0, hovered ? -CardRaise : 0), CardAnimationDuration);
    }

    private void UpdatePriceColors()
    {
        var slots = BattleStateManager.StoreCurrentSlots;
        if (slots == null) return;
        int points = BattleStateManager.MaterialPoints;
        bool pointsChanged = points != _lastPricePoints;
        for (int i = 0; i < StoreCardCount && i < slots.Count; i++)
        {
            if (_priceLabels[i] == null) continue;
            var slot = slots[i];
            bool stateChanged = !_priceStateInitialized[i] ||
                _lastSoldStates[i] != slot.IsSold ||
                _lastDiscountStates[i] != slot.IsDiscounted ||
                _lastEffectivePrices[i] != slot.EffectivePrice;
            if (!pointsChanged && !stateChanged) continue;
            _priceStateInitialized[i] = true;
            _lastSoldStates[i] = slot.IsSold;
            _lastDiscountStates[i] = slot.IsDiscounted;
            _lastEffectivePrices[i] = slot.EffectivePrice;
            _priceLabels[i].AddThemeColorOverride("font_color",
                slot.IsSold ? Colors.Gray :
                points < slot.EffectivePrice ? ColorCantAfford :
                slot.IsDiscounted ? ColorDiscount : Colors.White);
        }
        _lastPricePoints = points;
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
        if (BattleStateManager.MaterialPoints < price)
        {
            GD.Print($"[Store] 物资点不足: {BattleStateManager.MaterialPoints} < {price} ({slot.CardId})");
            FlashLabel(index);
            return;
        }
        BattleStateManager.MaterialPoints -= price;
        slot.IsSold = true;
        SetHoveredCard(-1);
        UpdateMaterialPointsLabel();
        if (_cards[index] != null) _cards[index].Modulate = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        var cardData = BattleStateManager.GetCachedCard(slot.CardId);
        if (cardData == null)
        {
            GD.PrintErr($"[Store] 卡牌数据未找到: {slot.CardId}");
            return;
        }
        _awaitingDeckSelect = true;
        _ = DoBuyCard(cardData);
    }

    private async Task DoBuyCard(CardData purchasedCard)
    {
        var selected = await ChooseSomeCard.Show(this, 1,
            $"选择一张卡替换为 {purchasedCard.Name}");

        if (selected.Count > 0)
        {
            var idx = BattleStateManager.DeckCardIds.IndexOf(selected[0]);
            if (idx >= 0)
            {
                GD.Print($"[Store] 替换卡组第{idx}张: {selected[0]} -> {purchasedCard.Id}");
                BattleStateManager.DeckCardIds[idx] = purchasedCard.Id;
            }
        }

        _awaitingDeckSelect = false;
    }

    private void FlashLabel(int index)
    {
        if (_priceLabels[index] == null) return;
        var tween = CreateTween();
        tween.TweenProperty(_priceLabels[index], "modulate", ColorCantAfford, 0.1);
        tween.TweenProperty(_priceLabels[index], "modulate", Colors.White, 0.3);
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

    private static void AnimateButton(CanvasItem button, float scale)
    {
        if (button is Control control) control.PivotOffset = control.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }
}
