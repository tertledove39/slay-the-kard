using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「空袭」特效：飞掠 **+** 投弹。
///
/// 轰炸机攻击时的完整编排是——卡牌**先飞起来**，在**盘旋的过程中把航弹扔下去**，
/// 投完了再落回桌面。投弹夹在飞掠的中间，而不是「飞完全程再投弹」，
/// 所以拼不出来：`attackEffect = flying,bombing` 那种写法只能做到一个演完接一个。
/// 节奏必须由**一个**特效自己掌握，这就是本类存在的理由。
///
/// 实现方式是**继承 `FlyingEffect`**，只覆写「停留」那一段
/// （`StayAsync`）。起飞、转向、盘旋摆动、降落、落回后还原状态、飞掠音效
/// 全部沿用父类——「卡飘起来」这套运动因此只有一份实现（规范 A），
/// 空袭和纯飞掠不会各自长歪。
///
/// 投弹本身也不在这里重写：它把 `effects/bombing_effect.tscn` 当**子特效**播一遍，
/// 弹数、飞行时长、错开间隔仍然配在那个场景里，本类只负责「什么时候开始投」。
/// </summary>
public partial class AirStrikeEffect : FlyingEffect
{
    /// <summary>
    /// 盘旋期间要播的子特效名（`EffectRegistry` 里的键）。
    /// 默认 `bombing` = 航弹。**留空则退化成纯飞掠**，只盘旋不投弹。
    /// </summary>
    [Export] public string StrikeEffectName = "bombing";

    /// <summary>
    /// 悬停 = **一边悬停一边投弹**。两件事同时开跑，谁后结束就等谁：
    /// 父类给的悬停时间一秒都不会少，投弹也不会把降落提前。
    ///
    /// 悬停比投弹短时，飞机会投完弹后继续悬停到时间结束——这是刻意的，
    /// 宁可多停一会儿，也不要让卡在航弹还在飞的时候就落回桌面。
    /// </summary>
    protected override async Task StayAsync(cardBase_ card, IReadOnlyList<Vector2> positions,
                                            float baseRotation, float stayDuration, int count)
    {
        Task hover = base.StayAsync(card, positions, baseRotation, stayDuration, count);
        Task strike = StrikeAsync(positions, count);
        await Task.WhenAll(hover, strike);
    }

    /// <summary>
    /// 播一遍投弹子特效。`count` 原样传下去——`bombing` 场景里弹数配的是 0，
    /// 意思是「用调用方给的数量」，也就是**攻击力**。
    /// </summary>
    private async Task StrikeAsync(IReadOnlyList<Vector2> positions, int count)
    {
        if (string.IsNullOrWhiteSpace(StrikeEffectName)) return;

        Effect strike = EffectRegistry.Create(StrikeEffectName);
        if (strike == null)
        {
            GD.PushWarning($"AirStrikeEffect: 投弹子特效取不到，本次只飞不投: '{StrikeEffectName}'");
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
            // 投弹出问题不该连累降落——父类那边还等着这一支返回。
            GD.PushWarning($"AirStrikeEffect: 投弹子特效播放异常: {exception.Message}");
        }
        finally
        {
            if (IsInstanceValid(strike)) EffectRegistry.Release(strike);
        }
    }
}
