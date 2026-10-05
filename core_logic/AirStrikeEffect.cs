using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「空袭」特效：飞掠 **+** 投弹。
///
/// 轰炸机攻击时的完整编排是——卡牌**刚开始起飞，航弹就一起扔出去**，
/// 投完了原地悬停一下再落回桌面。投弹夹在飞掠的中间，而不是「飞完全程再投弹」，
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
    /// **投弹与起飞同时开始**。
    ///
    /// 一开始是等起飞演完（`StayAsync`）才投，实机反馈「炮弹发射得太晚」——
    /// 起飞那一段有整整一秒，航弹要比它先出去。现在挂在 `DuringRiseAsync` 上：
    /// 卡刚开始往上飘，航弹就已经在飞了。
    ///
    /// 父类会把这一支和升起 `WhenAll` 等在一起，所以**起飞那段会自动延长到投弹结束**，
    /// 投完弹才开始悬停、降落——不会出现「卡已经落回桌面、航弹还在半路」。
    /// </summary>
    protected override Task DuringRiseAsync(cardBase_ card, IReadOnlyList<Vector2> positions, int count)
        => StrikeAsync(positions, count);

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
