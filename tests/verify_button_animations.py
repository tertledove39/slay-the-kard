#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def main():
    worldmap_cs = (ROOT / "bin" / "WorldMap.cs").read_text(encoding="utf-8")
    worldmap_tscn = (ROOT / "bin" / "worldMap.tscn").read_text(encoding="utf-8")
    choose = (ROOT / "bin" / "ChooseMission.cs").read_text(encoding="utf-8")
    store = (ROOT / "Store.cs").read_text(encoding="utf-8")
    event_scene = (ROOT / "bin" / "EventScene.cs").read_text(encoding="utf-8")
    reward = (ROOT / "bin" / "PostBattleReward.cs").read_text(encoding="utf-8")
    display = (ROOT / "DisplayCard.cs").read_text(encoding="utf-8")

    results = [
        check('public void _on_deck_pressed()' in worldmap_cs and 'BuildDisplayDeck' in worldmap_cs, 'deck button callback uses the shared deck viewer'),
        check('var viewDeckBtn = new Button();' not in worldmap_cs, 'world map no longer creates a dynamic deck button in code'),
        check('[node name="deck" type="Button" parent="."]' in worldmap_tscn, 'worldMap.tscn contains the deck button node'),
        check('[connection signal="pressed" from="deck" to="." method="_on_deck_pressed"]' in worldmap_tscn, 'worldMap.tscn connects the deck button to _on_deck_pressed'),
        check('ConnectHover("store")' in worldmap_cs and 'ConnectHover("deck")' in worldmap_cs, 'world map store and deck buttons get hover effects'),
        check('ConnectHover(_btn1);' in choose and 'ConnectHover(_btn2);' in choose and 'ConnectHover(_btn3);' in choose, 'ChooseMission buttons get hover effects'),
        check('refreshBtn.MouseEntered' in store and 'backBtn.MouseEntered' in store, 'store refresh and back buttons get hover effects'),
        check('btn.MouseEntered += () => AnimateButton(btn, HoverScale);' in event_scene, 'event choice buttons get hover effects'),
        check('btn.MouseEntered += () => AnimateButton(btn, HoverScale);' in reward and 'skipBtn.MouseEntered += () => AnimateButton(skipBtn, HoverScale);' in reward, 'post-battle reward buttons get hover effects'),
        check('button.MouseEntered += () => AnimateButton(button, HoverScale);' in display, 'display deck back button gets a hover effect'),
        check(all(text in file for text, file in [
            ('private const float HoverScale = 1.08f;', worldmap_cs),
            ('private const float HoverDuration = 0.12f;', worldmap_cs),
            ('private const float HoverScale = 1.08f;', choose),
            ('private const float HoverDuration = 0.12f;', choose),
            ('private const float HoverScale = 1.08f;', store),
            ('private const float HoverDuration = 0.12f;', store),
            ('private const float HoverScale = 1.08f;', reward),
            ('private const float HoverDuration = 0.12f;', reward),
            ('private const float HoverScale = 1.08f;', display),
            ('private const float HoverDuration = 0.12f;', display),
        ]), 'hover effects reuse the standard 1.08x / 0.12s parameters')
    ]

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
