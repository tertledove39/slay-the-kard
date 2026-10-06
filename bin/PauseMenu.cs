using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 暂停菜单要执行的两个动作。菜单自己**不知道**这是战斗还是世界地图——
/// 文案与行为都由调用方给，所以同一个菜单能同时服务两边（规范 A：不写第二套）。
/// </summary>
public sealed class PauseAction
{
    /// <summary>按钮上的字，如「认输」「放弃」「保存并退出」。</summary>
    public string Text { get; init; }

    /// <summary>按下后干什么。要切场景就自己 await。</summary>
    public Func<Task> OnPressed { get; init; }

    /// <summary>按下去是不是要二次确认（「认输/放弃」这类不可逆的），确认文案写这里。</summary>
    public string ConfirmText { get; init; }
}

/// <summary>
/// 暂停菜单：**音量 + 两个可配置动作**。
///
/// 两个界面共用一个场景，差别只在调用方传进来的两个 <see cref="PauseAction"/>：
///
/// | 界面 | 动作一 | 动作二 |
/// |---|---|---|
/// | 战斗（`battlefield_`） | **认输**（玩家总部防御归零，走既有失败流程） | **保存并退出** |
/// | 世界地图（`WorldMap`） | **放弃**（删档 + 重置进度 + 回主菜单） | **保存并退出** |
///
/// 音量直接用 `SettingsManager` 的四个 `float` 项，改完**立即生效并落盘**，
/// 行构建与设置界面共用 `bin/SettingRow.cs`。
///
/// 打开方式由调用方决定（ESC 或左上角按钮），菜单只负责「显示、关掉、把结果告诉调用方」。
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    /// <summary>盖在战斗/世界地图之上，但低于对白气泡与卡组查看器（那两个是 Layer 2）。</summary>
    private const int OverlayLayer = 10;

    private const string ScenePath = "res://bin/pause_menu.tscn";
    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;

    private PauseAction _action1;
    private PauseAction _action2;
    private Action _onClosed;

    /// <summary>二次确认框开着。此时 ESC 归对话框管，菜单不能抢。</summary>
    private bool _confirming;

    /// <summary>当前有没有菜单开着。调用方据此忽略其它输入（战斗中尤其重要）。</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// 弹一个暂停菜单。`onClosed` 在菜单关掉（含按动作按钮之后）时调一次，
    /// 调用方用它解锁输入。已经开着就返回 null，不会叠第二个。
    /// </summary>
    public static PauseMenu Show(Node host, PauseAction action1, PauseAction action2, Action onClosed = null)
    {
        if (IsOpen) return null;
        if (host == null || !GodotObject.IsInstanceValid(host)) return null;

        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        if (scene?.Instantiate() is not PauseMenu menu)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} PauseMenu.cs: 场景加载失败 {ScenePath}");
            return null;
        }

        menu._action1 = action1;
        menu._action2 = action2;
        menu._onClosed = onClosed;
        host.AddChild(menu);
        IsOpen = true;
        return menu;
    }

    /// <summary>关掉菜单（动作按钮里、或外面按 ESC 时调）。</summary>
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _onClosed?.Invoke();
        _onClosed = null;
        QueueFree();
    }

    public override void _Ready()
    {
        Layer = OverlayLayer;

        BuildVolumeRows();

        var buttons = GetNode<VBoxContainer>("Panel/Margin/Rows/Actions");
        AddActionButton(buttons, _action1);
        AddActionButton(buttons, _action2);
        AddActionButton(buttons, new PauseAction { Text = "返回游戏", OnPressed = () => { Close(); return Task.CompletedTask; } });
    }

    public override void _Input(InputEvent @event)
    {
        if (_confirming) return;   // 确认框开着时 ESC 是「取消」，不是「关菜单」
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        if (key.Keycode == Key.Escape)
        {
            // 吃掉，别让底下那层也响应同一个 ESC。
            // 本类是 CanvasLayer 不是 Control，没有 `AcceptEvent()`，用 Viewport 那个。
            GetViewport()?.SetInputAsHandled();
            Close();
        }
    }

    private void BuildVolumeRows()
    {
        var list = GetNode<VBoxContainer>("Panel/Margin/Rows/VolumeRows");
        foreach (SettingItem item in SettingsManager.Items)
        {
            if (item.Type == "float") SettingRow.AddFloatSlider(list, item);
        }
    }

    private void AddActionButton(VBoxContainer list, PauseAction action)
    {
        if (action == null) return;

        var button = new Button { Text = action.Text, CustomMinimumSize = new Vector2(540, 52) };
        button.AddThemeFontSizeOverride("font_size", 24);
        button.Pressed += () => _ = RunActionAsync(action);
        button.MouseEntered += () => AnimateButton(button, HoverScale);
        button.MouseExited += () => AnimateButton(button, 1f);
        UiClickSound.Attach(button);
        list.AddChild(button);
    }

    private async Task RunActionAsync(PauseAction action)
    {
        // 「认输/放弃」这类不可逆动作要二次确认。确认框就是**这一个**菜单的一个状态，
        // 不再另开一层暂停菜单。
        if (!string.IsNullOrWhiteSpace(action.ConfirmText) && !await ConfirmAsync(action.ConfirmText)) return;

        // 先关菜单再跑动作：动作多半要切场景，而菜单是当前场景的子节点。
        Func<Task> after = action.OnPressed;
        Close();
        if (after != null) await after();
    }

    /// <summary>二次确认。实现在 <see cref="UiConfirm"/>，主菜单的覆盖确认也用它。</summary>
    private async Task<bool> ConfirmAsync(string text)
    {
        // `_confirming` 让菜单自己的 ESC 让位给确认框（那时 ESC 是「取消」）。
        _confirming = true;
        bool confirmed = await UiConfirm.AskAsync(this, text);
        _confirming = false;
        return confirmed;
    }

    private static void AnimateButton(Button button, float scale)
    {
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }
}
