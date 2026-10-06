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

    /// <summary>
    /// 播一个**子特效**（按 `EffectRegistry` 里的名字取），播完自动回收。
    ///
    /// 「取 → 进树 → 传参 → 播 → 还」这一套只在写一处：谁是子特效、什么时候播由调用方决定，
    /// 但怎么播不该各写一份。目前两个调用方：`AirStrikeEffect`（起飞时打出去的子特效）、
    /// `BulletEffect`（发射与落点的烟）。
    ///
    /// 子特效**自己的**弹数/时长/尺寸仍然配在它自己的场景里，本方法不碰——
    /// 它只负责把 `count` 原样传下去。
    ///
    /// 出问题只报警不抛出：调用方通常还有别的活在等（比如降落），
    /// 一个烟放不出来不该把整段动画带塌。
    /// </summary>
    protected Task PlayChildEffectAsync(string effectName, IReadOnlyList<Vector2> positions, int count = 0)
        => EffectRegistry.PlayOnceAsync(this, effectName, null, positions, null, count);
}

public static class EffectRegistry
{
    private static readonly Dictionary<string, string> ScenePaths = new()
    {
        ["bullet"] = "res://effects/bullet_effect.tscn",
        ["smoke"] = "res://effects/smoke_effect.tscn",
        // smoke_small 与 smoke 共用 SmokeEffect 脚本，差别只在场景里 Export 出去的 SizeScale
        // ——所以这里不是两套实现，是两个 Export 值（与 bullet / bombing / TankAttack 同一套路）。
        ["smoke_small"] = "res://effects/smoke_small_effect.tscn",
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
        ["sfx"] = "res://effects/sound_effect.tscn",
        // 坦克与火炮的攻击特效：打一发炮弹（`bin/tank_shell.tscn`）。
        // 名字沿用需求方给的大小写 `TankAttack`，**这张表是大小写敏感的**，写错了会报未知特效名。
        ["TankAttack"] = "res://effects/tank_attack_effect.tscn",
        // 转换：抬起 → 翻面（露卡背）→ 再翻面（换成新单位）→ 落回。
        // 与 airstrike / strafe 一样是**继承 FlyingEffect、只覆写一个阶段**。
        // 新单位的 id 走**特效名的参数**：`convert(panzer4)`（与 `sfx(严冬)` 同一套）。
        ["convert"] = "res://effects/convert_effect.tscn"
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
    /// - `sfx`：**攻击特效里自己带了音效**（如喀秋莎 `bullet,sfx(katyusha_fire)`）。
    ///   既然已经指定了要放什么，就不该再叠一层通用开火声；
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
        "airstrike",
        "sfx"
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
            // **必须先拆掉参数再查名单**：名单里存的是特效名（`sfx`），
            // 而卡上写的是 `sfx(katyusha_fire)`——直接拿整串去查永远查不到。
            // （曾经就是这么错的：喀秋莎的机枪声一直没被静音。）
            ParseName(raw, out string name, out _);
            if (NoFiringSoundNames.Contains(name)) return true;
        }
        return false;
    }

    /// <summary>
    /// 这串特效名（`attackEffect` 的原文，可以是逗号分隔的多个）里有没有指定的那一个。
    ///
    /// **按拆好的名字比对，参数会被扔掉**——`TankAttack,sfx(x)` 里找 `TankAttack` 要能命中，
    /// 而在 `sfx(katyusha_fire)` 里找 `TankAttack` 要能落空。
    /// 别用 `string.Contains`：那会把 `sfx(TankAttack)` 这种参数也算进去
    /// （`ReplacesFiringSound` 就是栽在这上面，见 `BUGS.md` #56）。
    /// </summary>
    public static bool Contains(string effectNames, string name)
    {
        if (string.IsNullOrWhiteSpace(effectNames) || string.IsNullOrWhiteSpace(name)) return false;

        foreach (string raw in effectNames.Split(','))
        {
            ParseName(raw, out string parsed, out _);
            if (string.Equals(parsed, name, StringComparison.OrdinalIgnoreCase)) return true;
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

    /// <summary>
    /// 「取一个特效 → 挂到 `host` 上 → 传参（名字括号里那一段）→ 播完 → 回收」的**唯一实现**。
    ///
    /// 三个调用方共用：`AirStrikeEffect`（起飞时打出去的子特效）、
    /// `BulletEffect`（发射与落点的烟）、`battlefield_`（`convert` 指令要**等**它演完）。
    ///
    /// 与 `battlefield_.StartEffect` 的分工：那个是**不等**的（fire-and-forget，攻击特效走它），
    /// 这个是**等**的。需要「演完再往下走」时才用这个。
    ///
    /// 出问题只报警不抛出：调用方通常还有别的活在等（比如降落、后面的效果指令）。
    /// </summary>
    /// <param name="host">特效挂在谁下面（通常是触发它的那个场景/特效自身）。</param>
    /// <param name="argument">特效名括号里的参数，不需要时传 null。</param>
    /// <param name="source">触发它的单位（`flying` 要动的那张卡）。</param>
    public static async Task PlayOnceAsync(Node host, string name, string argument,
                                           IReadOnlyList<Vector2> positions,
                                           cardBase_ source = null, int count = 0)
    {
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        if (string.IsNullOrWhiteSpace(name)) return;

        Effect effect = Create(name);
        if (effect == null)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} Effect.cs: 特效取不到，本次跳过: '{name}'");
            return;
        }

        // 顺序是刻意的：先进树（`_Ready` 跑完、子节点就绪），再传参数，最后才开演。
        // 反过来的话 `Configure` 里 `GetNode` 会拿到 null。
        host.AddChild(effect);
        effect.Configure(argument);
        effect.PrepareForUse();
        try
        {
            await effect.Play(positions, null, source, count);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} Effect.cs: 特效播放异常 "
                         + $"'{name}': {exception.Message}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(effect)) Release(effect);
        }
    }
}
