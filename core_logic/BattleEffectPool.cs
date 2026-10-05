using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleEffectPool : Node
{
    private const int BulletEffectCapacity = 4;
    private const int SmokeEffectCapacity = 8;

    /// <summary>每种弹体各自池化的上限。弹体种类按场景路径区分，子弹与航弹各池各的。</summary>
    private const int ProjectileCapacityPerPath = 40;

    private const string BulletEffectPath = "res://effects/bullet_effect.tscn";
    private const string SmokeEffectPath = "res://effects/smoke_effect.tscn";

    /// <summary>需要预热+池化的弹体场景。加新弹体（航弹等）只需在这里补一条。</summary>
    private static readonly string[] ProjectileScenePaths =
    {
        "res://bin/bullet.tscn",
        "res://bin/bomb.tscn",
    };

    public static BattleEffectPool Instance { get; private set; }

    private readonly Queue<BulletEffect> bulletEffects = new();
    private readonly Queue<SmokeEffect> smokeEffects = new();

    // 弹体池按场景路径分池。混成一个队列会串味——取到航弹却按子弹的贴图/朝向播。
    private readonly Dictionary<string, Queue<Bullet>> projectilePools = new();

    private readonly HashSet<Effect> leasedEffects = new();

    // 弹体 → 它属于哪个池。释放时要还回原来的池，所以得记着来路，
    // 不去猜 Godot 内部（SceneFilePath 之类）在池化往返后是否还可靠。
    private readonly Dictionary<Bullet, string> leasedProjectiles = new();

    private readonly Dictionary<string, PackedScene> projectileScenes = new();
    private PackedScene bulletEffectScene;
    private PackedScene smokeEffectScene;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void Prewarm(ResourceManager resources)
    {
        resources.PreloadScene(BulletEffectPath);
        resources.PreloadScene(SmokeEffectPath);
        bulletEffectScene = resources.GetScene(BulletEffectPath);
        smokeEffectScene = resources.GetScene(SmokeEffectPath);

        foreach (string path in ProjectileScenePaths)
        {
            resources.PreloadScene(path);
            PackedScene scene = resources.GetScene(path);
            projectileScenes[path] = scene;
            var pool = GetPool(path);
            for (int index = 0; index < ProjectileCapacityPerPath; index++)
                AddIdleProjectile(path, Instantiate<Bullet>(scene), pool);
        }

        for (int index = 0; index < BulletEffectCapacity; index++) AddIdleEffect(Instantiate<BulletEffect>(bulletEffectScene));
        for (int index = 0; index < SmokeEffectCapacity; index++) AddIdleEffect(Instantiate<SmokeEffect>(smokeEffectScene));
        _ = PrewarmRenderingAsync();
    }

    private Queue<Bullet> GetPool(string scenePath)
    {
        if (!projectilePools.TryGetValue(scenePath, out Queue<Bullet> pool))
        {
            pool = new Queue<Bullet>();
            projectilePools[scenePath] = pool;
        }
        return pool;
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

    /// <summary>
    /// 取一发弹体。`scenePath` 决定取哪个池（子弹 / 航弹），
    /// `fallbackScene` 是池空且缓存里也没有时的兜底实例化来源。
    /// </summary>
    public Bullet AcquireProjectile(string scenePath, PackedScene fallbackScene = null)
    {
        Queue<Bullet> pool = GetPool(scenePath);
        Bullet bullet;
        if (pool.Count > 0)
        {
            bullet = pool.Dequeue();
        }
        else
        {
            PackedScene scene = fallbackScene
                                ?? (projectileScenes.TryGetValue(scenePath, out PackedScene cached) ? cached : null)
                                ?? ResourceManager.Instance?.GetScene(scenePath)
                                ?? ResourceLoader.Load<PackedScene>(scenePath);
            bullet = Instantiate<Bullet>(scene);
        }

        if (bullet == null) return null;
        if (bullet.GetParent() != null) bullet.GetParent().RemoveChild(bullet);
        leasedProjectiles[bullet] = scenePath;
        bullet.PrepareForUse();
        return bullet;
    }

    public bool ReleaseProjectile(Bullet bullet)
    {
        if (bullet == null || !leasedProjectiles.Remove(bullet, out string scenePath)) return false;
        bullet.ResetForPool();
        if (bullet.GetParent() != null) bullet.GetParent().RemoveChild(bullet);

        Queue<Bullet> pool = GetPool(scenePath);
        if (pool.Count < ProjectileCapacityPerPath) AddIdleProjectile(scenePath, bullet, pool);
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

    private void AddIdleProjectile(string scenePath, Bullet bullet, Queue<Bullet> pool)
    {
        if (bullet == null) return;
        AddChild(bullet);
        bullet.ResetForPool();
        pool.Enqueue(bullet);
    }

    /// <summary>
    /// 先让每种弹体各露一帧（极低透明度）再收回去：着色器/贴图没预热过时，
    /// 第一次真正开火会卡一下。每种弹体都要预热，光热子弹不管航弹等于白做。
    /// </summary>
    private async Task PrewarmRenderingAsync()
    {
        var warmed = new List<Bullet>();
        foreach (string path in ProjectileScenePaths)
        {
            Queue<Bullet> pool = GetPool(path);
            if (pool.Count == 0) continue;
            Bullet bullet = pool.Peek();
            bullet.Visible = true;
            bullet.Modulate = new Color(1f, 1f, 1f, 0.01f);
            warmed.Add(bullet);
        }

        if (warmed.Count == 0) return;

        SmokeEffect smoke = smokeEffects.Count > 0 ? smokeEffects.Peek() : null;
        if (smoke != null)
        {
            smoke.Visible = true;
            if (smoke.GetNodeOrNull<Sprite2D>("SmokeSprite") is Sprite2D sprite)
                sprite.Modulate = new Color(1f, 1f, 1f, 0.01f);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        foreach (Bullet bullet in warmed)
        {
            bullet.Modulate = Colors.White;
            bullet.ResetForPool();
        }
        smoke?.ResetForPool();
    }

    private static T Instantiate<T>(PackedScene scene) where T : Node => scene?.Instantiate() as T;
}
