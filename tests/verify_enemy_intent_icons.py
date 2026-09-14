#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    card = (ROOT / "bin" / "cardBase_.cs").read_text(encoding="utf-8")
    names = ("normalUnit", "bigUnit", "heal", "damage")
    results = [
        check(all((ROOT / "assest" / f"{name}.png").exists() for name in names), "all four intent icon assets exist"),
        check(all(f'"{name}"' in card for name in ("boss",) + names), "intent icon names are registered in IconCache"),
        check("ParseActionMetadata(action)" in battle, "intent rows parse action metadata"),
        check('icon.Texture = IconCache.GetIcon(metadata.icon) ?? IconCache.GetIcon("boss")' in battle, "intent rows resolve per-action icons with boss fallback"),
        check('string icon = "boss"' in battle and 'key == "icon"' in battle, "missing icons default to boss"),
        check('key == "description"' in battle and "meta.Split(',')" in battle, "icon and description metadata are parsed independently"),
        check('ResourceLoader.Load<Texture2D>("res://assest/boss.png")' not in battle, "intent UI no longer hard-codes boss texture"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
