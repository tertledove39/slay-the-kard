using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「从 A 点向 B 点打出一串弹体」的特效。
///
/// 一个脚本、两个场景，靠 Export 出来的两个值区分：
/// - `effects/bullet_effect.tscn` → 子弹，固定 10 发；
/// - `effects/bombing_effect.tscn` → 航弹，弹数填 0 表示**用调用方给的数量**
///   （即攻击力，见 `Effect.Play` 的 `count`）。
///
/// 之所以不做成两个类：生成、随机错开、回池这一整套逻辑完全一样，
/// 分开写就是两份会各自长歪的实现。
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
    /// **每发命中时**播放的音效槽位（空字符串 = 不播）。对应 configs/music.ini 的 [sfx] 段。
    /// `bullet` 不填（保持原样），`bombing` 填 `dead` 让每发航弹落地各炸一声。
    /// </summary>
    [Export] public string ImpactSfxSlot = "";

    /// <summary>命中音效的音量（线性，1 = 原音量）。弹数多时叠在一起会偏吵，可以调小。</summary>
    [Export] public float ImpactSfxVolume = 1f;

    /// <summary>
    /// 命中音效的声部数。多发弹体是错开命中的，只用一个播放器会让后一声掐掉前一声，
    /// 听起来像少炸了几发。
    /// </summary>
    [Export] public int ImpactVoiceCount = 6;

    /// <summary>音效播放器所在的音频总线（与战场里既有的战斗音效一致）。</summary>
    private const string SfxBus = "SFX";

    private AudioStreamPlayer[] impactVoices;
    private int nextImpactVoice;

    public override void _Ready()
    {
        // 导出值在 Instantiate 时就已就位，这里建声部即可。
        if (string.IsNullOrWhiteSpace(ImpactSfxSlot) || ImpactVoiceCount <= 0) return;

        float volumeDb = Mathf.LinearToDb(Mathf.Clamp(ImpactSfxVolume, 0.0001f, 1f));
        impactVoices = new AudioStreamPlayer[ImpactVoiceCount];
        for (int index = 0; index < ImpactVoiceCount; index++)
        {
            var voice = new AudioStreamPlayer { Bus = SfxBus, VolumeDb = volumeDb };
            AddChild(voice);
            impactVoices[index] = voice;
        }
    }

    /// <summary>一发命中：轮换一个空闲声部放音效，让连发的爆炸声能叠着响。</summary>
    private void PlayImpactSfx()
    {
        if (impactVoices == null || impactVoices.Length == 0) return;

        AudioStream stream = MusicManager.Instance?.PickSfx(ImpactSfxSlot);
        if (stream == null) return;

        AudioStreamPlayer voice = impactVoices[nextImpactVoice];
        nextImpactVoice = (nextImpactVoice + 1) % impactVoices.Length;
        voice.Stream = stream;
        voice.Play();
    }

    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                                    cardBase_ source = null, int count = 0)
    {
        if (positions == null || positions.Count < 2) return;

        // 弹数：场景里写了正数就用它（bullet 固定 10 发），
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
    }

    /// <summary>
    /// 等一发弹体飞完再回收。`bullet.Play` 是在**飞抵目标那一刻**返回的，
    /// 所以命中音效挂在这里的时机正好，不必再往 `Bullet` 里塞一个回调。
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
