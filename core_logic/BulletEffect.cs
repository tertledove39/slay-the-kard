using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「从 A 点向 B 点打出一串弹体」的特效。
///
/// 一个脚本、三个场景，靠 Export 出来的几个值区分：
/// - `effects/bullet_effect.tscn` → 子弹，固定 10 发，0.3 秒飞完全程；
/// - `effects/bombing_effect.tscn` → 航弹，弹数填 0 表示**用调用方给的数量**
///   （即攻击力，见 `Effect.Play` 的 `count`），飞行时长远长于子弹；
/// - `effects/tank_attack_effect.tscn` → 坦克炮弹，固定 1 发、0.6 秒，
///   并且**飞抵目标时响一声命中音**。
///
/// 之所以不做成三个类：生成、随机错开、回池这一整套逻辑完全一样，
/// 分开写就是三份会各自长歪的实现。
///
/// **命中音是可选能力**：槽位由**特效名的参数**给出（`TankAttack(artillery_large_impact)`），
/// 不传就不响——所以 `bullet` / `bombing` 照旧完全无声。
///
/// （历史上这里曾有一套写死的命中音，航弹连发时每发各炸一声会糊成一片，被整段删掉了。
/// 现在它回来了，但**默认关闭、由调用方按需开启**——问题从来不是这个能力，而是默认值。）
/// </summary>
public partial class BulletEffect : Effect
{
    /// <summary>弹体场景。航弹那个场景把它设成 res://bin/bomb.tscn。</summary>
    [Export] public string ProjectileScenePath = "res://bin/bullet.tscn";

    /// <summary>打出几发。**0 表示用调用方经 Play(count) 传进来的数量**。</summary>
    [Export] public int ProjectileCount = 10;

    /// <summary>相邻两发之间最多错开多少毫秒（实际取 0~本值之间的随机数）。</summary>
    [Export] public int StaggerMaxMs = 100;

    /// <summary>
    /// 每发弹体飞完全程要几秒。航弹要「显著慢」才有投弹的感觉，所以这是 Export 而不是
    /// 写死在 `Bullet` 里（那边只是默认值）。调用方若通过 `Play(time)` 传了时长，以它为准。
    /// </summary>
    [Export] public float ProjectileFlightSeconds = 0.3f;

    /// <summary>
    /// **发射位置**（`positions[0]`）冒的烟，写 `EffectRegistry` 里的特效名（可以带参数）。
    /// **留空 = 不冒**——`bullet` / `bombing` 都没填，所以它们两端一直是干净的。
    ///
    /// `tank_attack_effect.tscn` 填的是 `smoke_small`（炮口那一小股白烟）。
    /// </summary>
    [Export] public string MuzzleEffect = "";

    /// <summary>
    /// **落点**（`positions[^1]`，即最后一个目标）冒的烟，格式同 `MuzzleEffect`。
    ///
    /// 它等**全部弹体都飞抵之后**才冒一次——那一刻只有这里知道（`await bullet.Play(...)`
    /// 正是在弹体抵达时返回的），所以挂在 `PlayAndReleaseBullet` 之后的收尾处，
    /// 而不是每发各冒一个（`bombing` 有攻击力那么多发，逐个冒会糊成一片）。
    /// </summary>
    [Export] public string ImpactEffect = "";

    /// <summary>命中音的音量（线性，1 = 原音量）。没有命中音槽位时不起作用。</summary>
    [Export] public float ImpactSfxVolume = 1f;

    /// <summary>
    /// 命中音的声部数。多发弹体是先后命中的，只用一个播放器会让后一声掐掉前一声。
    /// 没有命中音槽位时不起作用。
    /// </summary>
    [Export] public int ImpactVoiceCount = 4;

    /// <summary>音效播放器所在的音频总线（与战场里既有的战斗音效一致）。</summary>
    private const string SfxBus = "SFX";

    /// <summary>命中音槽位，来自**特效名的参数**；为空则整段命中音都不存在。</summary>
    private string impactSfxSlot;

    private AudioStreamPlayer[] impactVoices;
    private int nextImpactVoice;

