#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

# 插件标签语法为 [#tag]；缺少 # 的 [tag] 会被当作正文显示
BAD_TAG = re.compile(r"\[(normal|happy|sad|angry|portrait=none|expression=)")

# 设计分辨率，用于校验立绘落点
VIEWPORT_WIDTH = 1600
VIEWPORT_HEIGHT = 900

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
        check("GetNode<ExampleBalloon>" not in balloon and 'balloon.Call("start"' in balloon,
              "inner balloon is invoked dynamically instead of cast to the C# type"),
        check("example_balloon.gd" in (ROOT / "addons" / "dialogue_manager" / "example_balloon" / "example_balloon.tscn").read_text(encoding="utf-8"),
              "inner balloon scene ships the GDScript implementation"),
        check(all(f"~ {title}" in example for title in ("world_map_intro", "battle_warning", "missing_portrait_fallback")), "example dialogue contains reusable titles"),
        check(all((ROOT / "assest" / f"{name}.png").exists() for name in ("happy", "angry", "normal", "sad")), "available portrait assets exist"),
    ]

    # 立绘靠最左侧，且不被底部对话框遮挡
    inner_scene = (ROOT / "addons" / "dialogue_manager" / "example_balloon" / "example_balloon.tscn").read_text(encoding="utf-8")
    panel_block = re.search(r'\[node name="MarginContainer" type="MarginContainer" parent="Balloon"\](.*?)(?=\n\[node)', inner_scene, re.S)
    panel_height = float(re.search(r"offset_top = -([\d.]+)", panel_block.group(1)).group(1)) if panel_block else 219.0

    portrait_block = re.search(r'\[node name="Portrait".*?\n(.*?)(?=\n\[node|\Z)', scene, re.S)
    portrait_offsets = dict(re.findall(r"(offset_\w+) = (-?[\d.]+)", portrait_block.group(1))) if portrait_block else {}
    portrait_right = float(portrait_offsets.get("offset_right", 1e9))
    portrait_bottom = float(portrait_offsets.get("offset_bottom", 1e9))

    results.extend([
        check(portrait_offsets.get("offset_left") == "0.0", "portrait is flush to the left edge"),
        check(portrait_right <= VIEWPORT_WIDTH / 2,
              f"portrait stays in the left half of the viewport (right={portrait_right})"),
        check(portrait_bottom <= VIEWPORT_HEIGHT - panel_height,
              f"portrait bottom ({portrait_bottom}) stays above the dialogue panel (top={VIEWPORT_HEIGHT - panel_height})"),
    ])

    dialogue_files = sorted((ROOT / "dialogues").glob("*.dialogue"))
    results.extend([
        check(bool(dialogue_files) and not any(BAD_TAG.search(p.read_text(encoding="utf-8")) for p in dialogue_files),
              "dialogue tags use the [#tag] form (a bare [tag] would show up as text)"),
        check(all("[#" in p.read_text(encoding="utf-8") for p in dialogue_files),
              "every dialogue file declares at least one properly formed tag"),
        check("ApplyBalloonStyle" in balloon and "BorderWidthTop = 0" in balloon,
              "balloon panel style is overridden (borderless)"),
        check("PanelColor" in balloon and "0f, 0f, 0f" in balloon,
              "balloon panel uses translucent black background"),
        check("CharacterNameFontSize" in balloon and "normal_font_size" in balloon,
              "character name font size is overridden"),
    ])
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
