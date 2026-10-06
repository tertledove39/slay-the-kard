#!/usr/bin/env python3
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    world_map = (ROOT / "bin" / "WorldMap.cs").read_text(encoding="utf-8")
    state = (ROOT / "bin" / "CardRestoration.cs").read_text(encoding="utf-8")
    battlefield = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    results = [
        check("if (BattleStateManager.IsCardDataCached) return;" in world_map,
              "cached card data skips card.ini parsing"),
        check("if (BattleStateManager.IsEventDataCached) return;" in world_map,
              "cached event data skips event.ini parsing"),
        check("var cachedPools = BattleStateManager.GetCachedAreaPools();" in world_map and
              "BattleStateManager.CacheAreaPools(_areaPools);" in world_map,
              "area pool parsing is cached across map scenes"),
        check("IsEventDataCached" in state and "CacheAreaPools" in state,
              "BattleStateManager owns return-map configuration caches"),
        check("CreateTimer(2.5f)" not in battlefield,
              "victory return does not add a fixed 2.5-second delay"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
