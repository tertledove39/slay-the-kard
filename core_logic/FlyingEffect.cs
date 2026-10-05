using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「飞掠」特效：让**触发它的那张卡**飘起来 → 在空中朝目标方向轻微摆动 → 落回桌上。
///
/// 「漂浮」是靠三件事一起表现的：往上升、轻微放大、以及在漂浮期间把 ZIndex 抬到最高
/// 压住其他卡——高度是假的，遮挡关系才是让人一眼看出「它起来了」的关键。
///
/// 与 `bullet` / `smoke` 那种「在坐标上生成一个贴图」的特效不同，这个特效动的是
/// **已经存在的卡牌节点**，所以它需要 `Effect.Play` 的 `source` 参数；
/// 同时它自己**不画任何东西**（不生成贴图、不占父节点层级），
/// 场景里只挂一个 AudioStreamPlayer 用来放飞掠音效。
///
/// 三段时序：
/// 1. **升起与转向同时进行**——升到最高点时方向已经对准目标，不做「先升完再慢慢转」；
/// 2. 在空中朝着目标方向左右摆动（摆幅 `SwayDegrees`，速度按 `SwaySecondsPerCycle` 算）；
/// 3. 落回桌上，位置/缩放/**角度**一起还原。
///
/// 每段的时长都直接填秒数（不再按总时长的百分比切），因为「摆动 4 秒一个周期」
/// 这种要求用百分比表达不出来。参数全部 Export 在 `effects/flying_effect.tscn` 上。
/// </summary>
public partial class FlyingEffect : Effect
{
    /// <summary>往上升多少像素。</summary>
    [Export] public float RiseHeight = 46f;

    /// <summary>漂浮期间放大到原来的多少倍（1.18 = 放大 18%）。</summary>
    [Export] public float RiseScale = 1.18f;

    /// <summary>在「已经指向目标」的基础上，左右摆动的幅度（度）。</summary>
    [Export] public float SwayDegrees = 5f;

    /// <summary>摆动**一个来回**要几秒。</summary>
    [Export] public float SwaySecondsPerCycle = 4f;

    /// <summary>摆动几个来回。</summary>
    [Export] public float SwayCycles = 1f;

    /// <summary>升起（含同时进行的转向）用几秒。</summary>
    [Export] public float RiseDuration = 1.5f;

    /// <summary>落回桌上用几秒。</summary>
    [Export] public float LandDuration = 1.5f;

    /// <summary>漂浮期间用的 ZIndex，要高于其他卡的 10 / 手牌的 20。</summary>
    [Export] public int TopZIndex = 200;

    /// <summary>飞掠音效所在的槽位名，对应 configs/music.ini 的 [sfx] 段。</summary>
    [Export] public string SfxSlot = "flyby";

    private AudioStreamPlayer sfxPlayer;

    public override void _Ready()
    {
        sfxPlayer = GetNodeOrNull<AudioStreamPlayer>("Sfx");
    }

