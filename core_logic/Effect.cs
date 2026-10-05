using Godot;
using System;
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

    /// <summary>
    /// 特效名括号里带的那一段参数。`playEffect = sfx(严冬)` 传进来的就是「严冬」；
    /// 名字不带括号时是 `null`。
    ///
    /// **在 `AddChild` 之后、`Play` 之前调用**，所以需要时可安全地 `GetNode`。
    /// 不需要参数的特效（`bullet`、`smoke`……）忽略它即可。
    /// </summary>
    public virtual void Configure(string argument) { }

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
        ["flying"] = "res://effects/flying_effect.tscn",
        // airstrike = 飞掠 + 投弹；strafe = 飞掠 + 打枪。
        // 两个场景挂的是**同一个脚本**（AirStrikeEffect），差别只在 Export 出去的
        // `StrikeEffectName`（bombing / bullet）——所以这里不是两套实现，是两套 Export。
        // 再加「飞起来干别的」（火箭弹、机枪扫射……）同样只是再加一个场景。
        ["airstrike"] = "res://effects/air_strike_effect.tscn",
        ["strafe"] = "res://effects/strafe_effect.tscn",
        // 只放音效、不画任何东西。槽位由**特效名的参数**指定：`playEffect = sfx(严冬)`
        // ——所以卡牌语音不需要一场景一音效，全项目共用这一个。
        ["sfx"] = "res://effects/sound_effect.tscn"
    };

    /// <summary>
    /// 把 `名字(参数)` 拆成名字与参数——`sfx(严冬)` 拆成 `sfx` 与 `严冬`。
    /// 名字不带括号时 `argument` 为 `null`。
    ///
    /// 解析放在注册表里，是因为**特效名的格式本来就是它定的**（`Create` 只认名字那一段）。
    /// 调用方另有括号感知的拆分器（`battlefield_.SplitEffectString`）负责切多个特效，
    /// 两者配合下参数里出现逗号也不会被拆坏。
    /// </summary>
    public static void ParseName(string raw, out string name, out string argument)
    {
        name = (raw ?? string.Empty).Trim();
        argument = null;

        int open = name.IndexOf('(');
        if (open <= 0 || !name.EndsWith(")")) return;

        argument = name.Substring(open + 1, name.Length - open - 2).Trim();
        name = name.Substring(0, open).Trim();
    }

    /// <summary>
    /// **不放通用开火声**的特效名。`Attack()` 每次都放一声通用的开火声
    /// （`battleSound`，资源就是 `机枪_低.wav`）；名单里的特效不放。
    ///
    /// 名单怎么定——看这个特效**打出来的东西和机枪声搭不搭**，而不是看它有没有自己的音效：
    /// - `flying` / `airstrike`：**飞机掠过然后扔炸弹**。炸弹配机枪「哒哒」是串味，所以不放；
    /// - `strafe`：**飞机掠过然后打枪**。打的就是子弹，**机枪声正是它要的**，所以不在名单里。
    ///
    /// 换句话说「自带音效」并不自动等于「不要开火声」——`strafe` 自带飞掠声，
    /// 但它照样要那声机枪。所以这张表叫 `NoFiringSoundNames`（按结果命名），
    /// 而不是按原因命名。
    ///
    /// 判定放在注册表里，是因为「这个特效是什么」本来就是这张表在管：
    /// 写到卡上、或按 `CardTypes.Bomber` 判，都会让同一个特效配在不同卡上行为不一致。
    ///
    /// `tests/verify_attack_effects.py` 会交叉核对表里每个名字对应的场景**确实挂了
    /// `AudioStreamPlayer`**（防止这张表变成假话），并单独钉住 `strafe` **不在**表里。
    /// </summary>
    private static readonly HashSet<string> NoFiringSoundNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "flying",
        "airstrike"
    };

    /// <summary>
    /// 这串特效名（`attackEffect` 的原文，可以是逗号分隔的多个）里有没有
    /// 「不放通用开火声」的。有一个就算——`attackEffect = flying,bullet` 那种写法下，
    /// 飞机声和机枪声已经串了，再补一声开火声只会更糊。
    /// </summary>
    public static bool ReplacesFiringSound(string effectNames)
    {
        if (string.IsNullOrWhiteSpace(effectNames)) return false;

        foreach (string raw in effectNames.Split(','))
        {
            if (NoFiringSoundNames.Contains(raw.Trim())) return true;
        }
        return false;
    }

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
