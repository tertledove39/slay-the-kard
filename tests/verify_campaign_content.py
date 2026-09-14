#!/usr/bin/env python3
from configparser import ConfigParser
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
AREA_COUNTS = {"area2": (6, 7), "area3": (6, 6), "area4": (6, 6), "area5": (6, 6), "area6": (6, 6), "area7": (7, 6)}
KILL_WINDOWS = {"area2": (18, 20), "area3": (19, 21), "area4": (23, 25), "area5": (25, 27), "area6": (27, 29), "area7": (29, 31)}
ACTION_RANGES = {"area2": (9, 9), "area3": (9, 9), "area4": (10, 10), "area5": (10, 11), "area6": (11, 11), "area7": (11, 12)}
ADD_START = {"area2": 8, "area3": 7, "area4": 6, "area5": 5, "area6": 5, "area7": 4}

def load(path):
    config = ConfigParser(interpolation=None, strict=True)
    config.optionxform = str
    config.read(path, encoding="utf-8")
    return config

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def main():
    events = load(ROOT / "bin" / "event.ini")
    battles = load(ROOT / "cards" / "enemyTurn.ini")
    areas = load(ROOT / "bin" / "AreaPool.ini")
    event_text = (ROOT / "bin" / "event.ini").read_text(encoding="utf-8")
    battle_text = (ROOT / "cards" / "enemyTurn.ini").read_text(encoding="utf-8")
    card_text = (ROOT / "cards" / "card.ini").read_text(encoding="utf-8")
    card_ids = set(re.findall(r"^\[([^]]+)\]", card_text, re.MULTILINE))
    event_card_ids = set(re.findall(r"replace(?:Random)?Card\(([^)]+)\)", event_text))
    enemy_card_ids = set(re.findall(r"addToEnemySupportLine\(([^)]+)\)", battle_text))
    results = [
        check(len(events.sections()) == 50, "event.ini contains exactly 50 events"),
        check(len(battles.sections()) == 50, "enemyTurn.ini contains exactly 50 battles"),
        check(event_card_ids <= card_ids, "all event rewards reference existing cards"),
        check(enemy_card_ids <= card_ids, "all battle deployments reference existing enemy cards"),
        check(not any("every" in key and value.startswith("ADD:") for section in battles.values() for key, value in section.items()), "periodic actions never stack permanent ADD actions"),
    ]
    pooled_events, pooled_battles = [], []
    for area, expected in AREA_COUNTS.items():
        # 与 WorldMap.LoadAreaPools 一致：只认 enemy*/entry* 开头的键，忽略 areaTimes 等元数据
        area_events = [value[6:] for key, value in areas[area].items() if key.startswith("entry") and value.startswith("event:")]
        area_battles = [value for key, value in areas[area].items() if key.startswith("enemy")]
        pooled_events.extend(area_events)
        pooled_battles.extend(area_battles)
        results.append(check((len(area_battles), len(area_events)) == expected, f"{area} has {expected[0]} battles and {expected[1]} events"))
        low, high = KILL_WINDOWS[area]
        kills = [int(re.search(r"t(\d+)", key).group(1)) for battle in area_battles for key, value in battles[battle].items() if "KillAllTargets" in value]
        results.append(check(len(kills) == len(area_battles) and all(low <= turn <= high for turn in kills), f"{area} deadlines match its difficulty window"))
        action_low, action_high = ACTION_RANGES[area]
        action_counts = [sum(1 for key in battles[battle] if key != "name") for battle in area_battles]
        results.append(check(all(action_low <= count <= action_high for count in action_counts), f"{area} action density matches its difficulty tier"))
        add_actions = [(key, value) for battle in area_battles for key, value in battles[battle].items() if value.startswith("ADD:")]
        results.append(check(all(not key.startswith("every") and int(re.search(r"t(\d+)", key).group(1)) >= ADD_START[area] for key, value in add_actions), f"{area} permanent pressure starts at a safe turn"))
        results.append(check(all(sum(1 for value in battles[battle].values() if value.startswith("ADD:")) == 1 for battle in area_battles), f"{area} has exactly one permanent growth effect per battle"))
        debuff_count = sum(1 for battle in area_battles for value in battles[battle].values() if any(token in value for token in ("|damage(", "|LoseAttack(", "AddTrait(Suppressed)")))
        results.append(check(debuff_count >= len(area_battles) // 2, f"{area} includes meaningful debuff pressure"))
    results.extend([
        check(set(pooled_events) == set(events.sections()) and len(pooled_events) == 50, "area2-7 distribute every event exactly once"),
        check(set(pooled_battles) == set(battles.sections()) and len(pooled_battles) == 50, "area2-7 distribute every battle exactly once"),
        check(all(battles[battle].get("name", "").strip() for battle in battles.sections()), "all battles have display names in enemyTurn.ini"),
        check(all(2 <= sum(1 for key in events[event] if key.startswith("choice") and key.endswith("_effect")) <= 3 for event in events.sections()), "every event has two or three choices"),
        check(all("[icon=" in value and ",description=" in value for section in battles.values() for key, value in section.items() if key != "name"), "every battle action has intent metadata"),
    ])
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
