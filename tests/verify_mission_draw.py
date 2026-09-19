#!/usr/bin/env python3
"""任务抽取规则验证。

三条规则：
1) 区域配了 boss 时，烈度不为 1 抽不到它，烈度为 1 只能进它；
2) 抽取阶段事件按钮只显示 <事件>，不透露是哪个事件；
3) 每轮抽取为 2 战斗 + 1 事件，事件永不超过 1 个。

抽取规则集中在 bin/MissionDrawer.cs。本脚本除断言源码结构外，
还会用真实的 bin/AreaPool.ini 数据把 Draw 的行为复算一遍，
确认各区域在各烈度下的实际产出符合预期。
"""
import random
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

BATTLE_COUNT = 2
EVENT_COUNT = 1
EVENT_PREFIX = "event:"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def info(message):
    print(f"[INFO] {message}")


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


# ------------------------- 与 MissionDrawer.Draw 等价的重算 -------------------------
def draw(pool, boss, intensity, rng):
    """逐行对应 bin/MissionDrawer.cs 的 Draw。"""
    result = []
    if not pool:
        return result
    has_boss = bool(boss and boss.strip())

    if has_boss and intensity == 1:
        return [boss]

    battles = [i for i in pool if i.strip() and not (has_boss and i == boss) and not i.startswith(EVENT_PREFIX)]
    events = [i for i in pool if i.strip() and not (has_boss and i == boss) and i.startswith(EVENT_PREFIX)]
    rng.shuffle(battles)
    rng.shuffle(events)

    result.extend(battles[:BATTLE_COUNT])
    result.extend(events[:EVENT_COUNT])

    nxt = BATTLE_COUNT
    target = BATTLE_COUNT + EVENT_COUNT
    while len(result) < target and nxt < len(battles):
        result.append(battles[nxt])
        nxt += 1
    return result


def load_area_pool():
    """解析 bin/AreaPool.ini，返回 {区域: {"boss":…, "pool":[…]}}。"""
    areas, section = {}, None
    for line in read("bin/AreaPool.ini").splitlines():
        s = line.strip()
        if s.startswith("[") and s.endswith("]"):
            section = s[1:-1]
            areas[section] = {"boss": "", "pool": []}
            continue
        if "=" not in s or s.startswith("#") or section is None:
            continue
        key, value = s.split("=", 1)
        key, value = key.strip(), value.strip()
        if key.lower() == "areatimes":
            continue
        if key.lower() == "boss":
            areas[section]["boss"] = value
        elif key.startswith("entry") or key.startswith("enemy"):
            areas[section]["pool"].append(value)
    return areas


