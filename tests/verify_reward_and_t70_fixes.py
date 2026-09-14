#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    cards = (ROOT / "cards" / "card.ini").read_text(encoding="utf-8")
    reward = (ROOT / "bin" / "PostBattleReward.cs").read_text(encoding="utf-8")
    t70_match = re.search(r"\[t70_机动防御\](.*?)(?=\n\[|\Z)", cards, re.DOTALL)
    command_match = re.search(r"\[机动防御\](.*?)(?=\n\[|\Z)", cards, re.DOTALL)
    t70 = t70_match.group(1) if t70_match else ""
    command = command_match.group(1) if command_match else ""
    results = [
        check("下个友方回合结束时" in command and "下个友方回合结束时" in t70, "mobile defense descriptions match next friendly turn end"),
        check("FriendlyTurnEnd:if(&lifeTime==0)Jump|this|KillAllTarget|Jump&" in t70, "new T-70 skips its creation-turn destruction"),
        check("if(&lifeTime>0)Jump" not in t70, "reversed T-70 lifetime condition is removed"),
        check("private const float CardDisplayScale = 1.0f;" in reward, "reward cards use full native scale"),
        check("new ScrollContainer" in reward and "VerticalScrollMode = ScrollContainer.ScrollMode.Auto" in reward, "reward groups are vertically scrollable"),
        check("content.CustomMinimumSize" in reward, "scroll content exposes its full height"),
        check("CreateGroupCardRow(groups[g], startX, gy, content)" in reward and "content.AddChild(btn)" in reward, "cards and group buttons share scroll content"),
        check('AddChild(skipBtn);' in reward and 'content.AddChild(skipBtn)' not in reward, "skip button remains fixed outside scrolling content"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
