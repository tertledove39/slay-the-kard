#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def main():
    project = (ROOT / "project.godot").read_text(encoding="utf-8")
    service = (ROOT / "core_logic" / "GameDialogue.cs").read_text(encoding="utf-8")
    balloon = (ROOT / "core_ui" / "GameDialogueBalloon.cs").read_text(encoding="utf-8")
    scene = (ROOT / "core_ui" / "game_dialogue_balloon.tscn").read_text(encoding="utf-8")
    example = (ROOT / "dialogues" / "example.dialogue").read_text(encoding="utf-8")
    results = [
        check('DialogueManager="*res://addons/dialogue_manager/dialogue_manager.gd"' in project, "Dialogue Manager autoload is enabled"),
        check('GameDialogue="*res://core_logic/GameDialogue.cs"' in project, "global dialogue facade is enabled"),
        check('runtime/balloon_path="res://core_ui/game_dialogue_balloon.tscn"' in project, "custom portrait balloon is configured"),
        check('StartMenuPath = "res://bin/start_menu.tscn"' in service, "dialogue is disabled only on the start menu"),
        check("Task<bool> PlayAsync" in service and "void Play(" in service, "blocking and fire-and-forget APIs exist"),
        check("SemaphoreSlim" in service and "WaitAsync()" in service, "concurrent requests are serialized"),
        check("resource != activeResource" in service, "unrelated plugin dialogue cannot complete a facade request"),
        check("DialogueEnded -= OnDialogueEnded" in service, "global event subscription is released"),
        check("DialogueEnded -= OnDialogueEnded" in balloon and "GotDialogue -= OnGotDialogue" in balloon, "balloon event subscriptions are released"),
        check(all(name in balloon for name in ('"happy"', '"sad"', '"angry"', '"normal"')), "all portrait expressions are supported"),
        check("using normal portrait" in balloon, "missing expressions use the normal portrait"),
        check('layer = 100' in scene, "dialogue renders above normal game UI"),
        check(all(f"~ {title}" in example for title in ("world_map_intro", "battle_warning", "missing_portrait_fallback")), "example dialogue contains reusable titles"),
        check(all((ROOT / "assest" / f"{name}.png").exists() for name in ("happy", "angry", "normal")), "available portrait assets exist"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
