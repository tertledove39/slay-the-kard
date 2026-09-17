#!/usr/bin/env python3
"""战役内容一致性验证

只验证引擎层面的事实约束，不约束关卡的设计风格与难度取舍。

覆盖：
- 冒烟测试：三份配置可解析，关卡文件非空
- 引用完整性：区域池的 enemy*/entry* 引用、关卡内引用的卡牌ID全部可解析
- 元数据完整性：每个关卡有 name，每条行动都带 [icon=...,description=...]
- 键格式：只允许 name/tN/everyNt/default/ADD，且永久前缀必须写 ADD: 冒号
- 区域一致性：AreaPool 的 section 与 AreaOrder 严格为 area1-area7
- 孤立内容：列出未被任何区域引用的关卡（提示信息，不计失败）
"""
from configparser import ConfigParser
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

EVENT_INI = ROOT / "bin" / "event.ini"
BATTLE_INI = ROOT / "cards" / "enemyTurn.ini"
AREA_INI = ROOT / "bin" / "AreaPool.ini"
CARD_INI = ROOT / "cards" / "card.ini"
STATE_CS = ROOT / "bin" / "CardRestoration.cs"

KEY_PATTERN = re.compile(r"^(t\d+|every\d+t|default|ADD)$")
ACTION_KEYS = re.compile(r"^(t\d+|every\d+t|default)$")
METADATA = re.compile(r"\[icon=[^\]]*?,\s*description=[^\]]*?\]")


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def load(path):
    """解析INI；解析失败时返回None并把原因交给调用方报告。"""
    config = ConfigParser(interpolation=None, strict=True)
    config.optionxform = str
    try:
        config.read(path, encoding="utf-8")
    except Exception as exc:  # 重复键、格式错误等
        return None, f"{path.name} 解析失败: {exc}"
    return config, None


def main():
    missing = [p.name for p in (EVENT_INI, BATTLE_INI, AREA_INI, CARD_INI, STATE_CS) if not p.exists()]
    if missing:
        print(f"[FAIL] 冒烟测试：缺少文件 {missing}")
        return 1

    events, err_e = load(EVENT_INI)
    battles, err_b = load(BATTLE_INI)
    areas, err_a = load(AREA_INI)
    parse_errors = [e for e in (err_e, err_b, err_a) if e]
    if parse_errors:
        print("[FAIL] 冒烟测试：配置解析失败")
        for err in parse_errors:
            print(f"       {err}")
        return 1

    card_ids = set(re.findall(r"^\[([^]]+)\]", CARD_INI.read_text(encoding="utf-8"), re.M))
    area_order = re.search(r"AreaOrder\s*=\s*\{(.*?)\}", STATE_CS.read_text(encoding="utf-8"), re.S)

    results = [
        check(len(battles.sections()) > 0, f"冒烟：enemyTurn.ini 解析出 {len(battles.sections())} 个关卡"),
        check(len(events.sections()) > 0, f"冒烟：event.ini 解析出 {len(events.sections())} 个事件"),
    ]

    # --- 区域一致性 ---
    expected_areas = [f"area{i}" for i in range(1, 8)]
    results.append(
        check(sorted(areas.sections()) == expected_areas, "区域池 section 严格为 area1-area7")
    )
    if area_order:
        declared = re.findall(r'"([^"]+)"', area_order.group(1))
        results.append(check(declared == expected_areas, f"AreaOrder 与区域池一致（{declared}）"))
    else:
        results.append(check(False, "CardRestoration.cs 中未找到 AreaOrder"))

    # --- 引用完整性 ---
    referenced_battles, referenced_events = set(), set()
    dangling_battles, dangling_events = [], []
    for area in expected_areas:
        if area not in areas:
            continue
        for key, value in areas[area].items():
            if key.startswith("enemy"):
                referenced_battles.add(value)
                if value not in battles.sections():
                    dangling_battles.append(f"{area}.{key}={value}")
            elif key.startswith("entry"):
                event_id = value[len("event:"):] if value.startswith("event:") else value
                referenced_events.add(event_id)
                if event_id not in events.sections():
                    dangling_events.append(f"{area}.{key}={value}")

    results.append(
        check(not dangling_battles, f"区域池的关卡引用全部可解析（悬空 {len(dangling_battles)} 个）")
    )
    for item in dangling_battles:
        print(f"       悬空引用: {item}")
    results.append(
        check(not dangling_events, f"区域池的事件引用全部可解析（悬空 {len(dangling_events)} 个）")
    )
    for item in dangling_events:
        print(f"       悬空引用: {item}")

    # --- 关卡内引用的卡牌ID ---
    referenced_cards = set()
    for section in battles.sections():
        for value in battles[section].values():
            referenced_cards.update(re.findall(r"addTo(?:Enemy)?SupportLine\(([^)]+)\)", value))
    unknown_cards = sorted(c for c in referenced_cards if c not in card_ids)
    results.append(
        check(not unknown_cards, f"关卡部署的单位全部是已存在的卡牌（未知 {len(unknown_cards)} 个）")
    )
    for card in unknown_cards:
        print(f"       未知卡牌ID: {card}")

    # --- 元数据完整性 ---
    unnamed = [s for s in battles.sections() if not battles[s].get("name", "").strip()]
    results.append(check(not unnamed, f"每个关卡都有 name（缺失 {len(unnamed)} 个）"))
    for section in unnamed:
        print(f"       缺少 name: {section}")

    # 意图面板按「每条行动」读取一个 icon，因此以整行值为单位检查，
    # 不要求用逗号分隔的每个子动作各自携带元数据。
    bad_metadata = []
    for section in battles.sections():
        for key, value in battles[section].items():
            if ACTION_KEYS.match(key) and value.strip() and not METADATA.search(value):
                bad_metadata.append(f"{section}.{key}: {value.strip()[:60]}")
    results.append(
        check(not bad_metadata, f"每条行动都带 [icon=...,description=...] 元数据（缺失 {len(bad_metadata)} 条）")
    )
    for item in bad_metadata[:10]:
        print(f"       缺少元数据: {item}")

    # --- 键格式 ---
    bad_keys, equals_prefix = [], []
    for section in battles.sections():
        for key, value in battles[section].items():
            if key != "name" and not KEY_PATTERN.match(key):
                bad_keys.append(f"{section}.{key}")
            if re.match(r"^ADD[=]", value.strip()):
                equals_prefix.append(f"{section}.{key}")
    results.append(check(not bad_keys, f"关卡键名格式合法（非法 {len(bad_keys)} 个）"))
    for item in bad_keys:
        print(f"       非法键名: {item}")
    results.append(
        check(not equals_prefix, f"永久行动前缀统一写作 ADD:（写成 ADD= 的 {len(equals_prefix)} 条）")
    )
    for item in equals_prefix:
        print(f"       ADD= 前缀不会被执行，实际失效: {item}")

    # --- 提示信息：未被任何区域引用的关卡 ---
    orphans = [s for s in battles.sections() if s not in referenced_battles]
    if orphans:
        print(f"\n[INFO] {len(orphans)} 个关卡未被任何区域引用，游戏内不可达：")
        print("       " + ", ".join(orphans))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
