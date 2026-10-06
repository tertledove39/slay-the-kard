#!/usr/bin/env python3
"""attackEffect 支持多个效果；新增 flying（飞掠）与 bombing（航弹）两个特效。

需求：
  1) `attackEffect` 里能填多个效果（英文逗号分隔）
  2) 新增 `flying`：卡牌飘起来 → 轻微左右摆动 → 落回桌上；配 飞机飞过_单位 音效
  3) 新增 `bombing`：用 航弹.png 做一个类 bullet 的效果，攻击力多少就扔多少发

设计要点：
  · 只给坐标不够用——`flying` 要动的是**那张卡本身**（拿不到节点就没法做动画），
    `bombing` 的弹数 = **攻击力**（特效自己不知道攻击力）。所以 `Effect.Play` 增加了
    `source` 与 `count` 两个入参，而不是给这两个新效果各开一个特例。
  · `bombing` 与 `bullet` **共用 BulletEffect 脚本**，差别只在场景里 Export 出去的
    「弹体场景」与「弹数」——不写第二个类。
  · 弹体池按场景路径分池：子弹与航弹混在一个队列里会串味。
  4) 新增 `airstrike`：子特效要**夹在飞掠中间**（起飞 → 打 → 降落）。
     拼接写法做不到这一点，所以 `AirStrikeEffect` 继承 `FlyingEffect`
     并只覆写 `DuringRiseAsync`，运动代码仍只有一份。
  5) 新增 `strafe`：与 `airstrike` 同脚本同节奏，只把子特效从 `bombing` 换成 `bullet`。
     **「其他不变」由「两个场景的非注释行做差集只差子特效名」这条断言守着。**

关于中文注释的落点：`.tscn` 里的 `;` 注释**留不住**——在 Godot 编辑器里保存一次
就会被整段抹掉，取值恰好等于 C# 默认值的属性也会被省略。所以本测试把中文说明
钉在 C# 的 `/// <summary>` 上，场景注释只作提示、不作失败。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
EFFECT = ROOT / "core_logic" / "Effect.cs"
BULLET_EFFECT = ROOT / "core_logic" / "BulletEffect.cs"
POOL = ROOT / "core_logic" / "BattleEffectPool.cs"
FLYING = ROOT / "core_logic" / "FlyingEffect.cs"
AIRSTRIKE = ROOT / "core_logic" / "AirStrikeEffect.cs"
AIRSTRIKE_SCENE = ROOT / "effects" / "air_strike_effect.tscn"
CARD_INI = ROOT / "cards" / "card.ini"
BULLET = ROOT / "bin" / "Bullet.cs"
SMOKE = ROOT / "core_logic" / "SmokeEffect.cs"
CONVERT = ROOT / "core_logic" / "ConvertEffect.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
CARD_BASE = ROOT / "bin" / "cardBase_.cs"
MUSIC_INI = ROOT / "configs" / "music.ini"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


def exports_of(cs_text):
    """C# 里声明的 [Export] 字段名，按声明顺序。"""
    return re.findall(r"\[Export\]\s+public\s+[\w<>\[\],\s]+?\s(\w+)\s*=", cs_text)


def uncommented_exports(scene_text, names):
    """返回「场景里的赋值行上方没有中文 ; 注释」的导出项名。

    .tscn 用分号作注释，且**必须单独一行**写在属性上方（行尾注释会被当成值的一部分）。
    """
    lines = scene_text.split("\n")
    missing = []
    for name in names:
        for index, line in enumerate(lines):
            if not re.match(rf"{re.escape(name)}\s*=", line):
                continue

            previous = ""
            for back in range(index - 1, -1, -1):
                if lines[back].strip():
                    previous = lines[back].strip()
                    break

            if not (previous.startswith(";") and re.search(r"[一-鿿]", previous)):
                missing.append(name)
            break
    return missing


# .tscn 里「引擎自带」的属性，不算手写的导出项。
# 注意这些名字是**全文件**扫描的：场景里除了挂脚本的根节点，往往还有子节点
# （`smoke_effect.tscn` 的 Sprite2D 就写了 texture / hframes / vframes），
# 那些是子节点自己的引擎属性，与「脚本的 Export 拼错了」是两回事。
ENGINE_PROPS = {
    "script", "layout_mode", "anchors_preset", "anchor_left", "anchor_top",
    "anchor_right", "anchor_bottom", "offset_left", "offset_top", "offset_right",
    "offset_bottom", "grow_horizontal", "grow_vertical", "mouse_filter", "visible",
    "z_index", "bus", "stream", "autoplay", "volume_db", "pitch_scale",
    "size_flags_horizontal", "size_flags_vertical", "focus_mode", "name",
    # Sprite2D / Node2D 的：
    "texture", "hframes", "vframes", "scale", "position", "rotation", "modulate",
}


def unknown_scene_props(scene_text, export_names):
    """场景里显式赋值的、既不是该类 Export 也不是引擎属性的名字。

    Godot 对 .tscn 里拼错的属性名是**静默忽略**的：`SwayDegree = 5`（少个 s）
    不会报任何错，只表现成「改了没反应」。这条断言就是拿来堵这种静默失败的。
    """
    known = set(export_names) | ENGINE_PROPS
    unknown = []
    for line in scene_text.split("\n"):
        match = re.match(r"([A-Za-z_]\w*)\s*=", line)
        if match and match.group(1) not in known:
            unknown.append(match.group(1))
    return unknown


def lacking_chinese_doc(cs_text, export_names):
    """返回「上方没有中文 /// 注释」的 Export 名。

    为什么不查 .tscn 里的 `;` 注释：那个**留不住**。实测在 Godot 编辑器里保存一次，
    注释会被整段抹掉，取值恰好等于 C# 默认值的属性也会被省略不写
    （所以「场景里必须写全每个 Export」这个要求本身就做不到）。
    C# 的 `/// <summary>` 才是中文说明唯一可靠的落点，也正是编辑器里悬停能看到的那份。
    """
    lacking = []
    for name in export_names:
        pattern = re.compile(r"((?:^[ \t]*///.*\n)+)[ \t]*\[Export\][^;\n]*?\b"
                             + re.escape(name) + r"\s*=", re.MULTILINE)
        match = pattern.search(cs_text)
        if match is None or not re.search(r"[一-鿿]", match.group(1)):
            lacking.append(name)
    return lacking


