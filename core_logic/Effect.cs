using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class Effect : Control
{
    public abstract Task Play(IReadOnlyList<Vector2> positions = null, float? time = null);
    public virtual void PrepareForUse() => Visible = true;
    public virtual void ResetForPool() => Visible = false;
}

public static class EffectRegistry
{
    private static readonly Dictionary<string, string> ScenePaths = new()
    {
        ["bullet"] = "res://effects/bullet_effect.tscn",
        ["smoke"] = "res://effects/smoke_effect.tscn"
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
