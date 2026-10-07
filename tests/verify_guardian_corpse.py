#!/usr/bin/env python3
"""守护判定必须只看活着的守护单位——阵亡但还没被收走的尸体不算。

复现路径（真起 battleField.tscn）：总部固定在 `supportLine[2]`，把守护 `i25`
摆在左边的 `supportLine[1]`，敌方四号坦克摆到敌方前线，然后让守护阵亡。

修复前：

    守护单位 state = destroyed，还绑在 supportLine[1] 上 = True
    尸体还在时 总部受保护 = True
    尸体还在时 攻击总部：防御 20 -> 20          <- 伤害被吞了
    等尸体收走后：supportLine[1] 上的卡 = <空>，总部受保护 = False
    尸体收走后 攻击总部：防御 20 -> 17

修复后：

    [PASS] 活着的时候守护照常拦得住（20 -> 20，应保持不变）
    [PASS] 守护已阵亡，这一击应该打中总部（20 -> 17）
    [PASS] 尸体收走后这一击打中了（17 -> 14）

根因：阵亡单位会在场上**停留一拍**（`DeathPresentationDelaySeconds`，见
`ProcessDeadUnitAsync`）——那一拍里 `state` 已是 `destroyed`，却还绑在格子上，
而 `HasTrait` 读的是托管字段、照样返回 true。判定里只写了 `leftCard != null`，
于是尸体继续「保护」隔壁，`Attack` 撞上那句判定**静默 return**。

同一份逻辑原先在两个函数里各写了一遍（`IsTargetProtectedByGuardian` 给
Attack 复检 + AI 选目标，`IsUnitProtectedByGuardian` 给刷浮标），缺陷也各有一份。
现在收敛到单一实现 `HasGuardianNeighbour`。
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def body_of(source, signature):
    """截出某个方法的函数体（按大括号配平）。"""
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[start:index + 1]
    raise AssertionError(f"大括号不配平: {signature}")


def strip_comments(text):
    return "\n".join(line.split("//")[0] for line in text.splitlines())


def main():
    source = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")

    neighbour = body_of(source, "private bool HasGuardianNeighbour(cardBase_ unit)")
    at_check = body_of(source, "private static bool IsGuardianAt(List<place_> line, int index)")
    by_unit = body_of(source, "private bool IsUnitProtectedByGuardian(cardBase_ unit)")
    by_target = body_of(source, "private bool IsTargetProtectedByGuardian(cardBase_ target, cardBase_ attacker)")

    results = []

    # --- 核心：邻居必须过存活判据，而不是只判 null ---
    results.append(check("CanBeSelected(card)" in at_check,
                         "邻居检查用 CanBeSelected（非 null + 节点存活 + 未阵亡）"))
    results.append(check("card != null && card.HasTrait(UnitTraits.Guardian)" not in source,
                         "旧的 `非null && HasTrait(Guardian)` 写法已全部消失（尸体不会再保护人）"))
    results.append(check("IsGuardianAt(line, index - 1)" in neighbour
                         and "IsGuardianAt(line, index + 1)" in neighbour,
                         "左右两侧都走同一个 IsGuardianAt"))

    # --- 单一实现：两份重复逻辑收敛成一份 ---
    results.append(check("=> HasGuardianNeighbour(unit);" in by_unit,
                         "IsUnitProtectedByGuardian（刷浮标）委托给唯一实现"))
    results.append(check("=>" not in by_target and "HasGuardianNeighbour(target)" in by_target,
                         "IsTargetProtectedByGuardian 也走唯一实现"))
    results.append(check(strip_comments(by_unit).count("HasTrait") == 0,
                         "浮标那条不再自己扫一遍邻居（没留下第二份实现）"))
    results.append(check("frontLine.Contains(myPlace)" not in strip_comments(by_unit),
                         "阵线查找只在唯一实现里出现一次"))

    # --- 别修过头：攻击者兵种规则仍在 ---
    results.append(check("cardBase_.IgnoresGuardian(attacker.cardType)" in by_target,
                         "火炮/轰炸机无视守护这条规则保留在带攻击者的那层"))
    results.append(check("HasSmokeScreenActive()" in neighbour
                         and "HasTrait(UnitTraits.Guardian)) return false;" in neighbour,
                         "烟幕不生效、守护自身不受守护，两条老规则都在"))

    # --- 静默吞伤害要留痕 ---
    results.append(check("is protected by a guardian" in source,
                         "Attack 被守护挡下时留日志（原先完全静默，正是「查不出为什么丢伤害」的原因"))
    results.append(check("is protected by a guardian" in
                         body_of(source, "public async Task Attack(cardBase_ from,cardBase_ to)"),
                         "那句日志写在 Attack 里，与烟幕失败的写法并列"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
