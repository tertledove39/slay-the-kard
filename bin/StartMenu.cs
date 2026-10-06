using System.Threading.Tasks;
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

    /// <summary>
    /// **开始**：新开一局。已经有存档就先问要不要覆盖——覆盖是不可逆的，
    /// 直接开新局会把上一局的进度悄悄冲掉。
    /// 答「否」就退回主菜单（也就是什么都不做，关掉确认框即可）。
    /// </summary>
    public async void _on_start_pressed()
    {
        if (SaveManager.HasSave())
        {
            bool overwrite = await UiConfirm.AskAsync(this, "已有一个存档，开始新游戏会覆盖它。确定吗？");
            if (!overwrite) return;      // 否 → 留在主菜单
            SaveManager.Delete();
        }

        BattleStateManager.ResetCampaignProgress();
        await SceneLoader.ChangeSceneAsync(this, WorldMapPath);
    }

    /// <summary>
    /// **继续**：有档就按存档里记的处境直接回去——战斗中存的直接回到那一场，
    /// 世界地图存的就回世界地图（见 `SaveManager.ResolveScenePath`）。
    /// 没有可读的档时按钮是灰的，走不到这里。
    /// </summary>
    public async void _on_continue_pressed()
    {
        if (!SaveManager.Load())
        {
            RefreshContinueButton();
            return;
        }
        await SceneLoader.ChangeSceneAsync(this, SaveManager.ResolveScenePath());
    }

    /// <summary>没有可读的档就把「继续」灰掉——比点下去没反应清楚。</summary>
    private void RefreshContinueButton()
    {
        var button = GetNodeOrNull<Button>("Menu/Continue");
        if (button != null) button.Disabled = !SaveManager.HasSave();
    }

    


    public override void _Ready()
    {
        SettingsManager.Initialize();
        MusicManager.Instance?.PlaySlot("start_menu");

        //准备悬浮
        ConnectHover("Menu/Continue");
        ConnectHover("Menu/Start");
        ConnectHover("Menu/Settings");
        ConnectHover("Menu/Credits");

        // 主菜单四个按钮都响按键音
        UiClickSound.AttachAll(this);

        // 没有存档就把「继续」灰掉
        RefreshContinueButton();

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