    /// <summary>三段加起来的总时长。`Play(time)` 给了值时按它整体缩放。</summary>
    private float TotalSeconds => RiseDuration + SwaySecondsPerCycle * SwayCycles + LandDuration;

    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                                    cardBase_ source = null, int count = 0)
    {
        // 动的是卡本身，没有卡就什么都做不了。
        if (source == null || !IsInstanceValid(source)) return;

        // 传了 time 就当作「整段总时长」，三段按比例一起缩放；不传就用场景里各段自己的秒数。
        float scale = 1f;
        if (time.HasValue && time.Value > 0f && TotalSeconds > 0f)
            scale = time.Value / TotalSeconds;

        Vector2 basePosition = source.Position;
        Vector2 baseScale = source.Scale;
        float baseRotation = source.Rotation;
        int baseZIndex = source.ZIndex;

        // 目标方向：positions[0] 是攻击者中心、positions[1] 是被攻击目标中心。
        float aimRotation = baseRotation + AimRotationOffset(positions);

        // 漂浮期间要压住其他卡：让刷新显示顺序跳过它，否则那套「场上卡一律 ZIndex = 10」
        // 会在下一帧就把我们抬起来的层级打回去。
        source.isUnderCardEffect = true;
        source.ZIndex = TopZIndex;
        PlaySfx();

        try
        {
            await RiseAndAimAsync(source, basePosition, baseScale, aimRotation, RiseDuration * scale);
            await SwayAroundAimAsync(source, aimRotation, SwaySecondsPerCycle * SwayCycles * scale);
            await LandAsync(source, basePosition, baseScale, baseRotation, LandDuration * scale);
        }
        finally
        {
            // 无论中途出什么事都要还原，否则这张卡会永远浮在空中、斜着、压在别人身上。
            if (IsInstanceValid(source))
            {
                source.Position = basePosition;
                source.Scale = baseScale;
                source.Rotation = baseRotation;
                source.ZIndex = baseZIndex;
                source.isUnderCardEffect = false;
            }
        }
    }

    /// <summary>
    /// 「指向目标」要转多少弧度——让卡的**上边缘**对准目标方向，
    /// 与 `Bullet` 的朝向约定一致（那边也是把「上」对准飞行方向）。
    /// 取不到目标坐标时返回 0，退化成「不转」而不是乱转一个角度。
    /// </summary>
    private static float AimRotationOffset(IReadOnlyList<Vector2> positions)
    {
        if (positions == null || positions.Count < 2) return 0f;

        Vector2 direction = positions[1] - positions[0];
        if (direction.LengthSquared() < 0.0001f) return 0f;

        return direction.Angle() + Mathf.Pi / 2f;
    }

    /// <summary>
    /// 升起 + 放大 + **转向目标**三件事同时进行，一起在 `duration` 内完成。
    /// 合在一起是刻意的：升到最高点时方向就该已经对准了，
    /// 不能出现「悬在空中还在慢慢转」的中间状态。
    /// </summary>
    private async Task RiseAndAimAsync(cardBase_ card, Vector2 basePosition, Vector2 baseScale,
                                       float aimRotation, float duration)
    {
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(card, "position", basePosition + new Vector2(0f, -RiseHeight), duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card, "scale", baseScale * RiseScale, duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card, "rotation", aimRotation, duration)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>
    /// 在**已经指向目标**的角度上左右摆动若干度。
    /// 一个来回的时长 = `duration / SwayCycles`（`duration` 已由调用方乘过周期数）。
    /// 最后一个来回停在瞄准角上，落回时角度才不会突然跳一下。
    /// </summary>
    private async Task SwayAroundAimAsync(cardBase_ card, float aimRotation, float duration)
    {
        int cycles = Mathf.Max(1, Mathf.RoundToInt(SwayCycles));
        int halfSwings = cycles * 2;
        float step = duration / halfSwings;
        float offset = Mathf.DegToRad(SwayDegrees);

        var tween = CreateTween();
        for (int index = 0; index < halfSwings; index++)
        {
            float target = aimRotation + (index % 2 == 0 ? offset : -offset);
            tween.TweenProperty(card, "rotation", target, step)
                 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        tween.TweenProperty(card, "rotation", aimRotation, step)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>落回桌上：位置、缩放、角度一起还原（角度从瞄准角转回原位）。</summary>
    private async Task LandAsync(cardBase_ card, Vector2 basePosition, Vector2 baseScale,
                                 float baseRotation, float duration)
    {
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(card, "position", basePosition, duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenProperty(card, "scale", baseScale, duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenProperty(card, "rotation", baseRotation, duration)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>
    /// 放飞掠音效。取不到（槽位没配 / 加载失败）就静默跳过——
    /// 少一声音不该让整段动画停下来。
    /// </summary>
    private void PlaySfx()
    {
        if (sfxPlayer == null) return;

        AudioStream stream = MusicManager.Instance?.PickSfx(SfxSlot);
        if (stream == null) return;

        sfxPlayer.Stream = stream;
        sfxPlayer.Play();
    }
}
