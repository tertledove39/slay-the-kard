using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BulletEffect : Effect
{
    private const int BulletCount = 10;
    private const string BulletScenePath = "res://bin/bullet.tscn";

    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null)
    {
        if (positions == null || positions.Count < 2) return;
        PackedScene scene = ResourceManager.Instance?.GetScene(BulletScenePath) ?? ResourceLoader.Load<PackedScene>(BulletScenePath);
        if (scene == null) return;

        var tasks = new List<Task>();
        for (int index = 0; index < BulletCount; index++)
        {
            Bullet bullet = BattleEffectPool.Instance?.AcquireBullet() ?? scene.Instantiate() as Bullet;
            if (bullet == null) continue;
            AddChild(bullet);
            tasks.Add(PlayAndReleaseBullet(bullet, positions, time));
            int delay = Random.Shared.Next(0, 100);
            if (delay > 0)
                await ToSignal(GetTree().CreateTimer(delay / 1000.0), SceneTreeTimer.SignalName.Timeout);
        }
        await Task.WhenAll(tasks);
    }

    private static async Task PlayAndReleaseBullet(Bullet bullet, IReadOnlyList<Vector2> positions, float? time)
    {
        try
        {
            await bullet.Play(positions, time);
        }
        finally
        {
            if (BattleEffectPool.Instance?.ReleaseBullet(bullet) != true && GodotObject.IsInstanceValid(bullet))
                bullet.QueueFree();
        }
    }
}
