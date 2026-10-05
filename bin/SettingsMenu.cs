using Godot;

public partial class SettingsMenu : Control
{
    private const string StartMenuPath = "res://bin/start_menu.tscn";
    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;

    public override void _Ready()
    {
        SettingsManager.Initialize();
        BuildSettings();
        var back = GetNode<Button>("Back");
        back.Pressed += () => _ = SceneLoader.ChangeSceneAsync(this, StartMenuPath);
        back.MouseEntered += () => AnimateButton(back, HoverScale);
        back.MouseExited += () => AnimateButton(back, 1f);
    }

    private void BuildSettings()
    {
        var list = GetNode<VBoxContainer>("SettingsList");
        foreach (var item in SettingsManager.Items)
        {
            if (item.Type == "bool")
            {
                var toggle = new CheckButton { Text = item.Name, ButtonPressed = SettingsManager.GetBool(item.Key) };
                toggle.CustomMinimumSize = new Vector2(540, 48);
                toggle.AddThemeFontSizeOverride("font_size", 24);
                toggle.Toggled += value => SettingsManager.SetBool(item.Key, value);
                list.AddChild(toggle);
            }
            else if (item.Type == "float")
            {
                AddSlider(list, item);
            }
        }
    }

    private static void AddSlider(VBoxContainer list, SettingItem item)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(540, 48) };
        var name = new Label { Text = item.Name, CustomMinimumSize = new Vector2(130, 0) };
        name.AddThemeFontSizeOverride("font_size", 24);
        var slider = new HSlider
        {
            MinValue = item.MinValue,
            MaxValue = item.MaxValue,
            Step = item.Step,
            Value = SettingsManager.GetFloat(item.Key),
            CustomMinimumSize = new Vector2(330, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var valueLabel = new Label { Text = FormatValue(slider.Value), CustomMinimumSize = new Vector2(70, 0) };
        valueLabel.AddThemeFontSizeOverride("font_size", 22);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        slider.ValueChanged += value =>
        {
            valueLabel.Text = FormatValue(value);
            SettingsManager.SetFloat(item.Key, (float)value);
        };
        row.AddChild(name);
        row.AddChild(slider);
        row.AddChild(valueLabel);
        list.AddChild(row);
    }

    private static string FormatValue(double value) => Mathf.IsEqualApprox((float)value, Mathf.Round((float)value))
        ? Mathf.RoundToInt((float)value).ToString()
        : value.ToString("0.##");

    private static void AnimateButton(Button button, float scale)
    {
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }
}
