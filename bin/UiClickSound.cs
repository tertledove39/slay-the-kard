using Godot;

/// <summary>
/// **按键音**：按下按钮时响一声。世界地图界面的所有按钮与战斗界面的「下一回合」按钮走它。
///
/// 为什么单独开一个类，而不是各场景自己写一行 `MusicManager.Instance.PlaySfx("button")`：
/// **「哪些按钮要响」是 UI 的事，不是音频管理器的事**。槽位名、递归规则、防重复挂载
/// 这些细节收在这里一处，场景侧只剩一句 `UiClickSound.AttachAll(this)`。
///
/// 播放本身委托给 <see cref="MusicManager.PlaySfx"/>——它挂在 autoload 上，
/// 所以「按下按钮 → 立刻切场景」时声音不会被场景销毁掐断。
/// </summary>
public static class UiClickSound
{
    /// <summary>
    /// `[sfx]` 段的槽位名。换按键音只要改这一处（或直接改 `configs/music.ini` 里 `button` 的值）。
    /// </summary>
    private const string ClickSlot = "button";

    /// <summary>已挂过的记号，防止同一个按钮被挂两次（那就是两声）。</summary>
    private static readonly StringName AttachedFlag = "_ui_click_sound";

    public static void Play() => MusicManager.Instance?.PlaySfx(ClickSlot);

    /// <summary>给一个按钮挂上按键音。本来就已经挂过的会跳过。</summary>
    public static void Attach(BaseButton button)
    {
        if (button == null || button.HasMeta(AttachedFlag)) return;
        button.SetMeta(AttachedFlag, true);
        button.Pressed += Play;
    }

    /// <summary>
    /// 给 `root` 底下**所有**按钮挂上按键音（含多层子节点）。
    ///
    /// 用递归而不是逐个写节点名：世界地图的 7 个区域按钮是运行时按名字前缀找出来的，
    /// 写死名字的话，以后在场景里加一个按钮就会静默没声。
    /// </summary>
    public static void AttachAll(Node root)
    {
        if (root == null) return;

        foreach (Node node in root.FindChildren("*", "BaseButton", recursive: true, owned: false))
        {
            if (node is BaseButton button) Attach(button);
        }
    }
}
