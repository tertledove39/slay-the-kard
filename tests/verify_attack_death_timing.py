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
    # 阵亡表现（停留 → 消失 → 冒烟 + 爆炸声）自成一个方法，**不被 await**：
    # 它有自己的 1 秒停留，但那段时间不该拖住阵亡检查的时序。
    presentation = battle.split("private async Task PlayDeathPresentationAsync", 1)[1].split("\n    private ", 1)[0]
    results = [
        check("private bool PlayAttackEffect" in battle and "return StartEffect" in battle, "attack effect reports whether a visual started"),
        check("private bool StartEffect" in battle and "private async Task RunEffect" in battle, "effect startup is synchronous while playback remains asynchronous"),
        check("SceneTreeTimer attackPresentationTimer = null;" in attack, "combat owns one presentation window"),
        check("attackPresentationTimer ??= GetTree().CreateTimer(0.5)" in attack, "the first started combat effect opens a 500ms window"),
        check(attack.count("attackPresentationTimer ??=") == 3, "ambush, attack, and counterattack share the same window"),
        check(attack.rindex("await ToSignal(attackPresentationTimer") < attack.rindex("ResumeDeathCheck()"), "death checks resume only after the attack window"),
        check("CreateTimer(0.5)" not in death and "attackPresentationTimer" not in death,
              "non-combat death handling does not borrow the attack window"),
        check('StartEffect("smoke"' in presentation and "PlayDeadSound(1);" in presentation,
              "smoke and the explosion moved into the death presentation"),
        check("await PlayDeathPresentationAsync" not in death and "_ = PlayDeathPresentationAsync(" in death,
              "death presentation is fire-and-forget（阵亡检查不等它）"),
        check("CreateTimer(DeathPresentationDelaySeconds)" in presentation,
              "the stay-on-field pause uses its own constant, not the attack window"),
        check("return false" in battle.split("private bool PlayAttackEffect", 1)[1].split("private bool StartEffect", 1)[0], "missing or zero-attack visuals do not add a delay"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
