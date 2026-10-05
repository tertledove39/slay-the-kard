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

    expected = {"冬季攻势": "严冬", "战略重心": "战略重心", "五年计划": "红色旗帜",
                "塔曼斯卡亚": "嘿", "方面军": "阿嘿", "朱可夫": "朱可夫",
                "拖拉机厂": "拉伸", "预备役": "预备役"}
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
    print("\n--- ⑥ 步兵进场音（三档） ---")
    results.append(check("private static string InfantryDeployEffect(cardBase_ card)" in battle,
                         "有独立的档位判定函数"))
    tier_fn = method(battle, "private static string InfantryDeployEffect(cardBase_ card)",
                     "\n    }")
    results.append(check("card.cardType != CardTypes.Infantry || card.isHq == HQ.hq" in tier_fn,
                         "只对步兵生效，且排除总部（总部的 cardType 也是 Infantry）"))
    results.append(check("card.ReadAttack() + card.ReadDefence()" in tier_fn,
                         "按 attack + defence 判档"))
    results.append(check("InfantryVoiceSmallSlot" in tier_fn and "InfantryVoiceMediumSlot" in tier_fn
                         and "InfantryVoiceLargeSlot" in tier_fn,
                         "三档各自的槽位名都走常量（不在方法里裸写）"))

    # 边界按**需求方给的规则**验一遍：把常量读出来重新算一遍分档
    limits = {k: int(re.search(rf"{k} = (\d+);", battle).group(1))
              for k in ("InfantryVoiceSmallMax", "InfantryVoiceMediumMax")}
    def tier(total):
        if total <= limits["InfantryVoiceSmallMax"]:
            return "small"
        return "medium" if total <= limits["InfantryVoiceMediumMax"] else "large"

    results.append(check([tier(n) for n in (1, 4, 5, 8, 9, 18)]
                         == ["small", "small", "medium", "medium", "large", "large"],
                         f"分档边界符合规则（≤{limits['InfantryVoiceSmallMax']} 小 / "
                         f"5~{limits['InfantryVoiceMediumMax']} 中 / ≥{limits['InfantryVoiceMediumMax']+1} 大）"))

    # 三条音频得配上，槽位名还得和代码常量对得上
    for const in ("InfantryVoiceSmallSlot", "InfantryVoiceMediumSlot", "InfantryVoiceLargeSlot"):
        name = re.search(rf'{const} = "(.+?)";', battle)
        results.append(check(name is not None and name.group(1) in slots,
                             f"{const} 指向的槽位「{name.group(1) if name else '?'}」在 [sfx] 段里存在"))
    for name in ("infantry_small", "infantry_medium", "infantry_large"):
        for f in slots.get(name, []):
            src = ROOT / f.replace("res://", "")
            results.append(check(src.exists(), f"{name} → {src.name} 存在"))
            results.append(check(Path(str(src) + ".import").exists(),
                                 f"{name} 的 {src.name} 已被 Godot 导入"))

    # 挂载点：没写 playEffect 的步兵走兜底；写了的一律以卡为准
    results.append(check("effect = card.cardType == CardTypes.Command ? DefaultCommandPlayEffect : InfantryDeployEffect(card);"
                         in play_card,
                         "PlayCardEffect 里：指令卡用「咚」、步兵用进场音"))
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

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
