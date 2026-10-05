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
BULLET = ROOT / "bin" / "Bullet.cs"
SMOKE = ROOT / "core_logic" / "SmokeEffect.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
CARD_BASE = ROOT / "bin" / "cardBase_.cs"
MUSIC_INI = ROOT / "configs" / "music.ini"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


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
    results.append(check("effectNames.Split(EffectNameSeparator" in start or
                         "Split(EffectNameSeparator" in start, "按分隔符拆分名字"))
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
    for name, path in sorted(paths.items()):
        results.append(check((ROOT / path.replace("res://", "")).exists(),
                             f"注册表里的 {name} → {path} 真实存在"))

    # ==================== ② flying ====================
    print("\n--- ② flying 飞掠 ---")
    for field in ["RiseHeight", "RiseScale", "SwayDegrees", "SwayCycles",
                  "Duration", "TopZIndex", "SfxSlot"]:
        results.append(check(f"[Export] public float {field}" in flying
                             or f"[Export] public int {field}" in flying
                             or f"[Export] public string {field}" in flying,
                             f"{field} 是 Export（调手感不必改代码）"))

    results.append(check("private async Task RiseAsync(" in flying
                         and "private async Task AimAsync(" in flying
                         and "private async Task SwayAroundAimAsync(" in flying
                         and "private async Task LandAsync(" in flying,
                         "升起 / 转向目标 / 在瞄准角上摆动 / 落回 四段分开"))
    results.append(check("baseScale * RiseScale" in flying, "漂浮时轻微放大"))
    results.append(check("basePosition + new Vector2(0f, -RiseHeight)" in flying, "往上升"))

    # 转向：幅度由「攻击者→目标」的方向决定，而不是固定角度
    aim = method(flying, "private static float AimRotationOffset(", "private async Task RiseAsync(")
    results.append(check("positions[1] - positions[0]" in aim, "方向取自 攻击者→目标"))
    results.append(check("direction.Angle() + Mathf.Pi / 2f" in aim,
                         "让卡的「上边」对准目标（与 Bullet 的朝向约定一致）"))
    results.append(check("direction.LengthSquared() < 0.0001f" in aim,
                         "攻击者与目标重合时不乱转"))

    set_ = method(flying, "private async Task AimAsync(", "private async Task SwayAroundAimAsync(")
    results.append(check('TweenProperty(card, "rotation", toRotation, duration)' in set_,
                         "转的是 rotation（不是左右平移）"))
    results.append(check("Tween.TransitionType.Sine" in set_ and "Tween.EaseType.InOut" in set_,
                         "转向是慢速 + 缓动"))

    sway = method(flying, "private async Task SwayAroundAimAsync(", "private async Task LandAsync(")
    results.append(check("SwayCycles * 2f" in sway, "摆动来回数由 SwayCycles 决定"))
    results.append(check("Mathf.DegToRad(SwayDegrees)" in sway, "摆幅是角度（DegToRad），不是像素"))
    results.append(check("aimRotation + (index % 2 == 0 ? offset : -offset)" in sway,
                         "摆动是「在瞄准角的基础上」左右偏，不是绕 0 度摆"))
    results.append(check('tween.TweenProperty(card, "rotation", aimRotation, step)' in sway,
                         "最后停在瞄准角上（落回才不会斜着停住）"))

    results.append(check("source.isUnderCardEffect = true;" in flying, "漂浮期间标记「特效在管这张卡」"))
    results.append(check("source.ZIndex = TopZIndex;" in flying, "抬高层级压住其他卡"))
    fin = method(flying, "finally", "private static float AimRotationOffset(")
    results.append(check("source.Position = basePosition;" in fin
                         and "source.Scale = baseScale;" in fin
                         and "source.Rotation = baseRotation;" in fin
                         and "source.ZIndex = baseZIndex;" in fin
                         and "source.isUnderCardEffect = false;" in fin,
                         "finally 里还原位置/缩放/角度/层级/标记（中途出错也不能让它永远浮着斜着）"))
    results.append(check("MusicManager.Instance?.PickSfx(SfxSlot)" in flying, "音效走 [sfx] 槽位"))
    results.append(check('SfxSlot = "flyby"' in flying, "默认槽位名 flyby"))

    flying_scene = (ROOT / "effects" / "flying_effect.tscn").read_text(encoding="utf-8")
    results.append(check('path="res://core_logic/FlyingEffect.cs"' in flying_scene, "场景挂了 FlyingEffect"))
    results.append(check("AudioStreamPlayer" in flying_scene and 'bus = &"SFX"' in flying_scene,
                         "场景里有一个 SFX 总线上的播放器"))
    results.append(check(re.search(r'Duration = [\d.]+', flying_scene) is not None,
                         "场景里写明了可调参数（值在场景里，不在代码里）"))

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
    results.append(check("[Export] public int StaggerMaxMs" in bullet_effect, "错开间隔也是 Export"))

    # 命中音效：每发各响一声，且要能叠着响
    results.append(check("[Export] public string ImpactSfxSlot" in bullet_effect, "命中音效槽位是 Export"))
    results.append(check("[Export] public float ImpactSfxVolume" in bullet_effect, "命中音量是 Export"))
    results.append(check("[Export] public int ImpactVoiceCount" in bullet_effect, "声部数是 Export"))
    results.append(check("new AudioStreamPlayer[ImpactVoiceCount]" in bullet_effect,
                         "按声部数建多个播放器（一个播放器会让后一声掐掉前一声）"))
    results.append(check('Bus = SfxBus' in bullet_effect and 'SfxBus = "SFX"' in bullet_effect,
                         "命中音效走 SFX 总线，与战场既有战斗音效一致"))
    results.append(check("Mathf.LinearToDb(Mathf.Clamp(ImpactSfxVolume, 0.0001f, 1f))" in bullet_effect,
                         "线性音量换算成 dB"))
    results.append(check("string.IsNullOrWhiteSpace(ImpactSfxSlot)" in bullet_effect,
                         "槽位为空就不建播放器（bullet 不受影响）"))
    release = method(bullet_effect, "private async Task PlayAndReleaseBullet(", "\n}")
    results.append(check("await bullet.Play(positions, time);" in release
                         and release.index("PlayImpactSfx()") > release.index("await bullet.Play("),
                         "命中音效挂在「弹体飞抵」之后（bullet.Play 正是在那一刻返回）"))

    bombing_scene = (ROOT / "effects" / "bombing_effect.tscn").read_text(encoding="utf-8")
    results.append(check('path="res://core_logic/BulletEffect.cs"' in bombing_scene,
                         "bombing 复用 BulletEffect 脚本（不写第二个类）"))
    results.append(check('ProjectileScenePath = "res://bin/bomb.tscn"' in bombing_scene,
                         "bombing 用航弹弹体"))
    results.append(check("ProjectileCount = 0" in bombing_scene, "bombing 弹数交给攻击力决定"))
    results.append(check('ImpactSfxSlot = "dead"' in bombing_scene, "bombing 每发命中配爆炸音效"))
    results.append(check("ImpactSfxVolume = 0.7" in bombing_scene, "爆炸音效音量 70%"))
    bullets_scene = (ROOT / "effects" / "bullet_effect.tscn").read_text(encoding="utf-8")
    results.append(check("ImpactSfxSlot" not in bullets_scene,
                         "bullet 不写命中音效，行为与从前一致"))

    bomb_scene = (ROOT / "bin" / "bomb.tscn").read_text(encoding="utf-8")
    results.append(check('path="res://bin/Bullet.cs"' in bomb_scene, "航弹复用 Bullet 脚本（运动方式照 bullet）"))
    results.append(check("航弹.png" in bomb_scene, "航弹弹体用 航弹.png"))

    bullet_scene_path = next(p for n, p in scene_paths(effect).items() if n == "bullet")
    results.append(check("ProjectileCount" not in
                         (ROOT / bullet_scene_path.replace("res://", "")).read_text(encoding="utf-8"),
                         "bullet 场景不写弹数，保持脚本默认的 10 发（行为不变）"))

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
