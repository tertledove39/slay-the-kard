using Godot;
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// BUGS #70 的行为回归：**总部的阵亡判定不能依赖 `state`**。
///
/// 老板报的现象：「总部血 -4 却死不掉，而其它单位死亡完全正常」。
/// 三条事实（能继续操作 / 别的单位正常死 / 显示的就是内部真值）合起来只剩一个可能：
/// 总部的 `state` 不是 `placed`，于是 `IsDeadPlacedUnit` 永远不成立。
///
/// 三段：
///   甲 基线   ：状态正常时，总部血 -4 正常阵亡
///   乙 复现   ：把总部的 `state` 强行改成 `inHand`（模拟那条破口）→ **仍然必须死**
///   丙 别修过头：**普通单位** `state` 是 `inHand` 时血 ≤ 0 **不许**死
///              （手牌也在 `cardInPlaces` 里，松掉这条就会把手牌误杀）
///
/// 跑法：godot --headless --path . res://tests/hq_death_test.tscn
/// </summary>
public partial class HqDeathTest : Node
{
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    /// <summary>等战场就绪的帧数。</summary>
    private const int ReadyFrames = 150;

    /// <summary>打完血之后的观察窗口。</summary>
    private const int ObserveFrames = 60;

    private int _failed;

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        await BaselineAsync();
        await CorruptedStateAsync();
        await HandCardStaysAliveAsync();

        GD.Print(_failed == 0 ? "\nResult: all passed" : $"\nResult: {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    /// <summary>甲：一切正常时，总部血 -4 就该阵亡。</summary>
    private async Task BaselineAsync()
    {
        var field = await NewFieldAsync();
        var hq = HqOf(field);
        GD.Print($">>> 甲 基线：总部 state={hq.getState()}，打到 -4 应当阵亡");
        await KillAsync(field, hq);
        Check(hq.getState() == CardState.destroyed, $"甲 总部阵亡（实际 state={hq.getState()}）");
        Free(field);
    }

    /// <summary>
    /// 乙：**这一条就是老板的现象**。总部状态被改坏（这里直接模拟成 `inHand`），
    /// 修复前 `IsDeadPlacedUnit` 要求 `state == placed`，于是它永远不死。
    /// </summary>
    private async Task CorruptedStateAsync()
    {
        var field = await NewFieldAsync();
        var hq = HqOf(field);

        GD.Print(">>> 乙 复现：把总部的 state 改成 inHand，再打到 -4");
        hq.setState(CardState.inHand);
        Check(hq.getState() == CardState.inHand, "乙 状态已改坏（前提成立）");

        await KillAsync(field, hq);
        Check(hq.getState() == CardState.destroyed,
              $"乙 总部照样阵亡（实际 state={hq.getState()}）");
        Free(field);
    }

    /// <summary>
    /// 丙：**别修过头**。手牌卡也在 `cardInPlaces` 里，如果总部的宽松判据被推广到
    /// 所有卡，一张血被打到 0 的手牌就会被凭空杀掉。普通单位必须仍旧要求 `placed`。
    /// </summary>
    private async Task HandCardStaysAliveAsync()
    {
        var field = await NewFieldAsync();
        var type = field.GetType();
        var player1 = type.GetField("player1", Any).GetValue(field);
        var hand = (IList)player1.GetType().GetMethod("GetCardsInHand", Any).Invoke(player1, null);
        var card = (cardBase_)hand[0];
        var places = (IList)type.GetField("cardInPlaces", Any).GetValue(field);

        GD.Print($">>> 丙 别修过头：手牌 {card.id} state={card.getState()}，打到 0 不许死");
        Check(places.Contains(card), "丙 手牌确实在 cardInPlaces 里（所以这条测试有意义）");

        await card.LoseDefence(card.ReadDefence() + 4);
        await (Task)type.GetMethod("CheckIfAnyUnitDiedAsync", Any).Invoke(field, null);
        await FramesAsync(ObserveFrames);

        Check(card.getState() == CardState.inHand,
              $"丙 手牌没有被误杀（实际 state={card.getState()}）");
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

    private async Task KillAsync(battlefield_ field, cardBase_ hq)
    {
        await hq.LoseDefence(hq.ReadDefence() + 4);
        await (Task)field.GetType().GetMethod("CheckIfAnyUnitDiedAsync", Any).Invoke(field, null);
        await FramesAsync(ObserveFrames);
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
