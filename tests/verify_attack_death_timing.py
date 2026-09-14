#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    attack = battle.split("public async Task Attack", 1)[1].split("async Task Move", 1)[0]
    death = battle.split("private async Task ProcessDeadUnitAsync", 1)[1].split("public async void OnNextTurnButtonPressed", 1)[0]
    results = [
        check("private bool PlayAttackEffect" in battle and "return StartEffect" in battle, "attack effect reports whether a visual started"),
        check("private bool StartEffect" in battle and "private async Task RunEffect" in battle, "effect startup is synchronous while playback remains asynchronous"),
        check("SceneTreeTimer attackPresentationTimer = null;" in attack, "combat owns one presentation window"),
        check("attackPresentationTimer ??= GetTree().CreateTimer(0.5)" in attack, "the first started combat effect opens a 500ms window"),
        check(attack.count("attackPresentationTimer ??=") == 3, "ambush, attack, and counterattack share the same window"),
        check(attack.rindex("await ToSignal(attackPresentationTimer") < attack.rindex("ResumeDeathCheck()"), "death checks resume only after the attack window"),
        check('StartEffect("smoke"' in death and "CreateTimer(0.5)" not in death, "non-combat death handling has no attack delay"),
        check("return false" in battle.split("private bool PlayAttackEffect", 1)[1].split("private bool StartEffect", 1)[0], "missing or zero-attack visuals do not add a delay"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
