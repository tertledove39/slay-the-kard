using Godot;
using System.Threading.Tasks;

public partial class StartMenu : Control
{
    private const string WorldMapPath = "res://bin/worldMap.tscn";
    private const string SettingsPath = "res://bin/settings_menu.tscn";
    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;

    public override void _Ready()
    {
        SettingsManager.Initialize();
        ConnectButton("Continue", OpenWorldMap);
        ConnectButton("Start", OpenWorldMap);
        ConnectButton("Settings", OpenSettings);
        ConnectButton("Credits", ShowCredits);
    }

    private void ConnectButton(string name, System.Action action)
    {
        var button = GetNode<Button>(name);
        button.Pressed += action;
        button.MouseEntered += () => AnimateButton(button, HoverScale);
        button.MouseExited += () => AnimateButton(button, 1f);
    }

    private static void AnimateButton(Button button, float scale)
    {
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }

    private void OpenWorldMap()
    {
        _ = SceneLoader.ChangeSceneAsync(this, WorldMapPath);
    }

    private void OpenSettings()
    {
        _ = SceneLoader.ChangeSceneAsync(this, SettingsPath);
    }

    private void ShowCredits()
    {
        var credits = GetNode<Label>("CreditsPanel");
        credits.Visible = !credits.Visible;
    }
}
