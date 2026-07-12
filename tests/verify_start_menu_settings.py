#!/usr/bin/env python3
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    project = (ROOT / "project.godot").read_text(encoding="utf-8")
    start_scene = (ROOT / "bin" / "start_menu.tscn").read_text(encoding="utf-8")
    start_menu = (ROOT / "bin" / "StartMenu.cs").read_text(encoding="utf-8")
    settings = (ROOT / "bin" / "SettingsManager.cs").read_text(encoding="utf-8")
    settings_menu = (ROOT / "bin" / "SettingsMenu.cs").read_text(encoding="utf-8")
    config = (ROOT / "bin" / "setting.ini").read_text(encoding="utf-8")
    results = [
        check('run/main_scene="res://bin/start_menu.tscn"' in project, "project starts from the start menu"),
        check('texture = ExtResource("2")' in start_scene and 'anchors_preset = 15' in start_scene, "start menu contains a full-screen background texture"),
        check(all(f'name="{name}"' in start_scene for name in ("Continue", "Start", "Settings", "Credits")), "start menu contains the four required buttons"),
        check('private const string WorldMapPath = "res://bin/worldMap.tscn";' in start_menu and 'private const string SettingsPath = "res://bin/settings_menu.tscn";' in start_menu, "start and settings buttons navigate to their respective scenes"),
        check('ConnectHover("Menu/Continue");' in start_menu and 'button.MouseEntered += () => AnimateButton(button, HoverScale);' in start_menu, "start menu buttons use valid node paths and animate on hover"),
        check(all(f'method="_on_{name}_pressed"' in start_scene for name in ("continue", "start", "settings", "credits")), "button pressed signals use the scene callback methods"),
        check('_on_continue_pressed()' in start_menu and 'SceneLoader.ChangeSceneAsync(this, WorldMapPath)' in start_menu, "continue and start callbacks open the world map"),
        check('[allowScreenShake]' in config and 'type=bool' in config and 'name=' in config and 'key=' in config, "setting.ini uses section-driven boolean setting definitions"),
        check('public static bool GetBool(string key)' in settings and 'public static void SetBool(string key, bool value)' in settings, "settings are accessible through the static settings manager"),
        check('foreach (var item in SettingsManager.Items)' in settings_menu and 'new CheckButton' in settings_menu, "settings screen builds controls from setting sections"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
