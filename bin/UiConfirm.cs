using Godot;
using System.Threading.Tasks;

/// <summary>
/// 二次确认弹窗：问一句「确定吗」，等玩家点确定/取消。
///
/// 用 Godot 内建的 `ConfirmationDialog`（自带确定/取消与模态遮罩）。
/// 它是个 `Window`，**没有**「等结果」的 await 形式，所以用
/// <see cref="TaskCompletionSource{TResult}"/> 把三个结束信号收成一个 Task：
/// 确定 → `true`；取消 / 右上角关闭 / ESC → `false`。`TrySet` 保证先到的那个说了算。
///
/// 三个调用方共用这一处实现（规范 A）：暂停菜单的「认输 / 放弃」、
/// 主菜单「开始」时的覆盖确认。谁都不许再抄一份 `TaskCompletionSource` 那套。
/// </summary>
public static class UiConfirm
{
    public static async Task<bool> AskAsync(Node host, string text, string title = "确认")
    {
        if (host == null || !GodotObject.IsInstanceValid(host)) return false;

        var dialog = new ConfirmationDialog
        {
            DialogText = text,
            Title = title,
            OkButtonText = "确定",
            CancelButtonText = "取消",
            Unresizable = true,
            Exclusive = true,
        };
        host.AddChild(dialog);

        var result = new TaskCompletionSource<bool>();
        dialog.Confirmed += () => result.TrySetResult(true);
        dialog.Canceled += () => result.TrySetResult(false);
        dialog.CloseRequested += () => result.TrySetResult(false);

        dialog.PopupCentered();
        bool confirmed = await result.Task;

        if (GodotObject.IsInstanceValid(dialog)) dialog.QueueFree();
        return confirmed;
    }
}