def main():
    drawer = read("bin/MissionDrawer.cs")
    world = read("bin/WorldMap.cs")
    choose = read("bin/ChooseMission.cs")

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    areas = load_area_pool()
    smoke = [
        check((ROOT / "bin" / "MissionDrawer.cs").exists(), "MissionDrawer.cs 存在"),
        check(len(areas) > 0, f"AreaPool.ini 可解析（{len(areas)} 个区域）"),
        check(any(a["boss"] for a in areas.values()), "至少有一个区域配置了 boss 键"),
        check("MissionDrawer.Draw(candidates, pool.ReadBoss(), intensity)" in world,
              "WorldMap 调用 MissionDrawer.Draw 并传入 boss 与当前烈度"),
    ]

    # --------------------------- 规则一：boss 分流 ---------------------------
    print("\n--- 规则一：boss 按烈度分流 ---")
    rule1 = [
        check("public static List<string> Draw(IReadOnlyList<string> pool, string bossName, int intensity" in drawer,
              "Draw 接收 boss 与烈度参数"),
        check("if (hasBoss && intensity == 1)" in drawer and "result.Add(bossName);" in drawer,
              "烈度为 1 时只返回 boss 一条"),
        check("if (hasBoss && id == bossName) continue;" in drawer,
              "烈度不为 1 时 boss 被排除在战斗池之外"),
        check('if (key.Equals("boss", StringComparison.OrdinalIgnoreCase))' in world
              and "area.SetBoss(val);" in world,
              "AreaPool.ini 的 boss 键被识别为区域元数据，不进入可抽取条目"),
        check("public string ReadBoss()" in world, "Area 提供 ReadBoss()"),
        check("string bossName = \"\";" in world, "未配置 boss 时为空串"),
    ]

    # --------------------------- 规则二：事件脱敏 ---------------------------
    print("\n--- 规则二：事件按钮只显示 <事件> ---")
    rule2 = [
        check('private const string EventMaskLabel = "<事件>";' in world, "定义脱敏文案常量 <事件>"),
        check("DisplayName = EventMaskLabel" in world, "事件条目一律使用脱敏文案"),
        check("DisplayName = ev != null ? ev.Title : eventId" not in world,
              "不再把 event.ini 的 title 直接当按钮文字"),
        check("BattleStateManager.GetEvent(eventId) == null" in world and "GD.PushError" in world,
              "事件配置缺失时仍然报错（报错不影响按钮脱敏）"),
        check("EventMaskLabel" not in choose or "DisplayName" not in choose,
              "ChooseMission 只负责显示，不做文案判断"),
    ]

    # --------------------------- 规则三：2战斗+1事件 ---------------------------
    print("\n--- 规则三：2 战斗 + 1 事件 ---")
    rule3 = [
        check("public const int BattleCount = 2;" in drawer, "战斗数量常量为 2"),
        check("public const int EventCount = 1;" in drawer, "事件数量常量为 1"),
        check("events.Take(EventCount)" in drawer, "事件按常量上限截取"),
        check("while (result.Count < target && nextBattle < battles.Count)" in drawer
              and "result.Add(battles[nextBattle++]);" in drawer,
              "兜底补位只从剩余战斗取，不会补出第 2 个事件"),
        check("PickRandomEntries" not in world, "WorldMap 中的旧抽取函数已移除（避免同一功能两处实现）"),
    ]

    # --------------------------- 数据层：真实配置的实际产出 ---------------------------
    print("\n--- 真实配置下的实际产出（各区域 × 各烈度，各跑 200 次）---")
    rng = random.Random(20260918)
    outcomes = {}
    problems = []
    for name, area in areas.items():
        for intensity in (3, 2, 1):
            seen = set()
            for _ in range(200):
                got = tuple(draw(area["pool"], area["boss"], intensity, rng))
                seen.add(got)
                ev = sum(1 for i in got if i.startswith(EVENT_PREFIX))
                if ev > EVENT_COUNT:
                    problems.append(f"{name}@{intensity} 出现 {ev} 个事件")
                if area["boss"] and intensity != 1 and area["boss"] in got:
                    problems.append(f"{name}@{intensity} 抽到了 boss")
                if area["boss"] and intensity == 1 and got != (area["boss"],):
                    problems.append(f"{name}@{intensity} 未锁定 boss：{got}")
            outcomes[(name, intensity)] = sorted(seen)

    for (name, intensity), combos in sorted(outcomes.items()):
        area = areas[name]
        if not area["pool"]:
            desc = "池为空，0 条（走现有提前返回）"
        elif area["boss"] and intensity == 1:
            desc = f"固定 1 条：{combos[0][0]}"
        else:
            sizes = sorted({len(c) for c in combos})
            ev = sorted({sum(1 for i in c if i.startswith(EVENT_PREFIX)) for c in combos})
            desc = f"{len(combos)} 种组合，按钮数 {sizes}，其中事件数 {ev}"
        print(f"  {name}@烈度{intensity}: {desc}")

    print()
    results = smoke + rule1 + rule2 + rule3 + [
        check(not problems, f"200 次 × 21 种情形均未违反三条规则（异常：{problems[:3]}）"),
    ]

    # area1 的 boss 在烈度 3/2 下必须完全不出现，且组成固定
    a1 = areas.get("area1", {})
    if a1.get("boss"):
        combos_hi = outcomes.get(("area1", 2), [])
        results.append(check(
            all(len(c) == 3 and sum(1 for i in c if i.startswith(EVENT_PREFIX)) == 1 for c in combos_hi),
            "area1 在烈度 2 下始终是 3 个按钮、恰好 1 个事件"))
        results.append(check(
            outcomes.get(("area1", 1)) == [(a1["boss"],)],
            f"area1 在烈度 1 下只提供 boss（{a1['boss']}）"))
        results.append(check(
            all(a1["boss"] not in c for c in combos_hi),
            f"area1 在烈度 2 下抽不到 boss（{a1['boss']}）"))

    # 条目不足时必须优雅降级：按钮数绝不超过池中实际可用条目数，也不会凭空造出事件。
    # 断言写成与具体配置无关的形式——各区域的池子会随内容调整而变化。
    oversized = []
    for (name, intensity), combos in outcomes.items():
        area = areas[name]
        boss_locked = bool(area["boss"]) and intensity == 1
        available = 1 if boss_locked else len(
            [i for i in area["pool"] if i and not (area["boss"] and i == area["boss"])])
        for combo in combos:
            if len(combo) > available:
                oversized.append(f"{name}@{intensity} 出了 {len(combo)} 条，池里只有 {available} 条")
    results.append(check(not oversized,
                         f"按钮数不超过池中可用条目数，不凭空造条目（异常：{oversized[:3]}）"))

    # 池中既有战斗又有事件的区域，必须凑满 3 个按钮
    thin = []
    for (name, intensity), combos in outcomes.items():
        area = areas[name]
        if bool(area["boss"]) and intensity == 1:
            continue
        battles = [i for i in area["pool"] if i and not i.startswith(EVENT_PREFIX)
                   and not (area["boss"] and i == area["boss"])]
        if len(battles) >= 2 and len(area["pool"]) >= 3:
            for combo in combos:
                if len(combo) != 3:
                    thin.append(f"{name}@{intensity} 只出了 {len(combo)} 条")
    results.append(check(not thin, f"战斗充足时恒定凑满 3 个按钮（异常：{thin[:3]}）"))

    info(f"共 {len(areas)} 个区域；配了 boss 的："
         f"{[n for n, a in areas.items() if a['boss']] or '无'}")

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
