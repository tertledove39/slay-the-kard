using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class Effect : Control
{
    /// <summary>
    /// 播放一次特效。
    ///
    /// 只给坐标是不够的，所以另外两个入参是必需的：
    /// - `source`：触发这次特效的**单位**。`flying` 要动的是这张卡本身，光有坐标
    ///   拿不到节点，也就没法做「升起 → 左右摆 → 落回」。
    /// - `count`：要生成几个。`bombing` 的弹数 = 攻击力，而特效自己不知道攻击力多少，
    ///   只能由调用方传进来；为 0 时特效用自己的默认值（`bullet` 就是这种）。
    ///
    /// 两者都是可选的：不需要它们的特效（`smoke`）忽略即可。
    /// </summary>
    public abstract Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                              cardBase_ source = null, int count = 0);

    public virtual void PrepareForUse() => Visible = true;
    public virtual void ResetForPool() => Visible = false;
}

public static class EffectRegistry
{
    private static readonly Dictionary<string, string> ScenePaths = new()
    {
        ["bullet"] = "res://effects/bullet_effect.tscn",
        ["smoke"] = "res://effects/smoke_effect.tscn",
        // bombing 与 bullet 共用 BulletEffect 脚本，差别只在场景里 Export 出去的
        // 「弹体场景」与「弹数」——所以这里是两个场景、不是两个类。
        ["bombing"] = "res://effects/bombing_effect.tscn",
        ["flying"] = "res://effects/flying_effect.tscn"
    };

    public static Effect Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string normalizedName = name.Trim();
        Effect pooled = BattleEffectPool.Instance?.AcquireEffect(normalizedName);
        if (pooled != null) return pooled;
        if (!ScenePaths.TryGetValue(normalizedName, out string path)) return null;
        PackedScene scene = ResourceManager.Instance?.GetScene(path) ?? ResourceLoader.Load<PackedScene>(path);
        if (scene == null)
        {
            GD.PushWarning($"EffectRegistry: failed to load effect '{name}' from {path}");
            return null;
        }
        return scene.Instantiate() as Effect;
    }

    public static void Release(Effect effect)
    {
        if (effect == null || !GodotObject.IsInstanceValid(effect)) return;
        if (BattleEffectPool.Instance?.ReleaseEffect(effect) == true) return;
        effect.QueueFree();
    }
}
