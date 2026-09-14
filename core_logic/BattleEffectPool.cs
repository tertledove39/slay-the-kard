using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleEffectPool : Node
{
    private const int BulletEffectCapacity = 4;
    private const int SmokeEffectCapacity = 8;
    private const int BulletCapacity = 40;
    private const string BulletEffectPath = "res://effects/bullet_effect.tscn";
    private const string SmokeEffectPath = "res://effects/smoke_effect.tscn";
    private const string BulletPath = "res://bin/bullet.tscn";

    public static BattleEffectPool Instance { get; private set; }

    private readonly Queue<BulletEffect> bulletEffects = new();
    private readonly Queue<SmokeEffect> smokeEffects = new();
    private readonly Queue<Bullet> bullets = new();
    private readonly HashSet<Effect> leasedEffects = new();
    private readonly HashSet<Bullet> leasedBullets = new();
    private PackedScene bulletEffectScene;
    private PackedScene smokeEffectScene;
    private PackedScene bulletScene;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void Prewarm(ResourceManager resources)
    {
        resources.PreloadScene(BulletEffectPath);
        resources.PreloadScene(SmokeEffectPath);
        resources.PreloadScene(BulletPath);
        bulletEffectScene = resources.GetScene(BulletEffectPath);
        smokeEffectScene = resources.GetScene(SmokeEffectPath);
        bulletScene = resources.GetScene(BulletPath);

        for (int index = 0; index < BulletEffectCapacity; index++) AddIdleEffect(Instantiate<BulletEffect>(bulletEffectScene));
        for (int index = 0; index < SmokeEffectCapacity; index++) AddIdleEffect(Instantiate<SmokeEffect>(smokeEffectScene));
        for (int index = 0; index < BulletCapacity; index++) AddIdleBullet(Instantiate<Bullet>(bulletScene));
        _ = PrewarmRenderingAsync();
    }

    public Effect AcquireEffect(string name)
    {
        Effect effect = name switch
        {
            "bullet" => bulletEffects.Count > 0 ? bulletEffects.Dequeue() : Instantiate<BulletEffect>(bulletEffectScene),
            "smoke" => smokeEffects.Count > 0 ? smokeEffects.Dequeue() : Instantiate<SmokeEffect>(smokeEffectScene),
            _ => null
        };
        if (effect == null) return null;
        if (effect.GetParent() != null) effect.GetParent().RemoveChild(effect);
        leasedEffects.Add(effect);
        return effect;
    }

    public bool ReleaseEffect(Effect effect)
    {
        if (!leasedEffects.Remove(effect)) return false;
        effect.ResetForPool();
        if (effect.GetParent() != null) effect.GetParent().RemoveChild(effect);
        if (effect is BulletEffect bulletEffect && bulletEffects.Count < BulletEffectCapacity)
            AddIdleEffect(bulletEffect);
        else if (effect is SmokeEffect smokeEffect && smokeEffects.Count < SmokeEffectCapacity)
            AddIdleEffect(smokeEffect);
        else
            effect.QueueFree();
        return true;
    }

    public Bullet AcquireBullet()
    {
        Bullet bullet = bullets.Count > 0 ? bullets.Dequeue() : Instantiate<Bullet>(bulletScene);
        if (bullet == null) return null;
        if (bullet.GetParent() != null) bullet.GetParent().RemoveChild(bullet);
        leasedBullets.Add(bullet);
        bullet.PrepareForUse();
        return bullet;
    }

    public bool ReleaseBullet(Bullet bullet)
    {
        if (bullet == null || !leasedBullets.Remove(bullet)) return false;
        bullet.ResetForPool();
        if (bullet.GetParent() != null) bullet.GetParent().RemoveChild(bullet);
        if (bullets.Count < BulletCapacity) AddIdleBullet(bullet);
        else bullet.QueueFree();
        return true;
    }

    private void AddIdleEffect(Effect effect)
    {
        if (effect == null) return;
        AddChild(effect);
        effect.ResetForPool();
        if (effect is BulletEffect bulletEffect) bulletEffects.Enqueue(bulletEffect);
        else if (effect is SmokeEffect smokeEffect) smokeEffects.Enqueue(smokeEffect);
    }

    private void AddIdleBullet(Bullet bullet)
    {
        if (bullet == null) return;
        AddChild(bullet);
        bullet.ResetForPool();
        bullets.Enqueue(bullet);
    }

    private async Task PrewarmRenderingAsync()
    {
        if (bullets.Count == 0 || smokeEffects.Count == 0) return;
        Bullet bullet = bullets.Peek();
        SmokeEffect smoke = smokeEffects.Peek();
        bullet.Visible = true;
        bullet.Modulate = new Color(1f, 1f, 1f, 0.01f);
        smoke.Visible = true;
        if (smoke.GetNodeOrNull<Sprite2D>("SmokeSprite") is Sprite2D sprite)
            sprite.Modulate = new Color(1f, 1f, 1f, 0.01f);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        bullet.Modulate = Colors.White;
        bullet.ResetForPool();
        smoke.ResetForPool();
    }

    private static T Instantiate<T>(PackedScene scene) where T : Node => scene?.Instantiate() as T;
}
