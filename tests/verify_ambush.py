#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def main():
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    attack_start = battle.index("bool attackerHasShock = from.HasShockActive();")
    attack_end = battle.index("PlayBattleSound(1);", attack_start)
    attack = battle[attack_start:attack_end]

    results = [
        check("ambushTriggered = !attackerHasShock && to.HasAmbushActive()" in attack, "ambush triggers independently of counter type restrictions"),
        check("attackerKilledByAmbush = from.ReadDefence() <= 0" in attack, "ambush kill is detected before attack damage"),
        check("if (!attackerKilledByAmbush)" in attack and "await to.LoseDefence(attackDamage)" in attack, "attack damage is skipped when ambush kills the attacker"),
        check("if (!attackerHasShock && !ambushTriggered" in attack, "normal counter only runs when ambush was not triggered"),
        check("to.UseAmbush()" in attack and attack.index("to.UseAmbush()") < attack.index("from.LoseDefence"), "ambush is consumed before dealing counter damage"),
        check("attackerHasShock" in attack and "from.RemoveShock()" in attack, "shock is consumed and bypasses ambush"),
        check("ambushTriggered" not in battle[battle.index("PlayBattleSound(1);", attack_start):], "no duplicate ambush check after attack resolves"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())