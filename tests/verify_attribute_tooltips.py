#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    card = (ROOT / "bin" / "cardBase_.cs").read_text(encoding="utf-8")
    handler = card.split("public override void _Input(InputEvent @event)", 1)[1].split("\n    }", 1)[0]
    results = [
        check("public override void _Input(InputEvent @event)" in card and "public override void _GuiInput(InputEvent @event)" not in card, "attribute hover bypasses GUI interception"),
        check("InputEventMouseMotion" in handler and "AcceptEvent" not in handler, "tooltip tracking does not consume drag or click input"),
        check("GetLocalMousePosition()" in handler and "mousePos - panelPos" in handler, "attribute hit testing uses card-local coordinates"),
        check("_hoveredAttributeIndex = -1;" in card.split("private void BuildAttributePanel()", 1)[1].split("var attrs = newAttrs;", 1)[0], "panel rebuild resets the hovered attribute"),
        check("cardType == CardTypes.Command ? new List<EffectAttribute>()" in card, "command cards clear stale pooled attribute panels"),
        check("!IsVisibleInTree()" in handler and "HideAttributeTooltip()" in card, "hidden pooled cards cannot retain tooltips"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
