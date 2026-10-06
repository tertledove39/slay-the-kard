#!/usr/bin/env python3
"""阵亡爆炸音效：在一批爆炸 wav 里随机播。

需求：爆炸音效改成在 `爆炸3.wav`～`爆炸21.wav` 里随机播，**下划线开头的那几个不要用**
（`assest/_爆炸16.wav`、`_爆炸17.wav`、`_爆炸19.wav` 是未采用的版本）。

实现复用了 `configs/music.ini` 已有的「槽位 + 逗号分隔 + 随机抽一条」机制，
只是把音效放在独立的 `[sfx]` 段，避免和 BGM 的「播完再切」调度混在一起。

顺带修掉一类错：场景里引用了不存在的音频文件（`battleField.tscn` 曾引用
`res://assest/机枪.wav`，实际只有 `机枪_低.wav` / `机枪_高.wav`），
Godot 每次加载都报 `Resource file not found` 但游戏照跑，很容易被忽略。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
MUSIC_INI = ROOT / "configs" / "music.ini"
MUSIC_CS = ROOT / "core_logic" / "MusicManager.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"

# 需求给的素材范围：爆炸3 ~ 爆炸21，下划线开头的排除
RANGE_FIRST, RANGE_LAST = 3, 21
PREFIX = "爆炸"
SUFFIX = ".wav"
ASSEST = ROOT / "assest"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


def read_slot(path, section, key):
    """从 ini 里取某个段某个键的原始值（值里含中文与逗号，自己拆比走 configparser 直观）。"""
    text = path.read_text(encoding="utf-8-sig")
    m = re.search(rf"(?ms)^\[{re.escape(section)}\](.*?)(?=^\[|\Z)", text)
    if not m:
        return None
    body = m.group(1)
    m2 = re.search(rf"(?m)^{re.escape(key)}\s*=\s*(.*)$", body)
    return m2.group(1).strip() if m2 else None


def expected_files():
    """按需求规则算出应当使用的文件集合，顺带把违规文件单独列出来。"""
    usable, excluded = [], []
    for n in range(RANGE_FIRST, RANGE_LAST + 1):
        plain = f"{PREFIX}{n}{SUFFIX}"
        underscored = f"_{PREFIX}{n}{SUFFIX}"
        if (ASSEST / plain).exists():
            usable.append(plain)
        if (ASSEST / underscored).exists():
            excluded.append(underscored)
    return usable, excluded


def main():
    results = []
    music_ini = MUSIC_INI.read_text(encoding="utf-8-sig")
    music_cs = MUSIC_CS.read_text(encoding="utf-8")

    # ==================== 配置侧 ====================
    print("--- [sfx] dead 槽位 ---")
    results.append(check("[sfx]" in music_ini, "configs/music.ini 有 [sfx] 段"))
    dead = read_slot(MUSIC_INI, "sfx", "dead")
    results.append(check(dead is not None and dead != "", "[sfx] 段配了 dead 槽位"))
    listed = [p.strip() for p in (dead or "").split(",") if p.strip()]

    usable, excluded = expected_files()
    results.append(check(len(usable) > 0, f"assest 里存在 {len(usable)} 条可用的爆炸音"))
    results.append(check(len(excluded) > 0, f"assest 里存在 {len(excluded)} 条下划线开头的（应排除）"))

    # 核心规则：下划线开头的一个都不能进来
    bad = [p for p in listed if Path(p).name.startswith("_")]
    results.append(check(not bad, f"清单里没有下划线开头的文件（违规 {len(bad)} 条）"))
    for b in bad:
        print(f"       不该列进来: {b}")

    # 清单必须刚好等于「范围 ∩ 存在 ∩ 非下划线」——既不漏，也不多
    listed_names = sorted(Path(p).name for p in listed)
    results.append(check(
        listed_names == sorted(usable),
        f"清单恰好等于 爆炸{RANGE_FIRST}~爆炸{RANGE_LAST} 里可用的那 {len(usable)} 条"
        f"（实际 {len(listed_names)} 条）",
    ))
    missing = set(usable) - set(listed_names)
    extra = set(listed_names) - set(usable)
    for m in sorted(missing):
        print(f"       漏了: {m}")
    for e in sorted(extra):
        print(f"       多了/不存在: {e}")

    # 清单里的每条都必须真的存在，否则又是一种「不报错但没声音」
    absent = [p for p in listed if not (ROOT / p.replace("res://", "")).exists()]
    results.append(check(not absent, f"清单里的文件都真实存在（缺 {len(absent)} 条）"))
    for a in absent:
        print(f"       找不到: {a}")

    results.append(check("res://assest/" + PREFIX in dead, "清单用的是 res:// 路径"))

    # ==================== 代码侧 ====================
    print("\n--- MusicManager 的复用实现 ---")
    results.append(check("public AudioStream PickSfx(string slot)" in music_cs, "MusicManager 提供 PickSfx(slot)"))
    results.append(check("Dictionary<string, string[]> sfxPaths" in music_cs, "音效槽位单独一张表，不与 BGM 混用"))
    load = method(music_cs, "private void LoadConfig()", "/// <summary>把一个段里的每个键读成一个槽位")
    results.append(check('LoadSection(ini, "sfx", sfxPaths);' in load, "LoadConfig 读了 [sfx] 段"))
    results.append(check('LoadSection(ini, "music", slotPaths);' in load, "[music] 段仍走同一段解析（没有第二份实现）"))
    results.append(check("private static void LoadSection(" in music_cs, "两个段共用 LoadSection"))
    pick = method(music_cs, "public AudioStream PickSfx(string slot)", "public void StopMusic()")
    results.append(check("sfxStreamCache" in pick, "音效流有缓存，不重复读盘"))
    results.append(check("return null;" in pick, "槽位缺失/加载失败时返回 null，让调用方保留原音效"))

    print("\n--- PlayDeadSound 接入 ---")
    battle = BATTLE.read_text(encoding="utf-8")
    results.append(check('private const string DeadSfxSlot = "dead";' in battle, "槽位名是具名常量"))
    play = method(battle, "void PlayDeadSound(int id)", "/// <summary>\n/// 点击后获得的当前理应正在选中的卡")
    results.append(check("PickSfx(DeadSfxSlot)" in play, "PlayDeadSound 从该槽位取音效"))
    results.append(check("if (stream != null)" in play, "取不到时不覆盖原音效（不会变成没声音）"))
    results.append(check(
        play.index("deadSound.Stream = stream") < play.index("deadSound.Play()"),
        "先换流再播，不会播到上一条",
    ))

    # ==================== 顺带修的那类错 ====================
    print("\n--- 场景引用的音频文件必须真实存在 ---")
    bad_refs = []
    for tscn in ROOT.rglob("*.tscn"):
        if ".godot" in tscn.parts or any(p.startswith("data_") for p in tscn.parts):
            continue
        text = tscn.read_text(encoding="utf-8-sig", errors="replace")
        for m in re.finditer(r'\[ext_resource type="AudioStream"[^\]]*path="res://([^"]+)"', text):
            if not (ROOT / m.group(1)).exists():
                bad_refs.append(f"{tscn.relative_to(ROOT)} -> res://{m.group(1)}")
    results.append(check(not bad_refs, f"所有 .tscn 引用的 AudioStream 都存在（缺 {len(bad_refs)} 条）"))
    for b in bad_refs:
        print(f"       {b}")

    results.append(check("res://assest/机枪.wav" not in
                         (ROOT / "bin" / "battleField.tscn").read_text(encoding="utf-8-sig"),
                         "battleField.tscn 不再引用不存在的 机枪.wav"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
