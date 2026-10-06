using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// **转换**：把一张场上的卡原地变成另一个单位。
///
/// 完整演出（老板给的节奏）：
///
///     1. 轻微浮起        —— 与战斗机起飞同一套，由父类 `FlyingEffect` 负责
///     2. 翻面            —— 翻到一半（侧对屏幕）时**露出卡背**
///     3. 再翻面          —— 翻到一半时**换成新单位**、收起卡背；落回来时已经是新卡
///
/// 本类**只覆写悬停那一段**（`StayAsync`）——浮起、落回、还原 Scale/Rotation/ZIndex、
/// `isUnderCardEffect` 标记、`_displayOrderDirty` 标脏全部沿用父类。
/// 与 `AirStrikeEffect` 覆写 `DuringRiseAsync` 是同一个套路：飞掠那套运动只有一份实现。
///
/// **翻面不用 shader**：2D 卡牌绕竖轴翻转的标准做法就是把 `Scale.X` 走 1 → 0 → 1，
/// `Scale.X = 0` 那一刻正好是「侧对屏幕」。压扁到 0 时换掉正面内容，就成了翻面。
/// shader 版能看到透视，但要引 GLSL、还可能因为编译失败而**静默什么都不发生**；
/// 这个需求用不着。翻面期间只动 X，Y 保持飞掠抬起后的值。
///
/// **新单位的 id 走特效名的参数**：`convert(panzer4)` → `Configure("panzer4")`
/// （与 `sfx(严冬)`、`TankAttack(artillery_large_impact)` 同一套）。
/// </summary>
public partial class ConvertEffect : FlyingEffect
{
    /// <summary>每一次**半翻**（1→0 或 0→1）要几秒。一整个翻面 = 两次半翻。</summary>
    [Export] public float HalfFlipSeconds = 0.18f;

    /// <summary>露出卡背之后停多久（这段是给玩家看清「翻过去了」的）。</summary>
    [Export] public float BackHoldSeconds = 0.35f;

    /// <summary>要变成的卡 id，来自特效名的参数。空 = 只演动画、不换卡。</summary>
    private string newCardId;

    public override void Configure(string argument) => newCardId = argument;

    /// <summary>
    /// 悬停阶段 = 两次翻面。父类默认在这一段做「原地静止悬停 / 左右摆」，这里整个换成翻面，
    /// 所以**不调 base.StayAsync**——摆幅对转换没有意义。
    /// </summary>
    protected override async Task StayAsync(cardBase_ card, IReadOnlyList<Vector2> positions,
                                            float baseRotation, float stayDuration, int count)
    {
        if (card == null || !IsInstanceValid(card)) return;

        // 抬起后父类把卡放大到了 RiseScale，这里以「此刻的 X」为准，翻面只动 X。
        float risenX = card.Scale.X;

        // ① 翻面 → 压扁那一刻盖上卡背
        await HalfFlipAsync(card, risenX, toZero: true, onEdge: () => card.SetConvertBackVisible(true));
        await HalfFlipAsync(card, risenX, toZero: false, onEdge: null);

        await ToSignal(GetTree().CreateTimer(BackHoldSeconds), SceneTreeTimer.SignalName.Timeout);

        // ② 再翻面 → 压扁那一刻换成新单位并收起卡背
        await HalfFlipAsync(card, risenX, toZero: true, onEdge: () => ApplyNewUnit(card));
        await HalfFlipAsync(card, risenX, toZero: false, onEdge: null);

        // 兜底：万一上面哪一步没走到（比如换卡失败提前 return），别把卡背留在场上
        card.SetConvertBackVisible(false);
    }

    /// <summary>
    /// 半次翻面：把 `Scale.X` 在 `risenX` 与 0 之间走一趟。
    ///
    /// `onEdge` 在**压扁到 0 的那一刻**（侧对屏幕、玩家看不见的那一帧）调用——
    /// 换卡面、盖/收卡背都只能在这个瞬间做，否则会穿帮。
    /// `tween.TweenMethod` 是逐帧回调，所以压扁是真的连续过程而不是瞬间跳变。
    /// </summary>
    private async Task HalfFlipAsync(cardBase_ card, float risenX, bool toZero, Action onEdge)
    {
        float from = toZero ? risenX : 0f;
        float to = toZero ? 0f : risenX;

        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(x => card.Scale = new Vector2(x, card.Scale.Y)),
                          from, to, HalfFlipSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);

        onEdge?.Invoke();
    }

    /// <summary>
    /// **把这张卡换成新单位**——在第二次翻面压扁到 0 的那一瞬间调用。
    ///
    /// 换的是**同一张卡**：同一个节点、同一个格子、同一方，只是数值/图标/名字变了。
    /// `SetCardInformation` 会把攻防费、效果、特性、图标、稀有度全刷成新卡的，
    /// 并把生命周期状态（烟幕/冲击/动员/伏击、`shouldBeRemoved`、`isDiscarding`）一起初始化
    /// ——那些状态属于旧卡，本就不该跟着过来。
    ///
    /// **行动次数不刷新**：转换不是「部署」，不该白送一次攻击。卡上还剩几次就还是几次。
    ///
    /// 失败（id 不存在 / 目标是总部）时只报警，卡保持原样——翻面动画照常演完，
    /// 玩家看到的是「翻回来还是原来那张」，比中途崩掉好。
    /// </summary>
    private void ApplyNewUnit(cardBase_ card)
    {
        card.SetConvertBackVisible(false);

        if (string.IsNullOrWhiteSpace(newCardId))
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} ConvertEffect.cs: 没给新卡 id，本次只演动画");
            return;
        }

        CardData data = BattleStateManager.GetCachedCard(newCardId);
        if (data == null)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} ConvertEffect.cs: 卡 id 不存在 '{newCardId}'，转换取消");
            return;
        }

        // 总部不能当转换结果：总部在场上只有一个、胜负判定挂在它身上，
        // 凭空多出一个（或把单位变成总部）会让死亡检查出怪事。
        if (data.IsHq == HQ.hq)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} ConvertEffect.cs: '{newCardId}' 是总部，不允许转换");
            return;
        }

        string from = card.id;
        card.SetCardInformation(data);
        GD.Print($"[Convert] {from} → {newCardId}");
    }
}
