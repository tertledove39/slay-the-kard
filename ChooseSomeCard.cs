using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ChooseSomeCard : Control
{
    private TaskCompletionSource<List<string>> _tcs;
    private readonly HashSet<int> _selected = new();
    private int _pickCount;
    private Label _countLabel;
    private Button _confirmBtn;
    private cardBase_[] _cards;
    private Vector2[] _cardPositions;
    private List<string> _deckIds;
    private int _hovered = -1;

    private const float CardW = 180f;
    private const float CardH = 240f;
    private const float CardScale = 0.9f;
    private const float HoverScale = 0.96f;
    private const float SelectedScale = 0.99f;
    private const float CardRaise = 18f;
    private const int Cols = 5;
    private const float GapX = 32f;
    private const float GapY = 28f;
    private const float HoverDuration = 0.12f;
    private const string CardScenePath = "res://bin/cardbase.tscn";
    private const string ScenePath = "res://choose_some_card.tscn";
    private const int OverlayLayer = int.MaxValue;

    public static async Task<List<string>> Show(Node parent, int pickCount, string title)
    {
        if (parent?.GetTree()?.Root == null) return new List<string>();

        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        var inst = scene.Instantiate() as ChooseSomeCard;
        if (inst == null) return new List<string>();

        var layer = new CanvasLayer { Layer = OverlayLayer };
        parent.GetTree().Root.AddChild(layer);
        layer.AddChild(inst);
        var result = await inst.Run(pickCount, title);
        layer.QueueFree();
        return result;
    }

    private async Task<List<string>> Run(int pickCount, string title)
    {
        _tcs = new TaskCompletionSource<List<string>>();
        _pickCount = pickCount;
        _deckIds = BattleStateManager.DeckCardIds?.ToList() ?? new List<string>();

        if (_deckIds.Count == 0)
        {
            GD.Print("[ChooseSomeCard] 卡组为空");
            return new List<string>();
        }

        _deckIds = _deckIds
            .Select(id => BattleStateManager.GetCachedCard(id))
            .Where(cd => cd != null)
            .OrderBy(cd => cd.Cost)
            .ThenBy(cd => cd.Name)
            .ThenByDescending(cd => cd.Attack)
            .Select(cd => cd.Id)
            .ToList();

        BuildUI(title);
        return await _tcs.Task;
    }

    private void BuildUI(string title)
    {
        var vs = GetViewport().GetVisibleRect().Size;

        var titleLabel = GetNodeOrNull<Label>("Label");
        if (titleLabel != null)
        {
            titleLabel.Text = title;
            titleLabel.Position = new Vector2(vs.X / 2 - 300, 20);
            titleLabel.Size = new Vector2(600, 40);
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.AddThemeFontSizeOverride("font_size", 24);
            titleLabel.AddThemeColorOverride("font_color", Colors.Gold);
            titleLabel.ZIndex = 60;
        }

        _countLabel = new Label();
        _countLabel.Position = new Vector2(vs.X / 2 - 100, 58);
        _countLabel.Size = new Vector2(200, 30);
        _countLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _countLabel.AddThemeFontSizeOverride("font_size", 18);
        _countLabel.ZIndex = 60;
        AddChild(_countLabel);

        BuildGrid(vs);

        _confirmBtn = new Button();
        _confirmBtn.Text = "确认";
        _confirmBtn.Position = new Vector2(vs.X / 2 + 10, vs.Y - 55);
        _confirmBtn.Size = new Vector2(100, 40);
        _confirmBtn.ZIndex = 60;
        _confirmBtn.Pressed += OnConfirm;
        AddChild(_confirmBtn);

        var backBtn = GetNodeOrNull<Button>("Button");
        if (backBtn != null)
        {
            backBtn.ZIndex = 60;
        }
        UpdateCountLabel();
    }

    private void BuildGrid(Vector2 vs)
    {
        float cw = CardW * CardScale, ch = CardH * CardScale;
        int rows = (_deckIds.Count + Cols - 1) / Cols;
        float gridW = Cols * cw + (Cols - 1) * GapX;
        float startX = (vs.X - gridW) / 2;

        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(0, 90);
        scroll.Size = new Vector2(vs.X, vs.Y - 150);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        AddChild(scroll);

        var container = new Control();
        container.CustomMinimumSize = new Vector2(vs.X, rows * (ch + GapY) + 20);
        scroll.AddChild(container);

        var cardScene = ResourceLoader.Load<PackedScene>(CardScenePath);
        float pfX = CardW / 2f * (1f - CardScale), pfY = CardH / 2f * (1f - CardScale);
        _cards = new cardBase_[_deckIds.Count];
        _cardPositions = new Vector2[_deckIds.Count];

        for (int i = 0; i < _deckIds.Count; i++)
        {
            int col = i % Cols, row = i / Cols;
            float x = startX + col * (cw + GapX), y = 10 + row * (ch + GapY);
            var cd = BattleStateManager.GetCachedCard(_deckIds[i]);
            if (cd == null) continue;

            var card = cardScene.Instantiate() as cardBase_;
            card.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            card.Size = new Vector2(CardW, CardH);
            card.Scale = new Vector2(CardScale, CardScale);
            container.AddChild(card);
            card.SetCardInformation(cd);
            card.SetIsFriend(IsFriend.friend);
            card.Position = new Vector2(x, y);
            card.PivotOffset = new Vector2(CardW / 2f, CardH / 2f);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.ZIndex = 10;
            _cards[i] = card;
            _cardPositions[i] = card.Position;

            var click = new ColorRect();
            click.Position = new Vector2(x + pfX, y + pfY);
            click.Size = new Vector2(cw, ch);
            click.Color = new Color(0, 0, 0, 0);
            click.MouseFilter = Control.MouseFilterEnum.Ignore;
            click.ZIndex = 20;
            int idx = i;
            click.GuiInput += (e) =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    Toggle(idx);
            };
            click.MouseEntered += () => SetHovered(idx);
            click.MouseExited += () => SetHovered(-1);
            container.AddChild(click);
            Callable.From(() => click.MouseFilter = Control.MouseFilterEnum.Stop).CallDeferred();
        }
    }

    private void SetHovered(int idx)
    {
        if (_hovered == idx) return;
        int previous = _hovered;
        _hovered = idx;
        UpdateCardVisual(previous);
        UpdateCardVisual(idx);
    }

    private void UpdateCardVisual(int idx)
    {
        if (idx < 0 || idx >= _cards.Length || _cards[idx] == null) return;
        bool active = _selected.Contains(idx) || _hovered == idx;
        float scale = _selected.Contains(idx) ? SelectedScale : HoverScale;
        _cards[idx].SetHover(active, scale);
        _cards[idx].ZIndex = active ? 30 : 10;
        var tween = _cards[idx].CreateTween();
        tween.TweenProperty(_cards[idx], "position", _cardPositions[idx] + new Vector2(0, active ? -CardRaise : 0), HoverDuration);
    }

    private void Toggle(int idx)
    {
        if (_selected.Contains(idx))
        {
            _selected.Remove(idx);
            UpdateCardVisual(idx);
        }
        else if (_selected.Count < _pickCount)
        {
            _selected.Add(idx);
            UpdateCardVisual(idx);
        }
        UpdateCountLabel();
    }

    private void UpdateCountLabel()
    {
        _countLabel.Text = $"已选: {_selected.Count}/{_pickCount}";
        if (_confirmBtn != null)
            _confirmBtn.Disabled = _selected.Count != _pickCount;
    }

    private void OnConfirm()
    {
        if (_selected.Count != _pickCount) return;
        var result = _selected.Select(i => _deckIds[i]).ToList();
        _tcs.TrySetResult(result);
    }

    void _on_button_pressed()
    {
        _tcs.TrySetResult(new List<string>());
    }
}
