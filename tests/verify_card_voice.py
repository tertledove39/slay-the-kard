#!/usr/bin/env python3
"""卡牌语音：打出某张卡时喊一声。

链条一共四环，缺一环就「打了没声」而且很难查：

    cards/card.ini   playEffect = sfx(严冬)
        │
        ├─ battlefield_.StartEffect  拆名字 → 得到特效名 `sfx` 与参数「严冬」
        │                            （拆分必须用括号感知的 SplitEffectString）
        ├─ EffectRegistry.ParseName  拆 `名字(参数)`
        ├─ EffectRegistry.Create     取到 effects/sound_effect.tscn
        ├─ Effect.Configure("严冬")  参数交给特效（**必须在 AddChild 之后**）
        └─ SoundEffect.Play          MusicManager.PickSfx("严冬") → configs/music.ini 的 [sfx] 段

这个脚本把每一环都钉住，并做**交叉核对**：卡里写的每个槽位，在 `[sfx]` 段里都得有，
且指向的文件得真的存在——写错一个名字就静默没声，靠肉眼看是看不出来的。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
EFFECT = ROOT / "core_logic" / "Effect.cs"
SOUND_EFFECT = ROOT / "core_logic" / "SoundEffect.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
MUSIC_INI = ROOT / "configs" / "music.ini"
CARD_INI = ROOT / "cards" / "card.ini"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


def sfx_slots(ini_text):
    """`[sfx]` 段里的 槽位名 -> 文件路径列表。"""
    section = ini_text.split("[sfx]", 1)[1]
    slots = {}
    for line in section.split("\n"):
        line = line.strip()
        if not line or line.startswith(";") or line.startswith("["):
            continue
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        slots[key.strip()] = [p.strip() for p in value.split(",") if p.strip()]
    return slots


def main():
    effect = EFFECT.read_text(encoding="utf-8")
    sound_effect = SOUND_EFFECT.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    music_ini = MUSIC_INI.read_text(encoding="utf-8-sig")
    card_ini = CARD_INI.read_text(encoding="utf-8-sig")
    results = []

    # ==================== ① 特效系统支持「名字带参数」 ====================
    print("--- ① 特效名可以带参数 ---")
    results.append(check("public virtual void Configure(string argument) { }" in effect,
                         "Effect 基类有 Configure 钩子（不需要参数的特效忽略即可）"))
    parse = method(effect, "public static void ParseName(string raw, out string name, out string argument)",
                   "public static Effect Create(string name)")
    results.append(check("indexOf('(')" in parse.replace("IndexOf('(')", "indexOf('(')")
                         and 'EndsWith(")")' in parse,
                         "ParseName 认 `名字(参数)` 这种写法"))
    results.append(check("argument = null;" in parse, "不带括号时参数为 null（而不是空串）"))

    # 拆分必须用括号感知的那个，否则参数里出现逗号会被拆坏
    start = method(battle, "private bool StartEffect(string effectNames", "private async Task RunEffect(")
    results.append(check("SplitEffectString(effectNames, ',')" in start,
                         "StartEffect 用括号感知的 SplitEffectString 拆分（参数里带逗号也不会坏）"))
    results.append(check("effectNames.Split(EffectNameSeparator" not in start,
                         "没有退回朴素的 Split（那个不认括号）"))
    results.append(check("EffectRegistry.ParseName(raw, out string name, out string argument);" in start,
                         "每个名字都过一遍 ParseName"))
    results.append(check("effect.Configure(argument);" in start, "参数交给特效"))

    # 顺序是刻意的：Configure 里可能要 GetNode，那时节点必须已经进树
    add_pos = start.index("AddChild(effect);")
    configure_pos = start.index("effect.Configure(argument);")
    prepare_pos = start.index("effect.PrepareForUse();")
    results.append(check(add_pos < configure_pos < prepare_pos,
                         "顺序是 AddChild → Configure → PrepareForUse（反了 Configure 里 GetNode 会拿到 null）"))

    # ==================== ② 音效特效本身 ====================
    print("\n--- ② SoundEffect ---")
    paths = dict(re.findall(r'\["(\w+)"\]\s*=\s*"([^"]+)"',
                            method(effect, "Dictionary<string, string> ScenePaths", "public static void ParseName")))
    results.append(check("sfx" in paths, "注册了 sfx 特效"))
    if "sfx" in paths:
        rel = paths["sfx"].replace("res://", "")
        scene = (ROOT / rel)
        results.append(check(scene.exists(), f"注册表里的 sfx → {paths['sfx']} 真实存在"))
        text = scene.read_text(encoding="utf-8")
        results.append(check('path="res://core_logic/SoundEffect.cs"' in text, "场景挂了 SoundEffect 脚本"))
        results.append(check('bus = &"SFX"' in text, "播放器走 SFX 总线（与其它战斗音效一致）"))
        results.append(check("mouse_filter = 2" in text, "特效节点不挡鼠标"))

    results.append(check("public override void Configure(string argument)" in sound_effect,
                         "SoundEffect 覆写了 Configure 来收槽位"))
    results.append(check("MusicManager.Instance?.PickSfx(slot)" in sound_effect,
                         "用槽位名去 [sfx] 段取音频（与 flyby / dead 同一套）"))
    # 关键：调用方的 finally 会立刻回收特效节点，不等放完的话声音会被掐掉
    play = method(sound_effect, "public override async Task Play(", "\n}")
    results.append(check("await ToSignal(player, AudioStreamPlayer.SignalName.Finished);" in play,
                         "**等音效放完才返回**（否则 RunEffect 的 finally 会把播放中的播放器一起删掉）"))
    results.append(check("GD.PushWarning" in play, "槽位缺失/取不到时留日志（否则表现成「打了没声」）"))

    # ==================== ③ 音效配置 ====================
    print("\n--- ③ configs/music.ini 的 [sfx] 段 ---")
    slots = sfx_slots(music_ini)
    for name in ["严冬", "战略重心", "红色旗帜", "嘿", "阿嘿", "朱可夫", "拉伸", "预备役"]:
        results.append(check(name in slots, f"[sfx] 配了槽位「{name}」"))
    # 每个槽位指向的文件都得真的在
    for name, files in sorted(slots.items()):
        for f in files:
            results.append(check((ROOT / f.replace("res://", "")).exists(),
                                 f"{name} → {f} 存在"))
    # 新增的 wav 必须被 Godot 导入过：缺 .import 时 ResourceLoader 直接取不到，
    # 表现是「配置全对但就是没声」，只跑 Python 测试根本看不出来。
    for name, files in sorted(slots.items()):
        for f in files:
            src = ROOT / f.replace("res://", "")
            if src.suffix.lower() in (".wav", ".ogg", ".mp3"):
                results.append(check(Path(str(src) + ".import").exists(),
                                     f"{name} 的 {src.name} 已被 Godot 导入（有 .import）"))

    # ==================== ④ 卡牌接线（交叉核对） ====================
    print("\n--- ④ 卡牌接线 ---")
    wired = {}
    for section in re.split(r"(?m)^\[", card_ini):
        if not section.strip():
            continue
        card = section.split("]")[0].strip()
        m = re.search(r"(?m)^playEffect\s*=\s*sfx\((.+?)\)\s*$", section)
        if m:
            wired[card] = m.group(1).strip()

    # 喀秋莎也在列：它的 playEffect 是「进入阵地」，进场与移动共用（见 ⑧ 与 ⑦）。
    # 严冬 被两张卡共用（冬季攻势 与 冬季战争）—— 一个槽位可以被多张卡引用，这是允许的。
    expected = {"冬季攻势": "严冬", "冬季战争": "严冬", "战略重心": "战略重心",
                "五年计划": "红色旗帜", "塔曼斯卡亚": "嘿", "方面军": "阿嘿",
                "朱可夫": "朱可夫", "拖拉机厂": "拉伸", "预备役": "预备役",
                "喀秋莎": "katyusha_into_pos"}
    for card, slot in sorted(expected.items()):
        results.append(check(wired.get(card) == slot, f"{card} → sfx({slot})"))

    # 交叉核对：卡里写的槽位，[sfx] 段里必须真有 —— 写错一个字母就静默没声
    for card, slot in sorted(wired.items()):
        results.append(check(slot in slots, f"{card} 用的槽位「{slot}」在 [sfx] 段里存在"))
    results.append(check(len(wired) == len(expected),
                         f"接线的卡数与预期一致（实际 {len(wired)}，预期 {len(expected)}）"))

    # 打出卡牌的路径确实会调用 playEffect
    results.append(check("PlayCardEffect(commandCard);" in battle, "指令卡打出时播 playEffect"))
    results.append(check("PlayCardEffect(card);" in battle, "自动/敌方打出指令走同一条"))

    # ==================== ⑤ 指令卡的默认音「咚」 ====================
    # 94 张指令卡里只有 8 张自带语音，其余 86 张要一个默认音。
    # 做成**默认值**而不是在 86 张卡上各写一行：那是同一个值抄 86 遍（规范 E），
    # 而且以后每加一张指令卡都要记得补——漏了就静默没声。
    print("\n--- ⑤ 指令卡默认音「咚」 ---")
    results.append(check("private const string DefaultCommandPlayEffect = \"sfx(咚)\";" in battle,
                         "默认音是一个常量（不在方法里裸写字符串）"))
    play_card = method(battle, "private void PlayCardEffect(cardBase_ card)", "\n}")
    results.append(check("string effect = card.playEffect;" in play_card,
                         "先取卡上写的 playEffect（自带语音的那 8 张以卡为准）"))
    # 具体的兜底表达式在 ⑥ 里统一钉（那里同时管步兵那一支），这里只确认用的是这个常量
    results.append(check("DefaultCommandPlayEffect" in play_card, "指令卡兜底用的是那个常量"))
    results.append(check("StartEffect(effect," in play_card, "最终播的是兜底后的那一份"))
    results.append(check("咚" in slots, "[sfx] 段里配了「咚」槽位"))
    # 交叉核对：常量里写的槽位必须真的在 [sfx] 段里 —— 拼错就整批指令卡静默没声
    m = re.search(r'DefaultCommandPlayEffect = "sfx\((.+?)\)"', battle)
    results.append(check(m is not None and m.group(1) in slots,
                         f"常量指向的槽位「{m.group(1) if m else '?'}」在 [sfx] 段里存在"))
    # 默认值这条路要真的被用上：得有指令卡确实没写 playEffect
    defaulted = 0
    for section in re.split(r"(?m)^\[", card_ini):
        if not section.strip():
            continue
        if not re.search(r"(?m)^cardType\s*=\s*Command\s*$", section):
            continue
        if not re.search(r"(?m)^playEffect\s*=", section):
            defaulted += 1
    results.append(check(defaulted > 0, f"确实有指令卡没写 playEffect（{defaulted} 张走默认音）"))

    # ==================== ⑥ 步兵进场音（三档） ====================
    # 规则：attack + defence = 总身材；≤4 小 / 5~8 中 / ≥9 大。
    # 进场有两条路，但它们都汇聚到 PlayCardEffect（见该方法的注释），所以只需挂一处。
    print("\n--- ⑥ 进场音：步兵 / 坦克火炮 三档，飞机不分档 ---")
    results.append(check("private static string DeployMoveEffect(cardBase_ card)" in battle,
                         "有独立的档位判定函数"))
    tier_fn = method(battle, "private static string DeployMoveEffect(cardBase_ card)", "\n    }")
    results.append(check("if (card.isHq == HQ.hq) return null;" in tier_fn,
                         "排除总部（总部的 cardType 也是 Infantry）"))
    # 判档本身已经抽成 SizeTier：进场音、开火音、命中音三处共用同一个判据，
    # 边界只写一遍（规范 E）。所以这里查的是那条判据存在，而不是它长在哪个函数里。
    size_fn = method(battle, "private static string SizeTier(cardBase_ card)", "\n    }")
    results.append(check("card.ReadAttack() + card.ReadDefence()" in size_fn,
                         "按 attack + defence 判档（判据在 SizeTier 里，只有一份）"))
    results.append(check("return $\"sfx({family}_{SizeTier(card)})\";" in tier_fn,
                         "进场音调 SizeTier 拿档位，不自己再算一遍边界"))
    results.append(check("CardTypes.Infantry => InfantryVoicePrefix" in tier_fn,
                         "步兵用 infantry 那一套素材"))
    results.append(check("CardTypes.Tank or CardTypes.Artillery => TankVoicePrefix" in tier_fn,
                         "**坦克与火炮**共用 tank 那一套素材"))
    # 飞机的兜底音抽成了 PlaneFallbackEffect：「哪些兵种算飞机、这一刻放哪条」只有那一张表。
    # 部署与移动两个调用点都走它，分开各写一遍就会出现两个说法。
    plane_fn = method(battle, "private static string PlaneFallbackEffect(cardBase_ card, bool forDeploy)",
                      "\n    }")
    results.append(check("PlaneFallbackEffect(card, forDeploy: true)" in tier_fn,
                         "部署路径走 PlaneFallbackEffect（飞机不分档这一条仍然成立）"))
    results.append(check("if (card.cardType is not (CardTypes.Plane or CardTypes.Bomber)) return null;" in plane_fn,
                         "战斗机与轰炸机**都**走这张表（主人后来说轰炸机也用这两条）"))
    results.append(check("return forDeploy ? PlaneDeployEffect : PlaneFlybyEffect;" in plane_fn,
                         "**部署与移动是两条不同的槽位**，按时机选而不是按兵种"))

    # 在 Python 里按源码那张表**重放一遍**——测的是规则，不是文本。
    slots_of = {c: re.search(rf'{c} = "sfx\((.+?)\)";', battle).group(1)
                for c in ("PlaneDeployEffect", "PlaneFlybyEffect")}

    def plane_fallback(card_type, for_deploy):
        if card_type not in ("Plane", "Bomber"):
            return None
        return slots_of["PlaneDeployEffect" if for_deploy else "PlaneFlybyEffect"]

    for card_type, for_deploy, want, note in [
        ("Plane", True, slots_of["PlaneDeployEffect"], "战斗机**部署**用 depl 那组"),
        ("Plane", False, slots_of["PlaneFlybyEffect"], "战斗机**移动**用 flyby 那组"),
        ("Bomber", True, slots_of["PlaneDeployEffect"], "轰炸机**部署**同战斗机"),
        ("Bomber", False, slots_of["PlaneFlybyEffect"], "轰炸机**移动**同战斗机"),
        ("Infantry", True, None, "步兵不归它管"),
        ("Infantry", False, None, "步兵不归它管"),
    ]:
        results.append(check(plane_fallback(card_type, for_deploy) == want,
                             f"{card_type} {'部署' if for_deploy else '移动'} -> {want or '不播'}（{note}）"))

    # 兜底要**真的轮得到**这 13 张卡：只要有一张飞机写了 playEffect，
    # 那张卡就会绕过整张表，改兜底等于对它无效。
    planes_with_own_voice = [p.split("\n")[0] for p in re.split(r"(?m)(?=^\[)", card_ini)
                             if re.search(r"^cardType[ \t]*=[ \t]*(Plane|Bomber)[ \t]*\r?$", p, re.MULTILINE)
                             and re.search(r"^playEffect[ \t]*=[ \t]*\S", p, re.MULTILINE)]
    results.append(check(not planes_with_own_voice,
                         f"没有飞机自己写了 playEffect（写了就会绕过兜底: {planes_with_own_voice}）"))
    results.append(check("InfantryVoiceSmallSlot" not in battle and "InfantryVoiceMediumSlot" not in battle,
                         "旧的「一档一个槽位常量」已收成前缀+后缀（否则加坦克要再抄一遍）"))

    # 边界按**需求方给的规则**验一遍：把常量读出来重新算一遍分档
    limits = {k: int(re.search(rf"{k} = (\d+);", battle).group(1))
              for k in ("DeploySoundSmallMax", "DeploySoundMediumMax")}
    def tier(total):
        if total <= limits["DeploySoundSmallMax"]:
            return "small"
        return "medium" if total <= limits["DeploySoundMediumMax"] else "large"

    results.append(check([tier(n) for n in (1, 4, 5, 8, 9, 18)]
                         == ["small", "small", "medium", "medium", "large", "large"],
                         f"分档边界符合规则（≤{limits['DeploySoundSmallMax']} 小 / "
                         f"5~{limits['DeploySoundMediumMax']} 中 / ≥{limits['DeploySoundMediumMax']+1} 大）"))

    # 槽位名是「前缀_档位」拼出来的，所以只要前缀与档位词对，所有槽位都对得上。
    # 逐个核一遍：家族 × 档位 的 6 个槽位 + 飞机那一条，都必须配了文件、且文件已被导入。
    families = {c: re.search(rf'{c} = "(.+?)";', battle) for c in ("InfantryVoicePrefix", "TankVoicePrefix")}
    results.append(check(all(v is not None for v in families.values()), "两个家族的槽位前缀都是常量"))
    wanted = [f"{v.group(1)}_{t}" for v in families.values() if v for t in ("small", "medium", "large")]
    planes = {c: re.search(rf'{c} = "sfx\((.+?)\)";', battle)
              for c in ("PlaneDeployEffect", "PlaneFlybyEffect")}
    results.append(check(all(v is not None for v in planes.values()),
                         "飞机那两条也是常量（不在方法里裸写槽位名）"))
    wanted += [v.group(1) for v in planes.values() if v]
    # 两条必须是**不同的槽位**：写重了就等于「部署与移动用同一条」，
    # 而主人给的素材本来就是按时机分成两组的。
    results.append(check(planes["PlaneDeployEffect"].group(1) != planes["PlaneFlybyEffect"].group(1),
                         "飞机部署与移动指向**不同**的槽位（写重了就等于没拆）"))
    for name in wanted:
        results.append(check(name in slots, f"槽位「{name}」在 [sfx] 段里存在"))
        for f in slots.get(name, []):
            src = ROOT / f.replace("res://", "")
            results.append(check(src.exists(), f"{name} → {src.name} 存在"))
            results.append(check(Path(str(src) + ".import").exists(),
                                 f"{name} 的 {src.name} 已被 Godot 导入"))

    # ==================== ⑦ 移动也响 ====================
    # 入场那一声走 PlayCardEffect（与部署分支互斥），**移动**那一声在 Move() 的 else 分支里。
    # 一次移动只响一声：卡上写了 playEffect 就用它（喀秋莎的「进入阵地」进场与移动共用一行），
    # 没写的飞机才放「飞过」。
    print("\n--- ⑦ 移动也响一声 ---")
    move_fn = method(battle, "private void PlayMoveEffect(cardBase_ card)", "\n    }")
    results.append(check("string effect = card.playEffect;" in move_fn,
                         "卡上有 playEffect 就用它（喀秋莎靠这一行同时覆盖进场与移动）"))
    results.append(check("effect = PlaneFallbackEffect(card, forDeploy: false);" in move_fn,
                         "没写的话走 PlaneFallbackEffect 的**移动**那一格（战斗机 = fighter_flyby）"))
    results.append(check("if (effect == null) return;" in move_fn
                         and "is not (CardTypes.Plane or CardTypes.Bomber)" not in move_fn,
                         "「非飞机不播」现在由 PlaneFallbackEffect 返回 null 表达，不再各判一遍兵种"))
    results.append(check(move_fn.index("card.playEffect") < move_fn.index("PlaneFallbackEffect"),
                         "两份音的优先级：卡上的 playEffect 先于飞机默认"))
    results.append(check("PlayPlaneMoveEffect" not in battle, "旧名 PlayPlaneMoveEffect 已不存在"))
    # 移动分支：确认它接在 Move() 的 else（非部署）里，且入场分支没有它
    move_body = battle[battle.index("async Task Move(cardBase_ card, place_ position)"):][:6000]
    deploy_branch = move_body[move_body.index("if (isDeployedFromHand)"):]
    else_branch = deploy_branch[deploy_branch.index("else"):]
    results.append(check("PlayMoveEffect(card);" in else_branch, "移动分支（else）里调了 PlayMoveEffect"))
    results.append(check("PlayMoveEffect(card);" not in deploy_branch[:deploy_branch.index("else")],
                         "部署分支里**没有**它 —— 两条互斥，不会连响两声"))

    # ==================== ⑧ 喀秋莎 ====================
    # 专属音效一个挂 attackEffect（攻击）、一个挂 playEffect（进场 + 移动）。
    print("\n--- ⑧ 喀秋莎专属音 ---")
    katyusha = card_ini[card_ini.index("[喀秋莎]"):][:600]
    # 攻击音挂在 attackEffect 上。**主人后来把 `bullet` 去掉了**（只响炮声、不出子弹视觉），
    # 所以这里只断言「带了这一条音效」，不锁死整串值——以后想加回视觉也不必改测试。
    results.append(check(re.search(r"(?m)^attackEffect\s*=.*sfx\(katyusha_fire\)", katyusha) is not None,
                         "攻击音挂在 attackEffect 上"))
    results.append(check("playEffect" in katyusha and "sfx(katyusha_into_pos)" in katyusha,
                         "进入阵地挂在 playEffect 上（进场与移动共用这一行）"))
    for name in ("katyusha_fire", "katyusha_into_pos"):
        results.append(check(name in slots, f"[sfx] 配了槽位「{name}」"))
        for f in slots.get(name, []):
            src = ROOT / f.replace("res://", "")
            results.append(check(src.exists(), f"{name} → {src.name} 存在"))
            results.append(check(Path(str(src) + ".import").exists(),
                                 f"{name} 的 {src.name} 已被 Godot 导入"))
    # 攻击特效里带了自定义音效，就不该再叠通用机枪声。
    # **这里必须验行为，不能只验名单里有没有 "sfx" 这个字符串** ——
    # 曾经就是这么漏的：名单里有 `sfx`，但判定拿的是**带参数的整串** `sfx(katyusha_fire)`，
    # 永远匹配不上，喀秋莎的机枪声一直没被静音，而文本断言照样是绿的。
    print("\n--- ⑧b 通用开火声的判定（验行为，不验文本） ---")
    suppress = method(effect, "public static bool ReplacesFiringSound(string effectNames)",
                      "public static Effect Create(string name)")
    results.append(check("ParseName(raw, out string name, out _);" in suppress,
                         "判定**先拆参数**再查名单"))
    results.append(check("NoFiringSoundNames.Contains(name)" in suppress,
                         "查的是拆出来的名字，不是整串"))
    results.append(check("NoFiringSoundNames.Contains(raw.Trim())" not in suppress,
                         "旧的「拿整串去查」写法已消失"))

    # 从源码里把名单和边界读出来，在 Python 里重放一遍判定——测的是规则，不是文本
    listed = set(re.findall(r'"(\w+)"', method(effect, "HashSet<string> NoFiringSoundNames",
                                               "public static bool ReplacesFiringSound")))

    def replaces(effect_names):
        """按 C# 的规则重放：逗号分段 -> 拆掉括号里的参数 -> 查名单。"""
        for raw in effect_names.split(","):
            name = raw.strip().split("(")[0].strip()
            if name in listed:
                return True
        return False

    cases = [
        ("bullet", False, "普通子弹卡照旧有通用开火声"),
        ("bullet,sfx(katyusha_fire)", True, "**喀秋莎**：带了自定义音效 -> 静音（曾经漏的就是这条）"),
        ("sfx(katyusha_fire)", True, "只写音效也是静音"),
        ("flying", True, "飞掠：扔炸弹，不该有机枪声"),
        ("airstrike", True, "空袭：同上"),
        ("strafe", False, "扫射：打的就是子弹，那声机枪正是它要的"),
    ]
    for value, want, why in cases:
        results.append(check(replaces(value) is want, f"「{value}」-> {'静音' if want else '保留机枪声'}：{why}"))

    # 挂载点：没写 playEffect 的走兜底；写了的一律以卡为准
    results.append(check("effect = card.cardType == CardTypes.Command ? DefaultCommandPlayEffect : DeployMoveEffect(card);"
                         in play_card,
                         "PlayCardEffect 里：指令卡用「咚」、其余兵种用各自的进场音"))
    cond = play_card.index("if (string.IsNullOrWhiteSpace(effect))")
    assign = play_card.index("effect = card.cardType == CardTypes.Command")
    results.append(check(cond < assign, "**卡上有 playEffect 就不兜底**——兜底在空白判断里面"))
    # 两条进场路径都走 PlayCardEffect
    results.append(check("PlayCardEffect(card);" in
                         method(battle, "async Task AddCardToPlace(cardBase_ card, place_ place)", "await TriggerUnitEffects(\"BeingAddedToField\""),
                         "效果刷进场（AddCardToPlace）走 PlayCardEffect"))
    # 取固定长度窗口：`Move` 很长，按「下一个方法名」切既脆又容易切错
    move = battle[battle.index("async Task Move(cardBase_ card, place_ position)"):][:6000]
    results.append(check("if (isDeployedFromHand)" in move and "PlayCardEffect(card);" in move,
                         "从手牌部署（Move）也走 PlayCardEffect，且只在 isDeployedFromHand 时"))
    results.append(check("GetMyPlace() == null" in move,
                         "isDeployedFromHand 判据：没有格子 = 从手牌来的"))

    # ==================== ⑨ 抽卡音 ====================
    # 五条变体（Draw_One_A~E）挂在 [sfx] 的 draw 槽位，靠该段本来就有的
    # 「逗号分隔、每次随机抽一条」实现随机——不用写任何抽签代码。
    # 难点在挂载：Player 里有**四条各自独立的抽牌实现**，得四处都挂。
    print("\n--- ⑨ 抽卡音 ---")
    results.append(check("private const string DrawSoundEffect = \"sfx(draw)\";" in battle,
                         "抽卡音是个常量"))
    results.append(check("public void PlayDrawSound() => StartEffect(DrawSoundEffect, null, null, null);" in battle,
                         "抽卡音有公共入口（Player 调 battlefield 的这个）"))
    results.append(check("draw" in slots, "[sfx] 段里配了 draw 槽位"))

    draw_files = slots.get("draw", [])
    results.append(check(len(draw_files) == 5, f"draw 是 5 条变体（实际 {len(draw_files)} 条）"))
    for f in draw_files:
        src = ROOT / f.replace("res://", "")
        results.append(check(src.exists(), f"draw → {src.name} 存在"))
        results.append(check(Path(str(src) + ".import").exists(),
                             f"draw 的 {src.name} 已被 Godot 导入"))
    results.append(check(all(re.search(r"Draw_One_[A-E]", f) for f in draw_files),
                         "五条都是 Draw_One_A~E（随机由 [sfx] 段的逗号分隔机制负责，无需额外代码）"))

    # 四处挂载点。Player 里四条抽牌实现都以 `deck.Remove*` 取牌，所以按这个定位：
    # 每一次从牌堆取牌之后都应该在附近调 PlayDrawSound —— 以后新加第 5 种抽法而忘了挂，这条会红。
    calls = battle.count("battlefield.PlayDrawSound();")
    results.append(check(calls == 4, f"四条抽牌路径各挂一次（实际 {calls} 处）"))
    takes = [m.start() for m in re.finditer(r"deck\.Remove(At)?\(", battle)]
    missing = [battle[t:t + 160] for t in takes
               if "battlefield.PlayDrawSound();" not in battle[t:t + 160]]
    results.append(check(not missing,
                         f"每一次「从牌堆取牌」之后都跟着抽卡音（漏挂 {len(missing)} 处）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
