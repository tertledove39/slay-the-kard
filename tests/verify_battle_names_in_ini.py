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
        check(len(config.sections()) > 0, f"enemyTurn.ini contains battle presets ({len(config.sections())})"),
        check(all(config[section].get("name", "").strip() for section in config.sections()), "every battle preset contains a name"),
        check(config["berlin_final_battle"]["name"] == "攻克柏林", "final battle uses the approved display name"),
        check(not (ROOT / "bin" / "HistoricalBattleNames.cs").exists(), "HistoricalBattleNames.cs is deleted"),
        check("HistoricalBattleNames" not in world and "LoadBattleNames()" in world, "WorldMap reads battle names from enemyTurn.ini"),
        check("GD.PushError" in world and "enemyName = id" in world, "missing names report an error and fall back to section ID"),
        check('if (key == "name") continue;' in battle, "battlefield excludes name metadata from action queues"),
        check("EnemyDisplayNames" not in state, "legacy enemy display-name dictionary is removed"),
        # 非战役模式回退 berlin 的行为不变，但已从 battlefield_ 的内联三元式收敛到 BattleStateManager，
        # 战斗脚本加载与战斗专属BGM共用同一个解析入口，故断言改在其当前位置。
        check('public const string DefaultEnemyPreset = "berlin";' in state
              and 'return IsCampaignMode ? SelectedEnemy : DefaultEnemyPreset;' in state,
              "noncampaign berlin fallback remains unchanged"),
        check("BattleStateManager.ResolveEnemyPreset()" in battle and ': "berlin";' not in battle,
              "battlefield resolves the preset through BattleStateManager instead of inlining it"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
