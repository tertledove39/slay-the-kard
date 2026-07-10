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
    ]

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
