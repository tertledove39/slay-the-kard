#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    end = (ROOT / "End.cs").read_text(encoding="utf-8")
    defeat_method = battle.split("private async Task ReturnToStartMenuAfterDefeat()", 1)[1].split("\n    }", 1)[0]
    results = [
        check("card == null) return" in battle, "card removal handles null before card access"),
        check("card.GetIsFriend() == IsFriend.friend && card.isHq == HQ.hq" in battle, "friendly HQ destruction starts defeat flow"),
        check("defeatTransitionStarted" in battle and "!defeatTransitionStarted" in battle, "defeat transition starts only once"),
        check("public async Task ShowDefeat()" in end, "End exposes an awaitable defeat interface"),
        check('img.Modulate = new Color(0.35f, 0.35f, 0.35f, 1f)' in end, "defeat emblem is gray"),
        check('Text = "战斗失败"' in end and 'Text = "返回主菜单"' in end, "defeat UI waits for a return button"),
        check("await completion.Task" in end, "defeat flow waits for player input"),
        check('await endNode.ShowDefeat()' in defeat_method and 'res://bin/start_menu.tscn' in defeat_method, "defeat proceeds directly to the start menu"),
        check("PostBattleReward" not in defeat_method and "AdvanceArea" not in defeat_method and "CalculateMaterialPoints" not in defeat_method, "defeat grants no victory completion or rewards"),
        check("img.Modulate = Colors.White" in end, "victory display resets the emblem color"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
