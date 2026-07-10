#!/usr/bin/env python3
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    store = (ROOT / "Store.cs").read_text(encoding="utf-8")
    results = [
        check("private void UpdateHoveredCard()" in store,
              "store tracks the currently hovered purchasable card"),
        check("_cards[index].SetHover(hovered);" in store,
              "store cards reuse the hand card hover style"),
        check("_cardPositions[index] + new Vector2(0, hovered ? -CardRaise : 0)" in store,
              "store cards raise using the shared selection interaction"),
        check("if (!BattleStateManager.StoreCurrentSlots[i].IsSold" in store,
              "sold cards do not receive the purchase hover effect"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
