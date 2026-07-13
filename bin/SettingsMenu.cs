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
            if (item.Type != "bool") continue;
            var toggle = new CheckButton { Text = item.Name, ButtonPressed = SettingsManager.GetBool(item.Key) };
            toggle.CustomMinimumSize = new Vector2(420, 48);
            toggle.AddThemeFontSizeOverride("font_size", 24);
            toggle.Toggled += value => SettingsManager.SetBool(item.Key, value);
            list.AddChild(toggle);
        }
    }

    private static void AnimateButton(Button button, float scale)
    {
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }
}
