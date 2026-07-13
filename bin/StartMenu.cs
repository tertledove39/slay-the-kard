using Godot;
public partial class StartMenu : Control
{
    private const string WorldMapPath = "res://bin/worldMap.tscn";
    private const string SettingsPath = "res://bin/settings_menu.tscn";
    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;
    public void _on_credits_pressed()
    {
        var credits = GetNode<Label>("CreditsPanel");
        credits.Visible = !credits.Visible;
    }

    public void _on_settings_pressed()
    {
        _ = SceneLoader.ChangeSceneAsync(this, SettingsPath);
    }

    public void _on_start_pressed()
    {
        _ = SceneLoader.ChangeSceneAsync(this, WorldMapPath);
    }
    public void _on_continue_pressed()
    {
        _ = SceneLoader.ChangeSceneAsync(this, WorldMapPath);
    }

    public override void _Ready()
    {
        SettingsManager.Initialize();
        MusicManager.Instance?.PlaySlot("start_menu");
        ConnectHover("Menu/Continue");
        ConnectHover("Menu/Start");
        ConnectHover("Menu/Settings");
        ConnectHover("Menu/Credits");
    }

    private void ConnectHover(string path)
    {
        var button = GetNode<Button>(path);
        button.MouseEntered += () => AnimateButton(button, HoverScale);
        button.MouseExited += () => AnimateButton(button, 1f);
    }

    private static void AnimateButton(Button button, float scale)
    {
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }
}
