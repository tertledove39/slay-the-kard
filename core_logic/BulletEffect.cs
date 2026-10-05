using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「从 A 点向 B 点打出一串弹体」的特效。
///
/// 一个脚本、两个场景，靠 Export 出来的两个值区分：
/// - `effects/bullet_effect.tscn` → 子弹，固定 10 发，0.3 秒飞完全程；
/// - `effects/bombing_effect.tscn` → 航弹，弹数填 0 表示**用调用方给的数量**
///   （即攻击力，见 `Effect.Play` 的 `count`），飞行时长远长于子弹。
///
/// 之所以不做成两个类：生成、随机错开、回池这一整套逻辑完全一样，
/// 分开写就是两份会各自长歪的实现。
///
/// **这个特效不发声。** 曾经有过一套「每发命中各播一声」的机制
/// （`ImpactSfxSlot` / `ImpactSfxVolume` / `ImpactVoiceCount`），但航弹连发时
/// 每发各炸一声会糊成一片，已整段删除——现在「不播音效」是结构上的事实，
/// 而不是靠场景里把某个槽位留空。要恢复的话见 git 历史 `0a75da7` 之前的版本。
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

    /// <summary>等一发弹体飞完再回收（`bullet.Play` 是在**飞抵目标那一刻**返回的）。</summary>
    private async Task PlayAndReleaseBullet(Bullet bullet, IReadOnlyList<Vector2> positions, float? time)
    {
        try
        {
            await bullet.Play(positions, time);
        }
        finally
        {
            if (BattleEffectPool.Instance?.ReleaseProjectile(bullet) != true && GodotObject.IsInstanceValid(bullet))
                bullet.QueueFree();
        }
    }
}
