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
    card = (ROOT / "bin" / "cardBase_.cs").read_text(encoding="utf-8")
    store = (ROOT / "Store.cs").read_text(encoding="utf-8")
    su76 = re.search(r"\[su76m\](.*?)(?=\n\[|\Z)", cards, re.DOTALL)
    su76_text = su76.group(1) if su76 else ""
    refresh = card.split("public void RefreshState()", 1)[1].split("private void BuildAttributePanel()", 1)[0]
    results = [
        check("cardType    = Tank" in su76_text or "cardType = Tank" in su76_text, "su76m is configured as a Tank"),
        check("SetCardInformation(cardData)" in store, "store refreshes persistent card display nodes from CardData"),
        check("attackLabel.Visible = !isCommand && !isHeadquarters" in refresh and "defenceLabel.Visible = !isCommand" in refresh, "refresh assigns stat visibility for every card category"),
        check('res://cards/卡背_command.png' in refresh and 'res://cards/卡背1.png' in refresh, "refresh assigns both command and normal unit frames"),
        check("iconSprite.Visible = !isHeadquarters" in refresh and "unitTypeSprite.Visible = !isHeadquarters" in refresh, "unit presentation visibility is restored after reuse"),
        check("costLabel.Visible = !isHeadquarters" in refresh and "nameLabel.Visible = !isHeadquarters" in refresh, "normal card labels are restored after reuse"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
