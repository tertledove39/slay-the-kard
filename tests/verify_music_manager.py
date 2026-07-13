#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def main():
    project = (ROOT / "project.godot").read_text(encoding="utf-8")
    music = (ROOT / "core_logic" / "MusicManager.cs").read_text(encoding="utf-8")
    config = (ROOT / "configs" / "music.ini").read_text(encoding="utf-8")
    start = (ROOT / "bin" / "StartMenu.cs").read_text(encoding="utf-8")
    world = (ROOT / "bin" / "WorldMap.cs").read_text(encoding="utf-8")
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    results = [
        check('MusicManager="*res://core_logic/MusicManager.cs"' in project, "MusicManager is registered as an autoload"),
        check('public static MusicManager Instance' in music, "MusicManager exposes a global instance"),
        check('public void PlaySlot(string slot)' in music, "MusicManager exposes PlaySlot"),
        check('[music]' in config and 'start_menu=' in config and 'world_map=' in config and 'battle=' in config, "music slot config contains the expected entries"),
        check('MusicManager.Instance?.PlaySlot("start_menu")' in start, "StartMenu requests menu music"),
        check('MusicManager.Instance?.PlaySlot("world_map")' in world, "WorldMap requests map music"),
        check('MusicManager.Instance?.PlaySlot("battle")' in battle, "battlefield requests battle music"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
