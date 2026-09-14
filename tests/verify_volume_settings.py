#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    settings = (ROOT / "bin" / "setting.ini").read_text(encoding="utf-8")
    manager = (ROOT / "bin" / "SettingsManager.cs").read_text(encoding="utf-8")
    menu = (ROOT / "bin" / "SettingsMenu.cs").read_text(encoding="utf-8")
    buses = (ROOT / "default_bus_layout.tres").read_text(encoding="utf-8")
    music = (ROOT / "core_logic" / "MusicManager.cs").read_text(encoding="utf-8")
    battle = (ROOT / "bin" / "battleField.tscn").read_text(encoding="utf-8")
    card = (ROOT / "bin" / "cardbase.tscn").read_text(encoding="utf-8")
    results = [
        check(all(key in settings for key in ("key=master_volume", "key=music_volume", "key=sfx_volume", "key=ui_volume")), "four volume settings are configured"),
        check(settings.count("min=0") == 4 and settings.count("max=100") == 4, "volume bounds are configurable"),
        check('item.Type == "float"' in menu and "new HSlider" in menu, "settings menu builds numeric sliders"),
        check("user://settings.cfg" in manager and "SaveUserValues" in manager, "settings persist in user storage"),
        check("AudioServer.SetBusVolumeDb" in manager and "AudioServer.SetBusMute" in manager, "volume values control audio buses"),
        check("EnsureAudioBuses();" in manager and "AudioServer.AddBus()" in manager and "AudioServer.SetBusSend" in manager, "missing category buses are created before applying volume"),
        check("ApplyAllVolumes();\n            return;" in manager, "reinitialization reapplies current volume values"),
        check(all(f'bus/{index}/name = &"{name}"' in buses for index, name in enumerate(("Master", "Music", "SFX", "UI"))), "audio bus layout contains all categories"),
        check('Bus = "Music"' in music, "background music uses the Music bus"),
        check(battle.count('bus = &"SFX"') == 2 and 'bus = &"SFX"' in card, "gameplay sounds use the SFX bus"),
        check(battle.count('type="AudioStreamPlayer"') == 2, "battle cues use non-positional audio players"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
