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
/// 1. **升起**——往上移动 + 轻微放大（`RiseDuration`），**不动角度**；
/// 2. **悬停**——原地停留 `SwaySecondsPerCycle × SwayCycles` 秒。默认**不转任何角度**；
///    想让它在悬停时左右摆动，把 `SwayDegrees` 调大即可（摆的是**卡牌自己的原始角度**）；
/// 3. **落回**——位置 / 缩放 / 角度一起还原（`LandDuration`）。
///
/// **本特效不负责转向**。早先版本会让卡边升边「指向被攻击的目标」，已移除：
/// 卡在整段动画里保持进入前的角度。
///
/// 每段的时长都直接填秒数（不再按总时长的百分比切），因为「悬停 1 秒」
/// 这种要求用百分比表达不出来。参数全部 Export 在 `effects/flying_effect.tscn` 上。
///
/// 中间那段（`StayAsync`）是 `virtual` 的，子类可以在悬停期间捎带做别的事——
/// `AirStrikeEffect` 就是靠覆写它做到「边悬停边投弹」的，见那个类的说明。
/// </summary>
public partial class FlyingEffect : Effect
{
    /// <summary>往上升多少像素。</summary>
    [Export] public float RiseHeight = 20f;

    /// <summary>漂浮期间放大到原来的多少倍（1.05 = 放大 5%）。</summary>
    [Export] public float RiseScale = 1.05f;

    /// <summary>
    /// 悬停期间左右摆动的幅度（度）。摆的是**卡牌自己的原始角度**，不是「指向目标」的角度。
    /// **默认 0 = 完全不转，静止悬停**；想让它在空中轻微晃，把这里调大即可。
    /// </summary>
    [Export] public float SwayDegrees = 0f;

    /// <summary>
    /// 摆动**一个来回**要几秒。`SwayDegrees = 0`（静止悬停）时它实际决定的是**悬停时长**：
    /// 总停留时间 = 本值 × `SwayCycles`。
    /// </summary>
    [Export] public float SwaySecondsPerCycle = 2f;

    /// <summary>摆动几个来回。静止悬停（`SwayDegrees = 0`）时它只影响总停留时长。</summary>
    [Export] public float SwayCycles = 1f;

    /// <summary>升起用几秒（只移动位置与缩放，不转角度）。</summary>
    [Export] public float RiseDuration = 1f;

    /// <summary>落回桌上用几秒。</summary>
    [Export] public float LandDuration = 1.5f;

    /// <summary>
    /// 漂浮期间用的 ZIndex，要高于场上其他卡的 10。
    /// （手牌是 20，但悬停的是场上卡、不会跟手牌在屏幕上重叠，所以不必比 20 高。）
    /// </summary>
    [Export] public int TopZIndex = 15;

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

        // 漂浮期间要压住其他卡：让刷新显示顺序跳过它，否则那套「场上卡一律 ZIndex = 10」
        // 会在下一帧就把我们抬起来的层级打回去。
        source.isUnderCardEffect = true;
        source.ZIndex = TopZIndex;
        PlaySfx();

        try
        {
            await RiseAsync(source, basePosition, baseScale, RiseDuration * scale);
            await StayAsync(source, positions, baseRotation, SwaySecondsPerCycle * SwayCycles * scale, count);
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
    /// **升起**：往上移动 + 轻微放大，两件事在 `duration` 内同时完成。
    ///
    /// **不碰角度**——本特效不负责转向。早先版本会让卡边升边「指向被攻击的目标」，
    /// 现在卡在整个动画里保持进入前的角度。
    /// </summary>
    private async Task RiseAsync(cardBase_ card, Vector2 basePosition, Vector2 baseScale, float duration)
    {
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(card, "position", basePosition + new Vector2(0f, -RiseHeight), duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card, "scale", baseScale * RiseScale, duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>
    /// **悬停**阶段——升到最高点之后、落回桌面之前的那段时间，也是子类唯一需要改的一段。
    ///
    /// 默认实现：`SwayDegrees` 为 0（默认）时就是**原地静止悬停**，只等够时间；
    /// 调大之后才左右摆动，摆的是**卡牌自己的原始角度**，不是「指向目标」的角度。
    ///
    /// 子类可以覆写它在悬停期间捎带做别的事：`AirStrikeEffect` 就是
    /// 「一边悬停一边投弹」，而**不需要**复制任何一段运动代码。
    ///
    /// 三段（升起 / 悬停 / 落回）之所以在这里切出来，就是因为「悬停时还能干什么」
    /// 是会变的，而「怎么升起来、怎么落回去」不会变。
    /// </summary>
    /// <param name="baseRotation">卡牌进入特效前的角度。悬停期间不改变它。</param>
    /// <param name="stayDuration">悬停总时长（秒），已按 `Play(time)` 缩放。</param>
    /// <param name="count">调用方给的数量（攻击力）。默认实现用不上，留给子类。</param>
    protected virtual async Task StayAsync(cardBase_ card, IReadOnlyList<Vector2> positions,
                                           float baseRotation, float stayDuration, int count)
    {
        if (stayDuration <= 0f) return;

        if (Mathf.IsZeroApprox(SwayDegrees))
        {
            // 静止悬停：没必要建一串「原地不动」的 tween，等够时间就行。
            await ToSignal(GetTree().CreateTimer(stayDuration), SceneTreeTimer.SignalName.Timeout);
            return;
        }

        await SwayAsync(card, baseRotation, stayDuration);
    }

    /// <summary>
    /// 在**卡牌自己的角度**上左右摆动若干度（`SwayDegrees` 为 0 时不会被调用）。
    /// 一个来回的时长 = `duration / SwayCycles`（`duration` 已由调用方乘过周期数）。
    /// 最后一个来回停在原角度上，落回时角度才不会突然跳一下。
    /// </summary>
    private async Task SwayAsync(cardBase_ card, float baseRotation, float duration)
    {
        int cycles = Mathf.Max(1, Mathf.RoundToInt(SwayCycles));
        int halfSwings = cycles * 2;
        float step = duration / halfSwings;
        float offset = Mathf.DegToRad(SwayDegrees);

        var tween = CreateTween();
        for (int index = 0; index < halfSwings; index++)
        {
            float target = baseRotation + (index % 2 == 0 ? offset : -offset);
            tween.TweenProperty(card, "rotation", target, step)
                 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        tween.TweenProperty(card, "rotation", baseRotation, step)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>
    /// 落回桌上：位置、缩放、角度一起还原。
    /// 角度这条在静止悬停时是空转（本来就没转过），留着是为了摆动被中途打断时不会斜着停在场上。
    /// </summary>
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
