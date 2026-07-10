#!/usr/bin/env python3
from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    if not condition:
        print(f"[FAIL] {message}")
        return False
    print(f"[PASS] {message}")
    return True


def main():
    chooser = (ROOT / "ChooseSomeCard.cs").read_text(encoding="utf-8")
    scene = (ROOT / "choose_some_card.tscn").read_text(encoding="utf-8")
    sources = {
        "Store": (ROOT / "Store.cs").read_text(encoding="utf-8"),
        "EventScene": (ROOT / "bin" / "EventScene.cs").read_text(encoding="utf-8"),
        "PostBattleReward": (ROOT / "bin" / "PostBattleReward.cs").read_text(encoding="utf-8"),
    }

    results = [
        check("new CanvasLayer { Layer = OverlayLayer }" in chooser,
              "ChooseSomeCard uses a dedicated CanvasLayer"),
        check("parent.GetTree().Root.AddChild(layer)" in chooser,
              "selection layer is attached to SceneTree root"),
        check("OverlayLayer = int.MaxValue" in chooser,
              "selection layer uses the highest layer value"),
        check(scene.index('name="OverlayMask"') < scene.index('name="Button"'),
              "shallow black mask is the bottom scene child"),
        check("color = Color(0.08, 0.08, 0.08, 0.72)" in scene,
              "overlay mask is shallow black"),
        check("mouse_filter = 0" in scene,
              "overlay mask blocks input to lower interfaces"),
        check(len(re.findall(r"ChooseSomeCard\.Show\(", sources["Store"])) == 1,
              "Store uses ChooseSomeCard exactly once"),
        check(len(re.findall(r"ChooseSomeCard\.Show\(", sources["EventScene"])) == 2,
              "EventScene uses ChooseSomeCard for both manual replacement paths"),
        check(len(re.findall(r"ChooseSomeCard\.Show\(", sources["PostBattleReward"])) == 1,
              "PostBattleReward uses ChooseSomeCard exactly once"),
        check(not any(name in "\n".join(sources.values()) for name in
                      ("ShowDeckPickUI", "ShowDeckReplaceUI", "ShowDeckSelection")),
              "legacy deck-selection implementations are absent"),
        check("_selected.Count != _pickCount" in chooser,
               "confirm requires the requested selection count"),
        check("private const int Cols = 5;" in chooser,
               "card selection grid uses five columns"),
        check("private const float CardScale = 0.9f;" in chooser,
               "card selection grid uses enlarged cards"),
        check("private const float GapX = 32f;" in chooser and
              "private const float GapY = 28f;" in chooser,
               "card selection grid uses expanded spacing"),
        check("_cards[idx].SetHover(active, scale);" in chooser and
              "_cardPositions[idx] + new Vector2(0, active ? -CardRaise : 0)" in chooser,
               "selected and hovered cards reuse the hand hover border, scale, and raise effect"),
        check("Callable.From(() => click.MouseFilter = Control.MouseFilterEnum.Stop).CallDeferred();" in chooser,
               "card input activates after the opening click is dispatched"),
        check("ColorRect[] _highlights" not in chooser,
               "selected cards do not use a separate selection frame"),
    ]

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
