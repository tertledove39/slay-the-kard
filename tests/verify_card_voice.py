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
    results.append(check("StartEffect(card.playEffect," in battle, "playEffect 走 StartEffect"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
