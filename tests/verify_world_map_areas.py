#!/usr/bin/env python3
"""世界地图区域按钮与区域池一致性验证

覆盖：
- 冒烟测试：地图场景、区域池、区域状态代码均存在且可解析
- 基本验证：按钮、区域池段名、AreaOrder、UnlockedArea 四者严格一致
- 边界白盒：区域编号连续、坐标落在画布内、按地理位置正确排序、无残留旧区域
"""
from configparser import ConfigParser
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

# Windows 控制台默认码页不是 UTF-8，统一输出编码便于阅读中文结果
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TSCN = ROOT / "bin" / "worldMap.tscn"
POOL = ROOT / "bin" / "AreaPool.ini"
STATE = ROOT / "bin" / "CardRestoration.cs"

EXPECTED_AREAS = [f"area{i}" for i in range(1, 8)]
REMOVED_AREAS = ["area8", "area9", "area10"]
VIEWPORT = (1600.0, 900.0)

# 由西到东：柏林(13.4E)、明斯克(27.5E)、斯摩棱斯克(32.0E)、库尔斯克(36.2E)、
# 哈尔科夫(36.2E)、莫斯科(37.6E)、斯大林格勒(44.5E)
EXPECTED_WEST_TO_EAST = ["area7", "area6", "area3", "area5", "area4", "area1", "area2"]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def parse_buttons(text):
    """解析 worldMap.tscn 中的区域按钮，返回 {区域名: (left, top, right, bottom)}"""
    buttons = {}
    pattern = re.compile(
        r'\[node name="(area\d+)" type="TextureButton" parent="\."\](.*?)(?=\n\[node|\Z)',
        re.S,
    )
    for match in pattern.finditer(text):
        name, body = match.group(1), match.group(2)
        values = []
        for key in ("offset_left", "offset_top", "offset_right", "offset_bottom"):
            found = re.search(rf"{key} = (-?[\d.]+)", body)
            values.append(float(found.group(1)) if found else None)
        buttons[name] = tuple(values)
    return buttons


def read_area_order(text):
    block = re.search(r"AreaOrder = \{([^}]*)\}", text)
    return re.findall(r'"(area\d+)"', block.group(1)) if block else []


def read_unlocked(text):
    block = re.search(r"UnlockedArea \{ get; \} = new\(\)\s*\{(.*?)\};", text, re.S)
    return dict(re.findall(r'\["(area\d+)"\] = (\d+)', block.group(1))) if block else {}


def main():
    if not (TSCN.exists() and POOL.exists() and STATE.exists()):
        print("[FAIL] 冒烟测试：缺少地图场景、区域池或区域状态文件")
        return 1

    tscn_text = TSCN.read_text(encoding="utf-8")
    state_text = STATE.read_text(encoding="utf-8")
    pool_text = POOL.read_text(encoding="utf-8")
    pool = ConfigParser(interpolation=None, strict=True)
    pool.optionxform = str
    pool.read(POOL, encoding="utf-8")

    buttons = parse_buttons(tscn_text)
    sections = pool.sections()
    order_areas = read_area_order(state_text)
    unlocked = read_unlocked(state_text)
    centers = {name: ((box[0] + box[2]) / 2, (box[1] + box[3]) / 2) for name, box in buttons.items()}

    results = [
        check(len(buttons) == len(EXPECTED_AREAS), "冒烟：地图场景可解析出 7 个区域按钮"),
        check(len(sections) == len(EXPECTED_AREAS), "冒烟：区域池可解析出 7 个区域段"),
        check(bool(order_areas) and bool(unlocked), "冒烟：区域顺序与解锁状态可解析"),
        check(sorted(buttons) == EXPECTED_AREAS, "worldMap.tscn 恰含 area1-area7 按钮"),
        check(sorted(sections) == EXPECTED_AREAS, "AreaPool.ini 恰含 area1-area7 区域段"),
        check(order_areas == EXPECTED_AREAS, "AreaOrder 按序包含 area1-area7"),
        check(sorted(unlocked) == EXPECTED_AREAS, "UnlockedArea 恰含 area1-area7"),
        check(
            unlocked.get("area1") == "1" and all(v == "0" for k, v in unlocked.items() if k != "area1"),
            "初始仅 area1 解锁",
        ),
        check(
            len(set(buttons) & set(sections)) == len(EXPECTED_AREAS),
            "每个按钮都有同名区域池段",
        ),
    ]

    for area in EXPECTED_AREAS:
        entries = [v for k, v in pool[area].items() if k.startswith(("enemy", "entry"))]
        results.append(check(len(entries) > 0, f"{area} 至少有一个可抽取任务"))

    results.append(
        check(
            all(box[0] is not None and box[2] is not None for box in buttons.values()),
            "所有按钮都有完整坐标",
        )
    )
    results.append(
        check(
            all(box[2] > box[0] and box[3] > box[1] for box in buttons.values()),
            "所有按钮宽高为正",
        )
    )
    results.append(
        check(
            all(0 <= box[0] and box[2] <= VIEWPORT[0] * 1.1 and box[1] <= VIEWPORT[1] for box in buttons.values()),
            "所有按钮落在画布可见范围内",
        )
    )
    results.append(
        check(
            sorted(centers, key=lambda a: centers[a][0]) == EXPECTED_WEST_TO_EAST,
            "按钮按由西到东排序与历史地理位置一致",
        )
    )
    results.append(
        check(
            centers["area1"][1] < centers["area2"][1] and centers["area1"][1] < centers["area4"][1],
            "莫斯科位于斯大林格勒与哈尔科夫以北",
        )
    )
    results.append(
        check(
            centers["area7"][0] < centers["area6"][0] < centers["area1"][0],
            "柏林在明斯克以西，明斯克在莫斯科以西",
        )
    )

    leftovers = [
        area
        for area in REMOVED_AREAS
        if re.search(rf"\b{area}\b", tscn_text) or re.search(rf"\b{area}\b", pool_text) or re.search(rf"\b{area}\b", state_text)
    ]
    results.append(check(not leftovers, f"旧区域无残留引用（发现：{leftovers or '无'}）"))

    results.append(
        check("[" + EXPECTED_AREAS[-1] + "]" in pool_text and "[" + REMOVED_AREAS[0] + "]" not in pool_text, "区域池以 area7 结尾")
    )

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
