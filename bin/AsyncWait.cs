using Godot;
using System.Threading.Tasks;

/// <summary>
/// 「等一帧」的唯一实现。
///
/// 直接写 `await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame)` 有一个
/// **致命形态**：节点一旦被摘出场景树，`GetTree()` 返回 **null**，而
/// `ToSignal(null, …)` 的 awaiter **永远不会完成** —— 挂在那句 await 上的协程
/// 就此永不返回。日志里看到的是这两行：
///
///     ERROR: Parameter "data.tree" is null.
///     ERROR: Parameter "p_source" is null.
///
/// 谁会被摘出场景树？`ResourceManager.ReleaseEmptyCard()` 的第一步就是
/// `GetParent().RemoveChild(card)`，而 `cardBase_.Dead()` → `RemoveCard` 会走到它；
/// 场景切换时整棵树离开，同理。
///
/// **后果不止「少播一段动画」**：调用方里若有 `using var guard = xxxScoped();`
/// 这类作用域清理，**它永远不会 Dispose** —— `using` 只能覆盖「返回」和「抛异常」，
/// 覆盖不了「挂死」。死亡检查的闸门就是这样被永久关死的：
/// `MoveToPosition` 挂在 `AddCardToPlace` / `Move` / `Attack` 中间，
/// 那三个方法各持一个 `DeathCheckGuard`，闸门停在 1，之后
/// `CheckIfAnyUnitDiedAsync()` 第一行就 return —— 表现就是「总部血 -4 却不死」。
///
/// 所以把判据收到这一处：**不在场景树上就不等**，立刻返回。
/// </summary>
public static class AsyncWait
{
    /// <summary>默认等待的「一帧」信号名。</summary>
    public const string ProcessFrame = "process_frame";

    /// <summary>物理帧信号名。</summary>
    public const string PhysicsFrame = "physics_frame";

    /// <summary>
    /// 等一帧。节点已释放、或已不在场景树上时**立即返回**，绝不挂死。
    ///
    /// 判据顺序不能颠倒：`IsInsideTree()` 走原生指针，对已释放对象会抛
    /// `ObjectDisposedException`，必须让 `IsInstanceValid` 先短路
    /// （见 docs/NOTICE.md「跨 await 持有的引用必须用 IsInstanceValid 重新确认」）。
    /// </summary>
    public static async Task WaitFrameAsync(Node node, string signalName = ProcessFrame)
    {
        if (node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) return;

        SceneTree tree = node.GetTree();
        if (tree == null) return;

        await node.ToSignal(tree, signalName);
    }
}
