#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def method(text, start, end):
    return text[text.index(start):text.index(end, text.index(start))]

def main():
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    card = (ROOT / "bin" / "cardBase_.cs").read_text(encoding="utf-8")
    arrow = (ROOT / "bin" / "Cardbase.cs").read_text(encoding="utf-8")
    store = (ROOT / "Store.cs").read_text(encoding="utf-8")
    death = method(battle, "async Task CheckIfAnyUnitDiedAsync()", "public async void OnNextTurnButtonPressed()")
    turn = method(battle, "public async void OnNextTurnButtonPressed()", "void RefreshAllCardInField()")
    move = method(card, "async public Task MoveToPosition", "async public Task DiscardCard")
    draw = method(arrow, "public override void _Draw()", "private bool IsValidPolygon")
    results = [
        check("deathCheckRunning" in death and "deathCheckRequested" in death, "death checks use a coalescing gate"),
        check("CheckIfAnyUnitDiedAsync(); // 递归" not in death, "death chains no longer recurse without awaiting"),
        check("await card.ExecChangeList()" in death, "change lists finish before death evaluation"),
        check('await TriggerUnitEffects("Dead"' in death, "Dead effects finish before removal"),
        check("turnTransitionRunning" in turn and "finally" in turn, "turn transitions reject re-entry and restore state"),
        check("await player1.DrawCard()" in turn and "await ApplyTurnStartTraits()" in turn, "turn state tasks remain in the serial chain"),
        check("private async Task ApplyEnemyTurnStartTraits()" in battle and "await ApplyEnemyTurnStartTraits()" in battle, "enemy trait changes are awaited"),
        check("DrawColoredPolygon(arrowBody" not in arrow and "DrawPolyline(centerCurve, Colors.Black, arrowWidth" in arrow, "arrow body avoids polygon triangulation"),
        check("new Vector2[" not in draw and "new Vector2[]" not in draw and "FillBezierCurve(centerCurve" in draw, "arrow drawing avoids per-frame curve arrays"),
        check("arrowBody" not in arrow and "upperCurve" not in arrow and "lowerCurve" not in arrow, "dynamic concave arrow polygons are absent"),
        check("trailEnd = arrowBase + mainDir * (arrowWidth / 2.0f)" in draw and "FillBezierCurve(centerCurve, p1, trailEnd" in draw, "arrow trail overlaps the arrow head"),
        check("DistanceSquaredTo(p2) < 4.0f" in draw, "near-zero arrows skip drawing"),
        check("nextP2.IsEqualApprox(p2)" in arrow, "stationary pointer does not redraw the arrow"),
        check("moveTween" in move and "moveTween.Kill()" in move, "card movement cancels the previous tween"),
        check("public override void _GuiInput" in card and "public override void _Input" not in card, "attribute tooltips use local GUI input"),
        check("_hoveredAttributeIndex != hoveredIndex" in card, "tooltip text and size update only when the icon changes"),
        check("_lastPricePoints" in store and "if (!pointsChanged && !stateChanged) continue" in store, "store theme colors update only after state changes"),
        check("_cardTweens[index].Kill()" in store, "store hover movement cancels the previous tween"),
        check(len(arrow.splitlines()) <= 300, "arrow renderer remains below the 300-line limit"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
