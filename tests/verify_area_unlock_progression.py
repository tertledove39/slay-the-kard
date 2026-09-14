#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    state = (ROOT / "bin" / "CardRestoration.cs").read_text(encoding="utf-8")
    world = (ROOT / "bin" / "WorldMap.cs").read_text(encoding="utf-8")
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    event = (ROOT / "bin" / "EventScene.cs").read_text(encoding="utf-8")
    results = [
        check("public static Dictionary<string, int> UnlockedArea" in state, "UnlockedArea is shared across scenes"),
        check(state.count('["area') == 7 and '["area1"] = 1' in state and '["area2"] = 0' in state, "only area1 is initially unlocked"),
        check("public static void AdvanceArea(string areaName)" in state and "UnlockedArea[areaName] = 0" in state, "progression closes the current area"),
        check("UnlockedArea[AreaOrder[index + 1]] = 1" in state, "progression unlocks the next ordered area"),
        check("BattleStateManager.UnlockedArea.TryGetValue(pair.Key" in world, "WorldMap renders shared UnlockedArea values"),
        check("BattleStateManager.AdvanceArea(BattleStateManager.SelectedArea)" in battle, "battle victory advances UnlockedArea"),
        check("BattleStateManager.AdvanceArea(_areaName)" in event, "event completion advances UnlockedArea"),
        check("foreach (string area in AreaOrder)" in state and "UnlockedArea[area] = 1" in state, "unlockall writes every UnlockedArea value"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
