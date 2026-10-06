#!/usr/bin/env python3
"""调试控制台的外观：浅黑色、非圆角、无边框，且样式只有一处定义。

需求：把战斗场景与世界地图两个控制台的边框去掉，改成浅黑色非圆角矩形。

踩点提醒（这条最容易漏）：**只换外层 Panel 的 StyleBox 是去不掉边框的**——
Godot 默认主题给 `LineEdit` 的 `normal` / `focus` 样式自带圆角与描边，
输入框那一圈框照样画得出来。必须把输入框的三个状态样式一起覆盖掉。

本测试同时钉住「样式单一来源」：两个控制台原先各写了一遍一模一样的 StyleBoxFlat，
现在都走 `ConsoleStyle`，任一文件里不得再自建。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
STYLE = ROOT / "bin" / "ConsoleStyle.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
WORLD = ROOT / "bin" / "WorldMap.cs"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    results.append(check(STYLE.exists(), "bin/ConsoleStyle.cs 存在"))
    style = STYLE.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    world = WORLD.read_text(encoding="utf-8")

    # --------------------------- 1) 边框与圆角归零 ---------------------------
    print("\n--- 1) 无边框、非圆角 ---")
    for side in ["Left", "Top", "Right", "Bottom"]:
        results.append(check(f"style.BorderWidth{side} = 0;" in style,
                             f"BorderWidth{side} 显式归零"))
    for corner in ["TopLeft", "TopRight", "BottomRight", "BottomLeft"]:
        results.append(check(f"style.CornerRadius{corner} = 0;" in style,
                             f"CornerRadius{corner} 显式归零"))

    # --------------------------- 2) 浅黑色 ---------------------------
    print("\n--- 2) 浅黑色底色 ---")
    m = re.search(r"Background = new Color\(([^)]*)\)", style)
    results.append(check(m is not None, "有具名的 Background 常量"))
    if m:
        parts = [p.strip().rstrip("f") for p in m.group(1).split(",")]
        r, g, b, a = (float(x) for x in parts[:4])
        results.append(check(r == g == b, f"底色是中性灰（R=G=B={r}），不是带色的黑"))
        results.append(check(0.0 < a < 1.0, f"底色带透明度（alpha={a}）——「浅」黑而非纯黑"))

    # --------------------------- 3) 输入框三态都要覆盖（关键） ---------------------------
    print("\n--- 3) 输入框三个状态样式都要覆盖（只改 Panel 去不掉边框） ---")
    for state in ["normal", "focus", "read_only"]:
        results.append(check(f'input.AddThemeStyleboxOverride("{state}", CreateInputBox());' in style,
                             f"覆盖 LineEdit 的 {state} 状态"))
    results.append(check("input.AddThemeStyleboxOverride" in style, "Apply() 会处理 LineEdit"))

    # --------------------------- 4) 单一来源 ---------------------------
    print("\n--- 4) 样式只有一处定义 ---")
    for name, text in [("battlefield_.cs", battle), ("WorldMap.cs", world)]:
        results.append(check("new StyleBoxFlat" not in text,
                             f"{name} 里不再自建 StyleBoxFlat"))
        results.append(check("AddThemeStyleboxOverride" not in text,
                             f"{name} 里不再自己设样式（全交给 ConsoleStyle）"))
        results.append(check("ConsoleStyle.Apply(_consolePanel, _consoleInput);" in text,
                             f"{name} 调用了 ConsoleStyle.Apply"))
        results.append(check('Colors.LimeGreen' not in text,
                             f"{name} 的文字颜色改走 ConsoleStyle.TextColor"))

    # --------------------------- 5) 两个控制台都接上了 ---------------------------
    print("\n--- 5) 战斗与世界地图各一处 ---")
    results.append(check(battle.count("ConsoleStyle.Apply(") == 1, "战斗场景恰好接入一次"))
    results.append(check(world.count("ConsoleStyle.Apply(") == 1, "世界地图恰好接入一次"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
