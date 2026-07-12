#!/usr/bin/env python3
from configparser import ConfigParser
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
AREA_COUNTS = {"area2": (6, 7), "area3": (6, 6), "area4": (6, 6), "area5": (6, 6), "area6": (6, 6), "area7": (6, 6), "area8": (7, 6), "area9": (7, 7)}
KILL_WINDOWS = {"area2": (18, 20), "area3": (19, 21), "area4": (21, 23), "area5": (23, 25), "area6": (25, 27), "area7": (27, 29), "area8": (29, 31), "area9": (31, 34)}

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
    names = (ROOT / "bin" / "HistoricalBattleNames.cs").read_text(encoding="utf-8")
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
        values = list(areas[area].values())
        area_events = [value[6:] for value in values if value.startswith("event:")]
        area_battles = [value for value in values if not value.startswith("event:")]
        pooled_events.extend(area_events)
        pooled_battles.extend(area_battles)
        results.append(check((len(area_battles), len(area_events)) == expected, f"{area} has {expected[0]} battles and {expected[1]} events"))
        low, high = KILL_WINDOWS[area]
        kills = [int(re.search(r"t(\d+)", key).group(1)) for battle in area_battles for key, value in battles[battle].items() if "KillAllTargets" in value]
        results.append(check(len(kills) == len(area_battles) and all(low <= turn <= high for turn in kills), f"{area} deadlines match its difficulty window"))
    results.extend([
        check(set(pooled_events) == set(events.sections()) and len(pooled_events) == 50, "area2-9 distribute every event exactly once"),
        check(set(pooled_battles) == set(battles.sections()) and len(pooled_battles) == 50, "area2-9 distribute every battle exactly once"),
        check(all(f'{{ "{battle}",' in names for battle in battles.sections()), "all battles have Chinese display names"),
        check(all(2 <= sum(1 for key in events[event] if key.startswith("choice") and key.endswith("_effect")) <= 3 for event in events.sections()), "every event has two or three choices"),
    ])
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
