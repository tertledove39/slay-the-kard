#!/usr/bin/env python3
from configparser import ConfigParser
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    config = ConfigParser(interpolation=None, strict=True)
    config.optionxform = str
    config.read(ROOT / "cards" / "enemyTurn.ini", encoding="utf-8")
    world = (ROOT / "bin" / "WorldMap.cs").read_text(encoding="utf-8")
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    state = (ROOT / "bin" / "CardRestoration.cs").read_text(encoding="utf-8")
    results = [
        check(len(config.sections()) == 50, "enemyTurn.ini still contains 50 battle presets"),
        check(all(config[section].get("name", "").strip() for section in config.sections()), "every battle preset contains a name"),
        check(config["berlin_final_battle"]["name"] == "攻克柏林", "final battle uses the approved display name"),
        check(not (ROOT / "bin" / "HistoricalBattleNames.cs").exists(), "HistoricalBattleNames.cs is deleted"),
        check("HistoricalBattleNames" not in world and "LoadBattleNames()" in world, "WorldMap reads battle names from enemyTurn.ini"),
        check("GD.PushError" in world and "enemyName = id" in world, "missing names report an error and fall back to section ID"),
        check('if (key == "name") continue;' in battle, "battlefield excludes name metadata from action queues"),
        check("EnemyDisplayNames" not in state, "legacy enemy display-name dictionary is removed"),
        check(': "berlin";' in battle, "noncampaign berlin fallback remains unchanged"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
