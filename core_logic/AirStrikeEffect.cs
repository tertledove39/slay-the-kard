using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「飞起来打一下」的特效：飞掠 **+** 一个子特效。
///
/// 完整编排是——卡牌**刚开始起飞，子特效就一起打出去**，打完了原地悬停一下再落回桌面。
/// 这一段夹在飞掠的中间，而不是「飞完全程再打」，所以拼不出来：
/// `attackEffect = flying,bombing` 那种写法只能做到一个演完接一个。
/// 节奏必须由**一个**特效自己掌握，这就是本类存在的理由。
///
/// **一个脚本、两个场景**，靠 Export 出去的 `StrikeEffectName` 区分（与 `bullet` / `bombing`
/// 共用 `BulletEffect` 是同一个套路）：
/// - `effects/air_strike_effect.tscn` → `bombing`，起飞即**投弹**；
/// - `effects/strafe_effect.tscn` → `bullet`，起飞即**打枪**。
///
/// 实现方式是**继承 `FlyingEffect`**，只覆写 `DuringRiseAsync`（与起飞并行的那件事）。
/// 起飞、悬停、降落、落回后还原状态、飞掠音效全部沿用父类——「卡飘起来」这套运动
/// 因此只有一份实现（规范 A），几种空中攻击不会各自长歪。
///
/// 子特效本身也不在这里重写：弹数、飞行时长、错开间隔仍然配在各自那个场景里，
/// 本类只负责「什么时候开始打」。
/// </summary>
public partial class AirStrikeEffect : FlyingEffect
{
    /// <summary>
    /// 起飞的同时要打出去的**子特效名**（`EffectRegistry` 里的键）。
    /// 默认 `bombing` = 航弹；`strafe` 那个场景把它设成 `bullet`。
    /// **留空则退化成纯飞掠**，只飞不打。
    /// </summary>
    [Export] public string StrikeEffectName = "bombing";

    /// <summary>
    /// **子特效与起飞同时开始**。
    ///
    /// 一开始是等起飞演完（`StayAsync`）才打的，实机反馈「炮弹发射得太晚」——
    /// 起飞那一段有整整一秒，弹要比它先出去。现在挂在 `DuringRiseAsync` 上：
    /// 卡刚开始往上飘，弹就已经在飞了。
    ///
    /// 父类会把这一支和升起 `WhenAll` 等在一起，所以**起飞那段会自动延长到打完**，
    /// 打完才开始悬停、降落——不会出现「卡已经落回桌面、弹还在半路」。
    /// </summary>
    protected override Task DuringRiseAsync(cardBase_ card, IReadOnlyList<Vector2> positions, int count)
        => StrikeAsync(positions, count);

    /// <summary>
    /// 播一遍子特效。`count` 原样传下去——`bombing` 场景里弹数配的是 0，
    /// 意思是「用调用方给的数量」，也就是**攻击力**；`bullet` 配的是固定 10 发。
    /// </summary>
    private async Task StrikeAsync(IReadOnlyList<Vector2> positions, int count)
    {
        if (string.IsNullOrWhiteSpace(StrikeEffectName)) return;

        Effect strike = EffectRegistry.Create(StrikeEffectName);
        if (strike == null)
        {
            GD.PushWarning($"AirStrikeEffect: 子特效取不到，本次只飞不打: '{StrikeEffectName}'");
            return;
        }

        AddChild(strike);
        strike.PrepareForUse();
        try
        {
            await strike.Play(positions, null, null, count);
        }
        catch (Exception exception)
        {
            // 子特效出问题不该连累降落——父类那边还等着这一支返回。
            GD.PushWarning($"AirStrikeEffect: 子特效播放异常: {exception.Message}");
        }
        finally
        {
            if (IsInstanceValid(strike)) EffectRegistry.Release(strike);
        }
    }
}
