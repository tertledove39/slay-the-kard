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
    private ColorRect[] _highlights;
    private List<string> _deckIds;

    private const float CardW = 180f;
    private const float CardH = 240f;
    private const float CardScale = 0.75f;
    private const int Cols = 7;
    private const float GapX = 8f;
    private const float GapY = 4f;
    private static readonly Color HlColor = new(1f, 0.84f, 0, 0.4f);
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
        _highlights = new ColorRect[_deckIds.Count];

        for (int i = 0; i < _deckIds.Count; i++)
        {
            int col = i % Cols, row = i / Cols;
            float x = startX + col * (cw + GapX), y = 10 + row * (ch + GapY);
            var cd = BattleStateManager.GetCachedCard(_deckIds[i]);
            if (cd == null) continue;

            var card = cardScene.Instantiate() as cardBase_;
            card.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            card.Size = new Vector2(CardW, CardH);
            container.AddChild(card);
            card.SetCardInformation(cd);
            card.SetIsFriend(IsFriend.friend);
            card.Scale = new Vector2(CardScale, CardScale);
            card.Position = new Vector2(x, y);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.ZIndex = 10;

            var hl = new ColorRect();
            hl.Position = new Vector2(x + pfX - 3, y + pfY - 3);
            hl.Size = new Vector2(cw + 6, ch + 6);
            hl.Color = new Color(0, 0, 0, 0);
            hl.MouseFilter = Control.MouseFilterEnum.Ignore;
            hl.ZIndex = 9;
            container.AddChild(hl);
            _highlights[i] = hl;

            var click = new ColorRect();
            click.Position = new Vector2(x + pfX, y + pfY);
            click.Size = new Vector2(cw, ch);
            click.Color = new Color(0, 0, 0, 0);
            click.MouseFilter = Control.MouseFilterEnum.Stop;
            click.ZIndex = 20;
            int idx = i;
            click.GuiInput += (e) =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    Toggle(idx);
            };
            container.AddChild(click);
        }
    }

    private void Toggle(int idx)
    {
        if (_selected.Contains(idx))
        {
            _selected.Remove(idx);
            if (_highlights[idx] != null) _highlights[idx].Color = new Color(0, 0, 0, 0);
        }
        else if (_selected.Count < _pickCount)
        {
            _selected.Add(idx);
            if (_highlights[idx] != null) _highlights[idx].Color = HlColor;
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
