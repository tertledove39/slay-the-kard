using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「飞掠」特效：让**触发它的那张卡**飘起来 → 在空中轻微左右摆动 → 落回桌上。
///
/// 「漂浮」是靠三件事一起表现的：往上升、轻微放大、以及在漂浮期间把 ZIndex 抬到最高
/// 压住其他卡——高度是假的，遮挡关系才是让人一眼看出「它起来了」的关键。
///
/// 与 `bullet` / `smoke` 那种「在坐标上生成一个贴图」的特效不同，这个特效动的是
/// **已经存在的卡牌节点**，所以它需要 `Effect.Play` 的 `source` 参数；
/// 同时它自己**不画任何东西**（不生成贴图、不占父节点层级），
/// 场景里只挂一个 AudioStreamPlayer 用来放飞掠音效。
///
/// 参数全部 Export 在 `effects/flying_effect.tscn` 上，调手感不必改代码。
/// </summary>
public partial class FlyingEffect : Effect
{
    /// <summary>往上升多少像素。</summary>
    [Export] public float RiseHeight = 46f;

    /// <summary>漂浮期间放大到原来的多少倍（1.18 = 放大 18%）。</summary>
    [Export] public float RiseScale = 1.18f;

    /// <summary>左右摆动幅度（像素）。</summary>
    [Export] public float SwayAmplitude = 14f;

    /// <summary>摆动几个来回。</summary>
    [Export] public float SwayCycles = 2f;

    /// <summary>整段动画的时长（秒）。</summary>
    [Export] public float Duration = 0.9f;

    /// <summary>漂浮期间用的 ZIndex，要高于其他卡的 10 / 手牌的 20。</summary>
    [Export] public int TopZIndex = 200;

    /// <summary>飞掠音效所在的槽位名，对应 configs/music.ini 的 [sfx] 段。</summary>
    [Export] public string SfxSlot = "flyby";

    // 三段的时间占比：升起 35% → 摆动 30% → 落回 35%。
    // 这样改 Duration 一处就能整段变快变慢，不必逐个调。
    private const float RiseFraction = 0.35f;
    private const float SwayFraction = 0.30f;

    private AudioStreamPlayer sfxPlayer;

    public override void _Ready()
    {
        sfxPlayer = GetNodeOrNull<AudioStreamPlayer>("Sfx");
    }

    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                                    cardBase_ source = null, int count = 0)
    {
        // 动的是卡本身，没有卡就什么都做不了。
        if (source == null || !IsInstanceValid(source)) return;

        float duration = time.HasValue && time.Value > 0f ? time.Value : Duration;
        Vector2 basePosition = source.Position;
        Vector2 baseScale = source.Scale;
        int baseZIndex = source.ZIndex;

        // 漂浮期间要压住其他卡：让刷新显示顺序跳过它，否则那套「场上卡一律 ZIndex = 10」
        // 会在下一帧就把我们抬起来的层级打回去。
        source.isUnderCardEffect = true;
        source.ZIndex = TopZIndex;
        PlaySfx();

        try
        {
            await RiseAsync(source, basePosition, baseScale, duration * RiseFraction);
            await SwayAsync(source, basePosition, duration * SwayFraction);
            await LandAsync(source, basePosition, baseScale, duration * (1f - RiseFraction - SwayFraction));
        }
        finally
        {
            // 无论中途出什么事都要还原，否则这张卡会永远浮在空中、压在别人身上。
            if (IsInstanceValid(source))
            {
                source.Position = basePosition;
                source.Scale = baseScale;
                source.ZIndex = baseZIndex;
                source.isUnderCardEffect = false;
            }
        }
    }

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

    private async Task SwayAsync(cardBase_ card, Vector2 basePosition, float duration)
    {
        int halfSwings = Mathf.Max(1, Mathf.RoundToInt(SwayCycles * 2f));
        float step = duration / halfSwings;

        var tween = CreateTween();
        for (int index = 0; index < halfSwings; index++)
        {
            float targetX = basePosition.X + (index % 2 == 0 ? SwayAmplitude : -SwayAmplitude);
            tween.TweenProperty(card, "position:x", targetX, step)
                 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        // 最后一定要摆回中线，否则落点会偏掉一整个摆幅。
        tween.TweenProperty(card, "position:x", basePosition.X, step)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task LandAsync(cardBase_ card, Vector2 basePosition, Vector2 baseScale, float duration)
    {
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(card, "position", basePosition, duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenProperty(card, "scale", baseScale, duration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
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
