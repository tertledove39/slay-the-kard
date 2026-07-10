#!/usr/bin/env python3
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    battlefield = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    results = [
        check('&& !instruction.StartsWith("AddPointMax", StringComparison.OrdinalIgnoreCase)' in battlefield,
              "AddPointMax effects do not also execute AddPoint"),
        check("if(point + i >= pointMax) {point = pointMax;}" in battlefield,
              "current command points are capped at the current maximum"),
        check("player1.AddPoint(-pendingLoss);" in battlefield,
              "pending next-turn point loss uses the Player point update path"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
