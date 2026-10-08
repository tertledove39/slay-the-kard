using Godot;
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// BUGS #69 的行为回归：**「await 永不返回」不能把死亡检查的闸门永久关死**。
///
/// 这是 `tests/verify_death_gate_hang.py`（源码白盒）之外的**真跑**部分——
/// 那条只保证「代码长成该有的样子」，这条保证「运行时真的是那个行为」。
///
/// 三段：
///   甲 基线      ：总部血 -4 → 正常阵亡（别把没坏的东西修坏）
///   乙 挂死复现  ：卡在移动动画途中被摘出场景树 → 方法必须收束、闸门必须回到 0、总部照样死
///   丙 看门狗边界：闸门关 1 秒不强制开（不能误伤正常暂停）；关 11 秒强制开并让总部死
///
/// 跑法：godot --headless --path . res://tests/death_gate_hang_test.tscn
/// </summary>
public partial class DeathGateHangTest : Node
{
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    /// <summary>等场景就绪的帧数。够总部落地、起手流程起来。</summary>
    private const int ReadyFrames = 150;

    /// <summary>判定「方法挂死了没」的观察窗口，必须显著长于一次移动动画。</summary>
    private const int ObserveFrames = 200;

    /// <summary>伪造「闸门已经关了这么久」——1 秒，不足以触发看门狗。</summary>
    private const int FakeHeldShortMsec = 1000;

    /// <summary>伪造的长时间——11 秒，必须超过 DeathCheckGateStuckSeconds。</summary>
    private const int FakeHeldLongMsec = 11000;

    private int _failed;

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        await BaselineAsync();
        await HangAsync();
        await WatchdogBoundaryAsync();

        GD.Print(_failed == 0 ? "\nResult: all passed" : $"\nResult: {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    /// <summary>甲：没有任何异常情况时，总部血 -4 就该阵亡。</summary>
    private async Task BaselineAsync()
    {
        var field = await NewFieldAsync();
        var hq = HqOf(field);

        GD.Print(">>> 甲 基线：总部血 -4，应当阵亡");
        await KillHqAsync(field, hq);
        Check(hq.getState() == CardState.destroyed, $"甲 总部阵亡（实际 state={hq.getState()}）");
        Free(field);
    }

    /// <summary>
    /// 乙：**这一条就是老板看到的现象**。移动动画途中把卡摘出场景树，
    /// 修复前 `MoveToPosition` 会挂在 `ToSignal(null, …)` 上永不返回，
    /// 它上游的 `using` 闸门跟着永不恢复，于是总部 -4 也不死。
    /// </summary>
    private async Task HangAsync()
    {
        var field = await NewFieldAsync();
        var hq = HqOf(field);
        var gate = field.GetType().GetMethod("ReadDeathCheckState", Any);

        var player1 = field.GetType().GetField("player1", Any).GetValue(field);
        var hand = (IList)player1.GetType().GetMethod("GetCardsInHand", Any).Invoke(player1, null);
        var places = (IList)field.GetType().GetField("enemySupprotLine", Any).GetValue(field);
        var victim = (cardBase_)hand[0];
        var freePlace = (place_)places[0];

        GD.Print($">>> 乙 挂死复现：{victim.id} 移动动画途中被摘出场景树");

        var task = (Task)field.GetType().GetMethod("AddCardToPlace", Any)
            .Invoke(field, new object[] { victim, freePlace });
        await FramesAsync(5);

        // 与 ResourceManager.ReleaseEmptyCard 的第一步完全一致
        victim.GetParent().RemoveChild(victim);
        Check(!victim.IsInsideTree(), "乙 卡已离开场景树（前提成立）");

        await FramesAsync(ObserveFrames);
        Check(task.IsCompleted, "乙 AddCardToPlace 已收束（修复前永不完成）");
        Check((int)gate.Invoke(field, null) == 0, "乙 闸门已恢复为 0（修复前停在 1）");

        await KillHqAsync(field, hq);
        Check(hq.getState() == CardState.destroyed, $"乙 总部仍然阵亡（实际 state={hq.getState()}）");
        Free(field);
    }

    /// <summary>
    /// 丙：看门狗的两条边界。**只关 1 秒不能开闸**——否则正常的一次入场/攻击动画
    /// 就会被它打断；**关满 11 秒必须开闸**——那是救「打不死的总部」的最后一道。
    /// </summary>
    private async Task WatchdogBoundaryAsync()
    {
        var field = await NewFieldAsync();
        var hq = HqOf(field);
        var type = field.GetType();
        var gate = type.GetMethod("ReadDeathCheckState", Any);
        var pausedSince = type.GetField("_deathCheckPausedSinceMsec", Any);
        var pauseField = type.GetField("pauseDeathCheck", Any);

        GD.Print(">>> 丙 看门狗边界");
        await KillHqAsync(field, hq, runDeathCheck: false);

        pauseField.SetValue(field, 1);
        pausedSince.SetValue(field, Time.GetTicksMsec() - FakeHeldShortMsec);
        await (Task)type.GetMethod("CheckIfAnyUnitDiedAsync", Any).Invoke(field, null);
        Check((int)gate.Invoke(field, null) == 1, "丙 只关 1 秒不强制开闸（正常暂停不受影响）");
        Check(hq.getState() == CardState.placed, "丙 此期间总部保持存活（暂停语义没坏）");

        pausedSince.SetValue(field, Time.GetTicksMsec() - FakeHeldLongMsec);
        await (Task)type.GetMethod("CheckIfAnyUnitDiedAsync", Any).Invoke(field, null);
        Check((int)gate.Invoke(field, null) == 0, "丙 关满 11 秒强制开闸");
        Check(hq.getState() == CardState.destroyed, $"丙 开闸后总部立刻阵亡（实际 state={hq.getState()}）");
        Free(field);
    }

    // ============================ 工具 ============================

    private async Task<battlefield_> NewFieldAsync()
    {
        BattleStateManager.IsCampaignMode = false;
        var field = ResourceLoader.Load<PackedScene>("res://bin/battleField.tscn").Instantiate() as battlefield_;
        AddChild(field);
        await FramesAsync(ReadyFrames);
        return field;
    }

    private static cardBase_ HqOf(battlefield_ field)
        => (cardBase_)field.GetType().GetField("myHq", Any).GetValue(field);

    /// <summary>把友方总部打到 -4，再按需跑一次死亡检查。</summary>
    private async Task KillHqAsync(battlefield_ field, cardBase_ hq, bool runDeathCheck = true)
    {
        await hq.LoseDefence(hq.ReadDefence() + 4);
        if (runDeathCheck)
            await (Task)field.GetType().GetMethod("CheckIfAnyUnitDiedAsync", Any).Invoke(field, null);
    }

    private async Task FramesAsync(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Free(battlefield_ field)
    {
        RemoveChild(field);
        field.QueueFree();
    }

    private void Check(bool ok, string message)
    {
        if (!ok) _failed++;
        GD.Print($"[{(ok ? "PASS" : "FAIL")}] {message}");
    }
}