    /// <summary>
    /// 收下命中音槽位（`TankAttack(artillery_large_impact)` 里的那一段）。
    ///
    /// 为什么不放场景 Export：**口径取决于攻击者**（火炮按身材分三档，坦克不分档），
    /// 而特效自己不知道攻击者是谁——由调用方算好了当参数传进来最简单。
    ///
    /// 声部在这里**懒建**：`Configure` 是在 `AddChild` 之后调的，那时 `_Ready` 已经跑过了。
    /// </summary>
    public override void Configure(string argument)
    {
        impactSfxSlot = argument;
        if (string.IsNullOrWhiteSpace(impactSfxSlot)) return;

        int voices = Mathf.Max(1, ImpactVoiceCount);
        float volumeDb = Mathf.LinearToDb(Mathf.Clamp(ImpactSfxVolume, 0.0001f, 1f));
        impactVoices = new AudioStreamPlayer[voices];
        for (int index = 0; index < voices; index++)
        {
            var voice = new AudioStreamPlayer { Bus = SfxBus, VolumeDb = volumeDb };
            AddChild(voice);
            impactVoices[index] = voice;
        }
    }

    /// <summary>一发命中：轮换一个空闲声部放音效，让连续命中能叠着响。</summary>
    private void PlayImpactSfx()
    {
        if (impactVoices == null || impactVoices.Length == 0) return;

        AudioStream stream = MusicManager.Instance?.PickSfx(impactSfxSlot);
        if (stream == null)
        {
            GD.PushWarning($"BulletEffect: 命中音槽位 '{impactSfxSlot}' 取不到"
                         + "（检查 configs/music.ini 的 [sfx] 段）");
            return;
        }

        AudioStreamPlayer voice = impactVoices[nextImpactVoice];
        nextImpactVoice = (nextImpactVoice + 1) % impactVoices.Length;
        voice.Stream = stream;
        voice.Play();
    }

    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                                    cardBase_ source = null, int count = 0)
    {
        if (positions == null || positions.Count < 2) return;

        // 弹数：场景里写了正数就用它（bullet 固定 10 发、坦克炮固定 1 发），
        // 填 0 则用调用方给的数量（bombing = 攻击力）。
        int shots = ProjectileCount > 0 ? ProjectileCount : count;
        if (shots <= 0) return;

        PackedScene scene = ResourceManager.Instance?.GetScene(ProjectileScenePath)
                            ?? ResourceLoader.Load<PackedScene>(ProjectileScenePath);
        if (scene == null)
        {
            GD.PushWarning($"BulletEffect: 弹体场景加载失败 {ProjectileScenePath}");
            return;
        }

        // 飞行时长：调用方传了就用它，否则用场景里配的（航弹比子弹慢得多）。
        float? flightSeconds = time ?? (ProjectileFlightSeconds > 0f ? ProjectileFlightSeconds : null);

        // 炮口烟与第一发同时出：不 await，让它和弹体一起跑。
        _ = PlayChildEffectAsync(MuzzleEffect, new List<Vector2> { positions[0] });

        var tasks = new List<Task>(shots);
        for (int index = 0; index < shots; index++)
        {
            Bullet bullet = BattleEffectPool.Instance?.AcquireProjectile(ProjectileScenePath, scene)
                            ?? scene.Instantiate() as Bullet;
            if (bullet == null) continue;
            AddChild(bullet);
            tasks.Add(PlayAndReleaseBullet(bullet, positions, flightSeconds));

            int delay = StaggerMaxMs > 0 ? Random.Shared.Next(0, StaggerMaxMs) : 0;
            if (delay > 0)
                await ToSignal(GetTree().CreateTimer(delay / 1000.0), SceneTreeTimer.SignalName.Timeout);
        }
        await Task.WhenAll(tasks);

        // 落点烟在**全部弹体都抵达之后**才冒，一次。（`await` 它，否则调用方
        // 会在烟还没演完时把整个特效回收掉——烟会跟着被掐掉半截。）
        await PlayChildEffectAsync(ImpactEffect, new List<Vector2> { positions[positions.Count - 1] });
    }

    /// <summary>
    /// 等一发弹体飞完再回收。`bullet.Play` 是在**飞抵目标那一刻**返回的，
    /// 所以命中音挂在这里的时机正好，不必再往 `Bullet` 里塞回调。
    /// </summary>
    private async Task PlayAndReleaseBullet(Bullet bullet, IReadOnlyList<Vector2> positions, float? time)
    {
        try
        {
            await bullet.Play(positions, time);
            PlayImpactSfx();
        }
        finally
        {
            if (BattleEffectPool.Instance?.ReleaseProjectile(bullet) != true && GodotObject.IsInstanceValid(bullet))
                bullet.QueueFree();
        }
    }
}