def code_only(cs_text):
    """去掉整行注释后的 C# 代码。

    断言「某段机制已经不存在」时必须先剥掉注释：说明「这东西删掉了」的文档
    本身就会写出那个名字，拿全文去搜会把自己的说明当成残留。
    """
    keep = []
    for line in cs_text.split("\n"):
        if line.strip().startswith("//"):
            continue
        # 行尾注释只在 `//` 不在字符串里时才切——`"res://..."` 里也有两个斜杠。
        cut = line.find("//")
        if cut >= 0 and line[:cut].count('"') % 2 == 0:
            line = line[:cut]
        keep.append(line)
    return "\n".join(keep)


def scene_paths(text):
    """取出 EffectRegistry.ScenePaths 里的 名字 -> 路径。"""
    block = method(text, "private static readonly Dictionary<string, string> ScenePaths",
                   "public static Effect Create")
    return dict(re.findall(r'\["(\w+)"\]\s*=\s*"([^"]+)"', block))


def main():
    results = []
    effect = EFFECT.read_text(encoding="utf-8")
    bullet_effect = BULLET_EFFECT.read_text(encoding="utf-8")
    pool = POOL.read_text(encoding="utf-8")
    flying = FLYING.read_text(encoding="utf-8")
    bullet = BULLET.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    card_base = CARD_BASE.read_text(encoding="utf-8")

    # ==================== ① 多个效果 ====================
    print("--- ① attackEffect 支持多个效果 ---")
    start = method(battle, "private bool StartEffect(", "private async Task RunEffect(")
    # 拆分现在交给 `SplitEffectString`——它**括号感知**，`sfx(严冬)` 这种带参数的名字
    # 才不会被拆坏（朴素 Split 会把参数里的逗号也当分隔符）。
    results.append(check("SplitEffectString(effectNames, ',')" in start,
                         "用括号感知的 SplitEffectString 拆分名字"))
    results.append(check("EffectNameSeparator = { ',' }" in battle, "分隔符是与 traits 一致的英文逗号"))
    results.append(check(
        'GD.Print($"[Effect] 未知特效名' in start,
        "未知名字留日志（静默忽略会让「拼错了」表现成「打了没特效」）",
    ))
    results.append(check("started = true;" in start and "return started;" in start,
                         "返回「是否至少启动了一个」"))
    results.append(check("_ = RunEffect(effect, positions, time, source, count);" in start,
                         "多个效果各自独立开跑（fire-and-forget），互不等待"))

    # ==================== Effect.Play 的上下文 ====================
    print("\n--- Effect.Play 增加 source / count ---")
    play = method(effect, "public abstract Task Play(", "public virtual void PrepareForUse()")
    results.append(check("cardBase_ source = null" in play, "基类 Play 收 source（触发它的单位）"))
    results.append(check("int count = 0" in play, "基类 Play 收 count（要生成几个）"))
    for name, text in [("Bullet", bullet), ("SmokeEffect", smoke), ("BulletEffect", bullet_effect)]:
        results.append(check("cardBase_ source = null, int count = 0" in text,
                             f"{name} 跟上了新签名"))

    atk = method(battle, "private bool PlayAttackEffect(", "private bool StartEffect(")
    results.append(check("int attack = from.ReadAttack();" in atk, "取出攻击力"))
    results.append(check("null, from, attack);" in atk, "把攻击者与攻击力一起传给特效"))
    results.append(check("if (attack <= 0) return false;" in atk, "攻击力为 0 时不出手"))
    card_eff = method(battle, "private void PlayCardEffect(", "private bool PlayAttackEffect(")
    results.append(check("null, card);" in card_eff, "playEffect 也把卡本身传进去"))

    # ==================== 注册表与文件存在性 ====================
    print("\n--- 特效注册表 ---")
    paths = scene_paths(effect)
    results.append(check("bombing" in paths, "注册了 bombing"))
    results.append(check("flying" in paths, "注册了 flying"))
    results.append(check("airstrike" in paths, "注册了 airstrike"))
    results.append(check("TankAttack" in paths, "注册了 TankAttack（大小写照需求方给的写）"))
    for name, path in sorted(paths.items()):
        results.append(check((ROOT / path.replace("res://", "")).exists(),
                             f"注册表里的 {name} → {path} 真实存在"))

    # ==================== ② flying ====================
    print("\n--- ② flying 飞掠 ---")
    for field in ["RiseHeight", "RiseScale", "SwayDegrees", "SwaySecondsPerCycle", "SwayCycles",
                  "RiseDuration", "LandDuration", "TopZIndex", "SfxSlot"]:
        results.append(check(f"[Export] public float {field}" in flying
                             or f"[Export] public int {field}" in flying
                             or f"[Export] public string {field}" in flying,
                             f"{field} 是 Export（调手感不必改代码）"))

    # 「摆动 4 秒一个周期」这种要求用「总时长的百分比」表达不出来，
    # 所以每段改成各自填秒数。
    results.append(check("RiseDuration + SwaySecondsPerCycle * SwayCycles + LandDuration" in flying,
                         "总时长由三段各自的秒数相加，不再按百分比切"))
    results.append(check("RiseFraction" not in flying and "AimFraction" not in flying
                         and "SwayFraction" not in flying,
                         "百分比分段已移除"))

    results.append(check("private async Task RiseAsync(" in flying
                         and "private async Task LandAsync(" in flying
                         and "private async Task SwayAsync(" in flying,
                         "三段：升起 / 悬停（内置摆动）/ 落回"))
    results.append(check("private async Task AimAsync(" not in flying
                         and "RiseAndAimAsync" not in flying
                         and "SwayAroundAimAsync" not in flying,
                         "旧的「升起含转向」「绕瞄准角摆动」两个方法已消失"))

    # 主人明确要求：**不转向**。卡在整段动画里保持进入前的角度。
    results.append(check("AimRotationOffset" not in flying, "指向目标的角度计算已整段删除"))
    results.append(check("public float SwayDegrees = 0f;" in flying,
                         "SwayDegrees 默认为 0（默认就是静止悬停，不是小幅摆动）"))

    rise = method(flying, "private async Task RiseAsync(", "protected virtual async Task StayAsync(")
    results.append(check("tween.SetParallel(true);" in rise, "升起与放大并行"))
    results.append(check('TweenProperty(card, "position", basePosition + new Vector2(0f, -RiseHeight), duration)' in rise,
                         "往上升"))
    results.append(check("baseScale * RiseScale" in rise, "漂浮时轻微放大"))
    results.append(check("rotation" not in rise, "升起过程中**不碰角度**（这就是「不转向」）"))

    sway = method(flying, "private async Task SwayAsync(", "private async Task LandAsync(")
    results.append(check("SwayCycles * 2f" in sway or "cycles * 2" in sway,
                         "摆动来回数由 SwayCycles 决定"))
    results.append(check("Mathf.DegToRad(SwayDegrees)" in sway, "摆幅是角度（DegToRad），不是像素"))
    results.append(check("baseRotation + (index % 2 == 0 ? offset : -offset)" in sway,
                         "摆动绕的是**卡牌自己的原始角度**，不是瞄准角"))
    results.append(check('tween.TweenProperty(card, "rotation", baseRotation, step)' in sway,
                         "最后停回原角度（落回才不会斜着停住）"))

    # 静止悬停（SwayDegrees = 0）时不该建一串「原地不动」的 tween
    stay = method(flying, "protected virtual async Task StayAsync(", "private async Task SwayAsync(")
    results.append(check("Mathf.IsZeroApprox(SwayDegrees)" in stay
                         and "stateTimer" not in stay,
                         "不摆时走「等够时间」的分支"))
    results.append(check("SceneTreeTimer.SignalName.Timeout" in stay, "静止悬停靠计时器等够时长"))
    results.append(check("await SwayAsync(card, baseRotation, stayDuration);" in stay,
                         "摆幅非 0 时才真的摆"))

    # 手感数值（摆幅、周期、升降时长）由主人在编辑器里调，测试**不钉具体数字**：
    # 钉了就会「调一次手感红一次」，那种红灯最后没人看。这里只钉两件不会变的事。
    flying_scene = (ROOT / "effects" / "flying_effect.tscn").read_text(encoding="utf-8")
    unknown = unknown_scene_props(flying_scene, exports_of(flying))
    results.append(check(not unknown, f"flying 场景里写的属性名都真实存在（可疑: {unknown}）"))

    results.append(check("source.isUnderCardEffect = true;" in flying, "漂浮期间标记「特效在管这张卡」"))
    results.append(check("source.ZIndex = TopZIndex;" in flying, "抬高层级压住其他卡"))
    fin = method(flying, "finally", "private async Task RiseAsync(")
    results.append(check("source.Position = basePosition;" in fin
                         and "source.Scale = baseScale;" in fin
                         and "source.Rotation = baseRotation;" in fin
                         and "source.ZIndex = baseZIndex;" in fin
                         and "source.isUnderCardEffect = false;" in fin,
                         "finally 里还原位置/缩放/角度/层级/标记（中途出错也不能让它永远浮着斜着）"))
    results.append(check("MusicManager.Instance?.PickSfx(SfxSlot)" in flying, "音效走 [sfx] 槽位"))
    results.append(check('SfxSlot = "flyby"' in flying, "默认槽位名 flyby"))

    results.append(check('path="res://core_logic/FlyingEffect.cs"' in flying_scene, "场景挂了 FlyingEffect"))
    results.append(check("AudioStreamPlayer" in flying_scene and 'bus = &"SFX"' in flying_scene,
                         "场景里有一个 SFX 总线上的播放器"))

    # 卡牌侧的两个配合点
    results.append(check("public bool isUnderCardEffect = false;" in card_base, "cardBase_ 有该标记"))
    results.append(check("isUnderCardEffect = false;" in card_base, "SetCardInformation 里归零（对象池复用）"))
    order = method(battle, "void RefreshAllCardDisplayOrder()", "var handCards = player1.GetCardsInHand();")
    results.append(check("card.isDiscarding || card.isUnderCardEffect" in order,
                         "刷新显示顺序会跳过特效期间的卡（否则抬起的层级下一帧就被打回 10）"))

    # ==================== ③ bombing ====================
    print("\n--- ③ bombing 航弹 ---")
    results.append(check('[Export] public string ProjectileScenePath' in bullet_effect, "弹体场景是 Export"))
    results.append(check('[Export] public int ProjectileCount' in bullet_effect, "弹数是 Export"))
    results.append(check("ProjectileCount > 0 ? ProjectileCount : count" in bullet_effect,
                         "弹数填 0 时改用调用方给的数量（= 攻击力）"))
    results.append(check("AcquireProjectile(ProjectileScenePath, scene)" in bullet_effect,
                         "按场景路径取弹体"))
    results.append(check("[Export] public float ProjectileFlightSeconds" in bullet_effect,
                         "飞行时长是 Export（不再写死在 Bullet 里的 0.3）"))
    results.append(check("float? flightSeconds = time ?? (ProjectileFlightSeconds > 0f ? ProjectileFlightSeconds : null);" in bullet_effect,
                         "调用方没传时长时用场景配的值"))
    results.append(check("PlayAndReleaseBullet(bullet, positions, flightSeconds)" in bullet_effect,
                         "把飞行时长传给每一发弹体"))
    results.append(check("[Export] public int StaggerMaxMs" in bullet_effect, "错开间隔也是 Export"))

    # 命中音是**可选能力**，槽位由特效名的参数给出，不传就整条路都不存在。
    #
    # 这套机制历史上被整段删过一次（航弹连发时每发各炸一声糊成一片）。它现在回来了，
    # 因为问题从来不是这个能力本身、而是**默认值**：航弹不该响，坦克炮弹该响。
    # 所以这里钉的不是「代码里没有音效」，而是**不传参数就一定不响**——
    # `Configure` 里那句提前 return 才是真正要守住的东西（bullet / bombing 都走这条）。
    results.append(check("public override void Configure(string argument)" in bullet_effect,
                         "命中音槽位走**特效名的参数**（口径取决于攻击者，场景里配不出来）"))
    configure = method(bullet_effect, "public override void Configure(", "private void PlayImpactSfx(")
    results.append(check("impactSfxSlot = argument;" in configure
                         and "if (string.IsNullOrWhiteSpace(impactSfxSlot)) return;" in configure,
                         "**不传参数就直接返回**：一个播放器都不建，bullet/bombing 结构上仍然无声"))
    results.append(check("impactVoices == null || impactVoices.Length == 0) return;" in bullet_effect,
                         "没建过声部时 PlayImpactSfx 直接返回（不靠别的分支兜）"))
    results.append(check("[Export] public float ImpactSfxVolume" in bullet_effect
                         and "[Export] public int ImpactVoiceCount" in bullet_effect
                         and "nextImpactVoice = (nextImpactVoice + 1) % impactVoices.Length;" in bullet_effect,
                         "音量与声部数是 Export，多个声部轮换（连发时后一声不掐前一声）"))
    release = method(bullet_effect, "private async Task PlayAndReleaseBullet(", "\n}")
    results.append(check("await bullet.Play(positions, time);" in release
                         and "PlayImpactSfx();" in release
                         and release.index("await bullet.Play(positions, time);")
                         < release.index("PlayImpactSfx();"),
                         "命中音响在**弹体飞抵目标之后**（`bullet.Play` 正是在那一刻返回的）"))

    bombing_scene = (ROOT / "effects" / "bombing_effect.tscn").read_text(encoding="utf-8")
    results.append(check('path="res://core_logic/BulletEffect.cs"' in bombing_scene,
                         "bombing 复用 BulletEffect 脚本（不写第二个类）"))
    results.append(check('ProjectileScenePath = "res://bin/bomb.tscn"' in bombing_scene,
                         "bombing 用航弹弹体"))
    results.append(check("ProjectileCount = 0" in bombing_scene, "bombing 弹数交给攻击力决定"))
    results.append(check(not re.search(r"ImpactSfx|AudioStreamPlayer", bombing_scene),
                         "bombing 场景里没有任何音效配置"))
    results.append(check("ProjectileFlightSeconds = 1.5" in bombing_scene,
                         "航弹飞行 1.5 秒（原来写死 0.3，太快）"))
    bullets_scene = (ROOT / "effects" / "bullet_effect.tscn").read_text(encoding="utf-8")
    results.append(check(not re.search(r"ImpactSfx|AudioStreamPlayer", bullets_scene),
                         "bullet 场景里也没有音效配置（子弹本来就不响）"))

    bomb_scene = (ROOT / "bin" / "bomb.tscn").read_text(encoding="utf-8")
    results.append(check('path="res://bin/Bullet.cs"' in bomb_scene, "航弹复用 Bullet 脚本（运动方式照 bullet）"))
    results.append(check("航弹.png" in bomb_scene, "航弹弹体用 航弹.png"))

    # 飞行缓动：航弹要「一直加速、不减速，到最快那一刻消失」。
    # 缓动曲线做成 Export 而不是在 Bullet 里改死——子弹（机枪）仍要保持原来的 InOut。
    results.append(check("[Export] public Tween.EaseType FlightEase" in bullet,
                         "缓动曲线是 Export（子弹与航弹各用各的）"))
    results.append(check(".SetEase(FlightEase)" in bullet, "Play 里真的用了这个缓动"))
    results.append(check("FlightEase = Tween.EaseType.InOut;" in bullet,
                         "默认仍是 InOut —— 不写这个 Export 的场景（子弹）行为不变"))
    results.append(check("FlightEase = 0" in bomb_scene,
                         "航弹场景设成 0 = EaseType.In（已经用 Godot 实测过 0 就是 In）"))
    results.append(check("FlightEase" not in
                         (ROOT / "bin" / "bullet.tscn").read_text(encoding="utf-8"),
                         "子弹场景不设这个项，吃默认的 InOut（机枪的收尾减速保持不变）"))
    # 「速度最大时直接消失」= In 缓动的最快点在 tween 末尾，而隐藏正好挂在末尾
    play_tail = method(bullet, "await ToSignal(_currentTween, Tween.SignalName.Finished);",
                       "public override void PrepareForUse()")
    results.append(check("OnMoveFinished();" in play_tail,
                         "tween 一结束就隐藏 —— In 缓动下那就是速度最大的那一刻"))

    # 航弹要看得见：净尺寸 = 根节点 scale × Sprite2D scale。
    # 原先是 3 × 0.6 = 1.8，实机反馈太小；现在 3 × 1.2 = 3.6，翻了一倍。
    bomb_root = re.search(r"scale = Vector2\(([\d.]+), ([\d.]+)\)\nscript", bomb_scene)
    bomb_sprite = re.search(r"scale = Vector2\(([\d.]+), ([\d.]+)\)\ntexture", bomb_scene)
    results.append(check(bomb_root is not None and bomb_sprite is not None,
                         "航弹的根缩放与贴图缩放都写在场景里（调大小不必改代码）"))
    if bomb_root and bomb_sprite:
        net = float(bomb_root.group(1)) * float(bomb_sprite.group(1))
        # 浮点：3 × 1.2 会算成 3.5999999999999996，比较要给容差
        results.append(check(round(net, 3) >= 3.6,
                             f"航弹净尺寸 {net:g}（根 {bomb_root.group(1)} × 贴图 {bomb_sprite.group(1)}），"
                             "不小于放大后的 3.6"))

    bullet_scene_path = next(p for n, p in scene_paths(effect).items() if n == "bullet")
    results.append(check("ProjectileCount = 10" in
                         (ROOT / bullet_scene_path.replace("res://", "")).read_text(encoding="utf-8"),
                         "bullet 场景写明固定 10 发（正数 = 固定发数，不随攻击力变）"))

    # ==================== ⑦ 坦克炮弹：炮口烟 / 落点烟 / 发光 / 更快 ====================
    # 主人：「坦克的动画有问题…在坦克炮发射和落点的位置都新增一个小型烟雾，
    #        并让坦克炮像子弹一样发光，再让坦克炮弹飞行速度大幅加快」。
    #
    # 两根烟挂在 BulletEffect 上、**默认关闭**（留空 = 不冒），只有 tank_attack_effect.tscn
    # 填了值——与命中音同一套「默认值而非逐卡配置」的思路，bullet / bombing 两端依旧是干净的。
    print("\n--- ⑦ 坦克炮弹的炮口烟 / 落点烟 / 发光 ---")
    results.append(check('["smoke_small"] = "res://effects/smoke_small_effect.tscn"' in effect,
                         "注册了 smoke_small"))
    results.append(check("[Export] public float SizeScale" in smoke,
                         "SmokeEffect 加的是 **Export**（不是写死的尺寸）"))
    results.append(check("float scale = (1f + frame * 0.2f) * SizeScale;" in smoke,
                         "SizeScale 乘在逐帧放大之上——放大过程本身的速度不受影响"))
    results.append(check("[Export] public float SizeScale = 1f;" in smoke,
                         "默认 1 = 阵亡烟那个大小，既有场景行为不变"))
    small_scene = (ROOT / "effects" / "smoke_small_effect.tscn").read_text(encoding="utf-8")
    results.append(check('path="res://core_logic/SmokeEffect.cs"' in small_scene,
                         "smoke_small 复用 SmokeEffect 脚本（不写第二个类）"))
    size = re.search(r"SizeScale = ([\d.]+)", small_scene)
    results.append(check(size is not None and 0.0 < float(size.group(1)) < 1.0,
                         f"smoke_small 的 SizeScale 确实比 1 小（{size.group(1) if size else '?'}）"))
    results.append(check('path="res://assest/Smoke_006.png"' in small_scene
                         and "hframes = 4" in small_scene and "vframes = 4" in small_scene,
                         "用的是同一张贴图与同一套 4x4 切帧（只改大小不改素材）"))

    # 两根烟各挂在哪一刻：发射是**开跑前**（与第一发同时出），落点是**全部弹体抵达之后**。
    results.append(check("[Export] public string MuzzleEffect" in bullet_effect
                         and "[Export] public string ImpactEffect" in bullet_effect,
                         "两个烟都是 Export（特效名，可带参数）"))
    results.append(check('[Export] public string MuzzleEffect = "";' in bullet_effect
                         and '[Export] public string ImpactEffect = "";' in bullet_effect,
                         "默认留空 = 不冒 —— bullet / bombing 两端依旧干净"))
    muzzle = bullet_effect.index("_ = PlayChildEffectAsync(MuzzleEffect")
    shots = bullet_effect.index("var tasks = new List<Task>(shots);")
    results.append(check(muzzle < shots and "positions[0]" in bullet_effect[muzzle:shots],
                         "炮口烟在**生成弹体之前**就放出去（不 await，与第一发同时跑）"))
    tail = method(bullet_effect, "await Task.WhenAll(tasks);", "\n    }")
    results.append(check("await PlayChildEffectAsync(ImpactEffect" in tail,
                         "落点烟在**全部弹体抵达之后**放，而且 await 它"))
    results.append(check("positions[positions.Count - 1]" in tail,
                         "落点取的是最后一个位置（目标），不是发射点"))
    results.append(check("PlayChildEffectAsync(ImpactEffect" in tail
                         and tail.index("await Task.WhenAll(tasks);") < tail.index("PlayChildEffectAsync(ImpactEffect"),
                         "顺序：先等所有弹体飞完，再冒烟 —— 不然烟会先炸在还没到的地方"))

    tank_scene = (ROOT / "effects" / "tank_attack_effect.tscn").read_text(encoding="utf-8")
    results.append(check('MuzzleEffect = "smoke_small"' in tank_scene
                         and 'ImpactEffect = "smoke_small"' in tank_scene,
                         "tank_attack 场景两个烟都填了 smoke_small"))
    flight = float(re.search(r"ProjectileFlightSeconds = ([\d.]+)", tank_scene).group(1))
    bullet_flight = float(re.search(r"ProjectileFlightSeconds = ([\d.]+)", bullets_scene).group(1))
    results.append(check(flight < bullet_flight,
                         f"炮弹 {flight}s 比子弹 {bullet_flight}s 还快（需求「大幅加快」）"))
    for name in (re.search(r'MuzzleEffect = "(.*?)"', tank_scene).group(1),
                 re.search(r'ImpactEffect = "(.*?)"', tank_scene).group(1)):
        results.append(check(name in scene_paths(effect), f"场景里填的特效名「{name}」真实注册过"))

    # 发光：bullet.tscn 靠 modulate 把贴图抬进 HDR 越过 glow 阈值，坦克炮弹照抄。
    shell_scene = (ROOT / "bin" / "tank_shell.tscn").read_text(encoding="utf-8")
    bullet_tscn = (ROOT / "bin" / "bullet.tscn").read_text(encoding="utf-8")
    # 战场里确实开着 glow，否则 18 倍的 modulate 也只会是「很亮的图」而不是发光的图。
    results.append(check("glow_enabled = true" in
                         (ROOT / "bin" / "battleField.tscn").read_text(encoding="utf-8"),
                         "战场开着 glow（发光才有意义）"))
    bullet_mod = re.search(r"modulate = (Color\([^)]*\))", bullet_tscn)
    shell_mod = re.search(r"modulate = (Color\([^)]*\))", shell_scene)
    results.append(check(bullet_mod is not None and shell_mod is not None
                         and bullet_mod.group(1) == shell_mod.group(1),
                         f"坦克炮弹的 modulate 与子弹**逐字相同**（{shell_mod.group(1) if shell_mod else '?'}）"))
    bright = re.match(r"Color\(([\d.]+)", shell_mod.group(1)) if shell_mod else None
    results.append(check(bright is not None and float(bright.group(1)) > 1.0,
                         "HDR 值远大于 1 —— 配合战场里开着的 glow 就能起 bloom"))

    # ==================== 场景里每个 Export 都要有中文注释 ====================
    print("\n--- 场景里 export 的内容必须带中文注释 ---")
    # 一个场景能写哪些 Export，看的是整条继承链，不是单个文件：
    # air_strike 的场景就合法地写了继承自 FlyingEffect 的 SwayDegrees。
    for scene_rel, cs_paths in [("effects/flying_effect.tscn", [FLYING]),
                                ("effects/air_strike_effect.tscn", [AIRSTRIKE, FLYING]),
                                ("effects/bullet_effect.tscn", [BULLET_EFFECT]),
                                ("effects/bombing_effect.tscn", [BULLET_EFFECT]),
                                ("effects/tank_attack_effect.tscn", [BULLET_EFFECT]),
                                ("effects/smoke_effect.tscn", [SMOKE]),
                                ("effects/smoke_small_effect.tscn", [SMOKE]),
                                ("effects/convert_effect.tscn", [CONVERT, FLYING])]:
        cs_text = "\n".join(p.read_text(encoding="utf-8") for p in cs_paths)
        scene_text = (ROOT / scene_rel).read_text(encoding="utf-8")
        names = exports_of(cs_text)

        # ① 中文说明钉在 C# 的 `/// <summary>` 上——这是唯一留得住的地方。
        #    场景里的 `;` 注释在编辑器保存一次之后会被抹掉（已实测），钉它等于钉一个假的绿。
        lacking = lacking_chinese_doc(cs_text, names)
        results.append(check(not lacking,
                             f"{scene_rel}：{len(names)} 个 Export（含继承）都有中文 /// 说明（缺 {len(lacking)} 项）"))
        for n in lacking:
            print(f"       没有中文说明: {n}")

        # ② 场景里写的属性名必须真实存在（拼错的话 Godot 静默忽略，最难查）
        unknown = unknown_scene_props(scene_text, names)
        results.append(check(not unknown, f"{scene_rel}：属性名都真实存在（可疑: {unknown}）"))

        # ③ 场景上的 `;` 注释**只提示、不作为失败**：它属于「编辑器还没碰过这个文件」的
        #    临时状态，下一次在 Godot 里保存就会消失，拿去当断言只会制造假的红灯。
        leftover = uncommented_exports(scene_text, names)
        if leftover:
            print(f"       [提示] {scene_rel} 里暂时没有 `;` 注释的项（编辑器保存后本来就会没）: {leftover}")

    # ==================== ④ airstrike ====================
    # 需求：投弹要夹在飞掠中间（起飞 → 投弹 → 降落），不是「飞完全程再投弹」。
    # 靠 `attackEffect = flying,bombing` 拼不出来——那种写法只能一个演完接一个，
    # 所以另开一个**继承 FlyingEffect** 的空袭特效，只覆写「停留」那一段。
    print("\n--- ④ airstrike 空袭（飞掠 + 盘旋投弹）---")
    airstrike = AIRSTRIKE.read_text(encoding="utf-8")
    airstrike_scene = AIRSTRIKE_SCENE.read_text(encoding="utf-8")

    results.append(check("class AirStrikeEffect : FlyingEffect" in airstrike,
                         "继承 FlyingEffect（运动代码只有一份，不复制）"))
    results.append(check("protected override Task DuringRiseAsync(" in airstrike,
                         "只覆写「起飞时并行的那件事」"))
    results.append(check("=> StrikeAsync(positions, count);" in airstrike,
                         "投弹与起飞**同时**开跑（实机反馈：等起飞演完再投太晚）"))
    airstrike_code = code_only(airstrike)
    results.append(check("StayAsync" not in airstrike_code,
                         "不再覆写悬停那一段——投弹已经挪到起飞阶段"))
    # 断言按**完整方法签名**查，不能只查 `RiseAsync`——父类那个钩子叫 `DuringRiseAsync`，
    # 子串匹配会把「覆写钩子」误判成「抄了一份升起实现」。
    results.append(check("private async Task RiseAsync(" not in airstrike_code
                         and "private async Task LandAsync(" not in airstrike_code
                         and "private async Task SwayAsync(" not in airstrike_code,
                         "升起/降落/摆动的实现没有搬过来（搬了就是第二份实现）"))

    # 父类必须留出这个钩子，而且三段顺序要显式可见
    rise_hook = method(flying, "protected virtual Task DuringRiseAsync(", "private async Task RiseAsync(")
    results.append(check("=> Task.CompletedTask;" in rise_hook,
                         "父类默认「起飞时不附带任何事」"))
    results.append(check("protected virtual Task DuringRiseAsync(" in flying, "起飞并行的钩子是 virtual"))
    play_body = method(flying, "Task during = DuringRiseAsync(source", "await StayAsync(source")
    results.append(check("Task during = DuringRiseAsync(source, positions, count);" in play_body
                         and "Task.WhenAll(RiseAsync(source" in play_body,
                         "起飞与那件事 WhenAll 等在一起（不会留着没跑完就降落）"))
    stay_hook = method(flying, "protected virtual async Task StayAsync(", "private async Task SwayAsync(")
    results.append(check("await SwayAsync(card, baseRotation, stayDuration);" in stay_hook,
                         "父类默认实现仍是「摆幅非 0 才摆」"))
    results.append(check("protected virtual async Task StayAsync(" in flying, "父类的悬停阶段是 virtual"))
    after = method(flying, "Task.WhenAll(RiseAsync(source", "await LandAsync(source")
    results.append(check("await StayAsync(source, positions, baseRotation," in after,
                         "三段顺序：升起（含并行的事）→ 悬停 → 落回"))

    # 投弹是「子特效」，弹数/时长仍在 bombing 场景里配，这里只决定何时开投。
    # 「取 → 进树 → 传参 → 播 → 还」那一套**已收到注册表上**（`EffectRegistry.PlayOnceAsync`）：
    # BulletEffect 的发射/落点烟、以及 battlefield_ 的 `convert` 指令都要同一套，
    # 所以现在**只有一份实现**（规范 A 的「先找已有功能、适合改造就改造」）。
    child_fn = method(effect, "public static async Task PlayOnceAsync(", "\n    }")
    results.append(check("Effect effect = Create(name);" in child_fn,
                         "子特效复用已注册的特效，不重写弹道"))
    results.append(check("await effect.Play(positions, null, source, count);" in child_fn,
                         "count（= 攻击力）原样传给子特效"))
    results.append(check("Release(effect)" in child_fn, "子特效用完回收"))
    results.append(check("host.AddChild(effect);" in child_fn and "effect.Configure(argument);" in child_fn
                         and "effect.PrepareForUse();" in child_fn,
                         "顺序与 StartEffect 一致：先进树、再传参、再 PrepareForUse、最后才开演"))
    results.append(check("catch (Exception exception)" in child_fn,
                         "子特效出问题只报警不抛出（调用方通常还有降落等活要干）"))

    results.append(check("protected Task PlayChildEffectAsync(string effectName" in effect
                         and "=> EffectRegistry.PlayOnceAsync(this, effectName, null, positions, null, count);" in effect,
                         "Effect.PlayChildEffectAsync 只是转调，自己不再写一遍"))
    results.append(check("=> PlayChildEffectAsync(StrikeEffectName, positions, count);" in airstrike,
                         "AirStrikeEffect 只把子特效名交给基类，自己不再抄一份"))
    results.append(check("EffectRegistry.Create" not in airstrike_code
                         and "EffectRegistry.Release" not in airstrike_code,
                         "AirStrikeEffect 里已经没有第二份「取/还子特效」了"))
    results.append(check("[Export] public string StrikeEffectName" in airstrike,
                         "子特效名是 Export"))
    results.append(check('StrikeEffectName = "bombing"' in airstrike, "默认投的是 bombing"))

    results.append(check('path="res://core_logic/AirStrikeEffect.cs"' in airstrike_scene,
                         "场景挂了 AirStrikeEffect"))
    results.append(check('bus = &"SFX"' in airstrike_scene, "场景里有一个 SFX 总线上的播放器"))
    results.append(check("SwaySecondsPerCycle = 0.5" in airstrike_scene,
                         "盘旋时间减半（1.0 -> 0.5 秒）"))

    card_ini = CARD_INI.read_text(encoding="utf-8-sig")
    results.append(check("flying,bombing" not in card_ini,
                         "全项目不再有 flying,bombing 这种拼接写法"))

    # ==================== ④b 飞机的**兵种默认**攻击动画 ====================
    # 毛驴的 strafe 与伊尔2M 的 airstrike 原本是**这两张卡各自**的配置，
    # 现在提升成兵种默认 —— 13 张飞机卡一个 attackEffect 都不用写，新加的飞机也自动有。
    # 所以断言从「某张卡写了什么」改成「按兵种解析出什么」。
    print("\n--- ④b 飞机默认：按兵种解析 ---")
    defaults = method(battle, "private static string DefaultAttackEffect(cardBase_ from)", "\n    }")
    results.append(check("CardTypes.Plane => PlaneStrikeEffect" in defaults,
                         "战斗机默认 = PlaneStrikeEffect"))
    results.append(check("CardTypes.Bomber => BomberStrikeEffect" in defaults,
                         "轰炸机默认 = BomberStrikeEffect"))
    results.append(check("CardTypes.Tank or CardTypes.Artillery => TankAttackEffectFor(from)" in defaults,
                         "坦克与火炮走的还是原来那条（没被这次改动碰到）"))
    results.append(check("_ => null" in defaults,
                         "步兵/指令/总部没有默认（返回 null，不播特效）"))
    results.append(check('PlaneStrikeEffect = "strafe";' in battle
                         and 'BomberStrikeEffect = "airstrike";' in battle,
                         "两条默认都是常量（不在 switch 里裸写特效名）"))
    results.append(check("return DefaultAttackEffect(from);" in battle,
                         "ResolveAttackEffect 的兜底改调它"))

    # 在 Python 里按源码那张表**重放一遍**——测的是规则，不是文本
    plane_default = re.search(r'PlaneStrikeEffect = "(.+?)";', battle).group(1)
    bomber_default = re.search(r'BomberStrikeEffect = "(.+?)";', battle).group(1)

    def resolve(card_type, own_effect=""):
        if own_effect.strip():
            return own_effect
        if card_type == "Plane":
            return plane_default
        if card_type == "Bomber":
            return bomber_default
        return None   # 坦克/火炮要算槽位，步兵等没有默认 —— 这条重放只管飞机

    for card_type, own, want, note in [
        ("Plane", "", "strafe", "战斗机没写 → 扫射"),
        ("Bomber", "", "airstrike", "轰炸机没写 → 投弹"),
        ("Plane", "bullet", "bullet", "卡上写了就以卡为准（哪怕写的是别的）"),
    ]:
        results.append(check(resolve(card_type, own) == want,
                             f"{card_type}（卡上 {'空' if not own else own}）→ {want}（{note}）"))

    # 13 张飞机卡现在**一个 attackEffect 都不用写**
    plane_cards = [p for p in re.split(r"(?m)(?=^\[)", card_ini)
                   if re.search(r"^cardType[ \t]*=[ \t]*(Plane|Bomber)[ \t]*\r?$", p, re.MULTILINE)]
    leftover = [p.split("\n")[0] for p in plane_cards
                if re.search(r"^attackEffect[ \t]*=[ \t]*\S", p, re.MULTILINE)]
    results.append(check(len(plane_cards) >= 13,
                         f"飞机卡共 {len(plane_cards)} 张"))
    results.append(check(not leftover,
                         f"飞机卡都清空了 attackEffect、吃兵种默认（残留 {leftover}）"))

    # 开火声的联动：默认动画换了，静音名单**不能跟着一起换** ——
    # strafe 打的就是子弹（要那声机枪），airstrike 扔的是炸弹（配机枪是串味）。
    # 这两条上一轮实机调过，改默认时最容易被顺手弄坏。
    # （⑤ 里另有一份同样的重放，这里自己读一遍是为了不依赖那一段的执行顺序。）
    muted_names = set(re.findall(r'"(\w+)"', method(effect, "HashSet<string> NoFiringSoundNames",
                                                    "public static bool ReplacesFiringSound")))
    results.append(check(not (plane_default in muted_names),
                         f"战斗机默认 {plane_default} **不在**静音名单里（保留机枪声）"))
    results.append(check(bomber_default in muted_names,
                         f"轰炸机默认 {bomber_default} **在**静音名单里（只有飞掠声）"))


    # ==================== ⑤ 谁不放通用开火声 ====================
    # 实机反馈两轮，方向是反的，所以这张表**按结果命名**而不是按原因：
    #   · 第一轮「投弹时听到哒哒声」→ airstrike 不放（炸弹配机枪是串味）；
    #   · 第二轮「strafe 开火时也要机枪声」→ strafe **放**（它打的就是子弹）。
    # 也就是说「自带音效」并不等于「不要开火声」——strafe 自带飞掠声，照样要那声机枪。
    print("\n--- ⑤ 谁不放通用开火声 ---")
    results.append(check("public static bool ReplacesFiringSound(string effectNames)" in effect,
                         "注册表提供「这串特效名放不放开火声」查询"))
    results.append(check("HashSet<string> NoFiringSoundNames" in effect,
                         "表名按结果命名（不放通用开火声的特效名）——不叫「自带音效」，那个说法已经不成立"))
    results.append(check("SelfVoicedNames" not in code_only(effect),
                         "旧名 SelfVoicedNames 已彻底移除（留着就是两个说法打架）"))
    # 判定读的是 `ResolveAttackEffect(from)` 而**不是** `from.attackEffect`：
    # 坦克/火炮卡上根本没写 attackEffect（吃默认），读原文会拿到空串，
    # `ReplacesFiringSound(null)` 返回 false，于是又叠一层通用机枪声。
    results.append(check("string resolvedAttackEffect = ResolveAttackEffect(from);" in battle,
                         "攻击声判定用的是**解析后**的特效串，不是卡上的原文"))
    results.append(check("if (!EffectRegistry.ReplacesFiringSound(resolvedAttackEffect))" in battle,
                         "攻击时按**攻击者**的攻击特效决定放不放开火声"))
    sound_block = method(battle, "// 攻击声一共三种可能", "// 标记单位已经攻击")
    results.append(check(sound_block.count("PlayBattleSound(1);") == 1
                         and "TankCannonSoundEffect(from, resolvedAttackEffect)" in sound_block,
                         "三种攻击声在**一处**分支里选完（通用机枪声只留一个出口）"))
    results.append(check(battle.count("PlayBattleSound(1);") == 1,
                         "全文件只有一处放通用开火声（没有漏掉的第二处）"))

    # 交叉核对：静音名单里的名字必须真的指向一个挂了 AudioStreamPlayer 的场景。
    # 否则这张表会变成一句假话——写了名字却根本不发声，等于白静音一场。
    muted = re.findall(r'"(\w+)"', method(effect, "HashSet<string> NoFiringSoundNames",
                                          "public static bool ReplacesFiringSound"))
    results.append(check(bool(muted), f"静音名单非空（{muted}）"))
    for name in muted:
        rel = scene_paths(effect).get(name)
        results.append(check(rel is not None
                             and "AudioStreamPlayer" in (ROOT / rel.replace("res://", "")).read_text(encoding="utf-8"),
                             f"{name} 确实自带播放器（不是写了个空名字）"))

    # 反向：扫射**必须**保留通用开火声 —— 它打的就是子弹，那声机枪正是主人要的。
    # 这条是本次需求的核心，单独钉死；以后有人「顺手统一一下」把它加进名单会立刻红。
    results.append(check("strafe" not in muted,
                         "strafe 不在静音名单里（开火时要听得见机枪声）"))

    # ==================== ⑥ strafe 扫射（飞掠 + 打枪） ====================
    # 「起飞后发射子弹」不是新动画，只是 airstrike 换了个子特效——
    # AirStrikeEffect 的 StrikeEffectName 本来就是「起飞时打什么」的开关。
    # 这一节钉住「**同一份脚本、只换 Export**」，防止有人又写第二个类。
    print("\n--- ⑥ strafe 扫射（飞掠 + 打枪）---")
    strafe_scene = (ROOT / "effects" / "strafe_effect.tscn").read_text(encoding="utf-8")
    results.append(check("strafe" in paths, "注册了 strafe"))
    results.append(check('path="res://core_logic/AirStrikeEffect.cs"' in strafe_scene,
                         "strafe 复用 AirStrikeEffect 脚本（不写第二个类）"))
    results.append(check('StrikeEffectName = "bullet"' in strafe_scene,
                         "子特效换成 bullet（起飞即打枪）"))
    results.append(check("SwaySecondsPerCycle = 0.5" in strafe_scene,
                         "盘旋时间与 airstrike 一致"))
    # 「其他不变」：两个场景除了 StrikeEffectName 应当**完全一样**
    airstrike_lines = {l for l in airstrike_scene.split("\n") if l.strip() and not l.startswith(";")}
    strafe_lines = {l for l in strafe_scene.split("\n") if l.strip() and not l.startswith(";")}
    only_airstrike = airstrike_lines - strafe_lines
    only_strafe = strafe_lines - airstrike_lines
    results.append(check(all("StrikeEffectName" in l or "node name=" in l for l in only_airstrike | only_strafe),
                         f"两个场景只差子特效名与节点名（airstrike 独有: {only_airstrike}；strafe 独有: {only_strafe}）"))
    card_ini = CARD_INI.read_text(encoding="utf-8-sig")
    # 取固定长度窗口：按 `[` 切会切到段标题自己那一个（它就以 `[` 开头）
    # 毛驴这里**不再断言它写了 strafe** —— strafe 现在是战斗机的兵种默认
    # （见 ④b），卡上留着那一行反而是同一数据配两处。
    donkey = card_ini[card_ini.index("[毛驴]"):][:400]
    results.append(check(re.search(r"^attackEffect[ \t]*=[ \t]*\r?$", donkey, re.MULTILINE) is not None,
                         "毛驴清空了 attackEffect（吃战斗机的兵种默认 strafe）"))

    # ==================== 弹体池 ====================
    print("\n--- 弹体池按场景路径分池 ---")
    results.append(check("Dictionary<string, Queue<Bullet>> projectilePools" in pool,
                         "弹体池按场景路径分开"))
    results.append(check("AcquireBullet" not in pool and "ReleaseBullet" not in pool,
                         "旧的单一弹体池入口已移除"))
    results.append(check("leasedProjectiles[bullet] = scenePath;" in pool
                         and "leasedProjectiles.Remove(bullet, out string scenePath)" in pool,
                         "释放时按记录还回原来的池（不去猜 Godot 内部字段）"))
    results.append(check('"res://bin/bomb.tscn"' in pool, "航弹进预热清单"))
    prewarm = method(pool, "private async Task PrewarmRenderingAsync()", "private static T Instantiate<T>")
    results.append(check("foreach (string path in ProjectileScenePaths)" in prewarm,
                         "预渲染预热遍历所有弹体（只热子弹不管航弹等于白做）"))

    # ==================== 音效配置 ====================
    print("\n--- [sfx] flyby 槽位 ---")
    music_ini = MUSIC_INI.read_text(encoding="utf-8-sig")
    m = re.search(r"(?m)^flyby\s*=\s*(.+)$", music_ini)
    results.append(check(m is not None, "[sfx] 配了 flyby 槽位"))
    if m:
        path = m.group(1).strip()
        results.append(check((ROOT / path.replace("res://", "")).exists(), f"flyby → {path} 存在"))
        results.append(check("飞机飞过_单位" in path, "用的是 飞机飞过_单位 音效"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
