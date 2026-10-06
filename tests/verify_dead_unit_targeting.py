#!/usr/bin/env python3
"""阵亡但还没消失的卡，不能参与任何「选择」。

背景：单位阵亡后会在场上**停留 `DeathPresentationDelaySeconds` 秒**才消失
（`battlefield_.cs` 的 `PlayDeathPresentationAsync`）。那一拍里它的状态已经是
`destroyed`，但节点**还挂在 `cardInPlaces` 和它自己的格子上**——于是手快就能点中它：

- 能当**攻击目标**（攻击落点校验只比阵营，根本不走 `IsValidTarget`）；
- 能当**指令目标**（`IsValidTarget` 不看状态）；
- 会被算进**「合法目标有几个」**（`GetHowManyCardIsValid` 遍历 `allPlaces`，没有状态过滤）；
- 点中它还会把 `cardNowChoose` 变成那张尸卡，层级被抬到 100。

修法是**一个判据 `CanBeSelected()`，四处引用**（规范 E：同一规则只写一处）。
这个脚本把那个判据和它的引用点都钉住，并反向断言没有漏网的入口。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


def main():
    battle = BATTLE.read_text(encoding="utf-8")
    results = []

    # ==================== 冒烟：判据存在且只有一个 ====================
    print("--- 冒烟：判据 ---")
    results.append(check("private static bool CanBeSelected(cardBase_ card)" in battle,
                         "判据 CanBeSelected 存在"))
    results.append(check("card.getState() != CardState.destroyed" in battle,
                         "判据就是「状态不是 destroyed」"))
    results.append(check(battle.count("private static bool CanBeSelected(") == 1,
                         "判据只定义一处（不在别处再抄一遍）"))
    results.append(check(battle.count("CanBeSelected(") >= 4,
                         f"至少四处引用同一个判据（实际 {battle.count('CanBeSelected(')} 处）"))

    # ==================== ① 指令目标 ====================
    print("\n--- ① 指令目标筛选 ---")
    valid_target = method(battle, "private bool IsValidTarget(cardBase_ card, TargetType targetType)",
                          "case TargetType.aPlace:")
    results.append(check("if (!CanBeSelected(card)) return false;" in valid_target,
                         "IsValidTarget 第一句就挡掉尸卡"))
    # 高亮、目标计数、指令落点校验全走 IsValidTarget —— 一处就够
    results.append(check(battle.count("IsValidTarget(") >= 3,
                         "IsValidTarget 是三个口子的共同入口"))
    results.append(check("foreach (var a in allPlaces)" in battle
                         and "IsValidTarget(a.GetMyCard(), t)" in battle,
                         "GetHowManyCardIsValid 走 IsValidTarget（靠上面那句一起被堵住）"))

    # ==================== ② 攻击目标 ====================
    print("\n--- ② 攻击落点 ---")
    # 攻击落点**不走 IsValidTarget**（那边是给指令用的目标类型筛选），必须自己挡一次。
    # 注意 `case InputState.P_InPlaceUnit:` 在文件里有**两个**（按下那个 switch 与
    # 释放那个 switch），按下那个用 `card`、释放那个用 `result`——所以要按内容定位，
    # 不能按 `case` 标号切。
    guard = "CanBeSelected(result.GetMyCard())"
    results.append(check(guard in battle, "攻击落点自己挡了一次（它不走 IsValidTarget）"))
    if guard in battle:
        after = battle[battle.index(guard):][:400]
        results.append(check("Attack(cardNowChoose, result.GetMyCard());" in after,
                             "挡住之后仍然保留正常攻击路径（挡的是尸卡，不是敌人）"))

    # ==================== ③ 鼠标点选 ====================
    print("\n--- ③ 鼠标点选 ---")
    # 按方法名起头取固定长度，别按 `return null;` 切——那个方法第二行就有一句
    # `if(cardInPlaces== null) return null;`，切片会当场截断。
    click = battle[battle.index("cardBase_ CheckCardClick("):][:600]
    results.append(check("CanBeSelected(card) && card.GetGlobalRect().HasPoint(mousePosition)" in click,
                         "CheckCardClick 点不中尸卡（否则 cardNowChoose 会变成它，层级被抬到 100）"))
    results.append(check(
        not re.search(r"if \(card\.GetGlobalRect\(\)\.HasPoint\(mousePosition\)\)", click),
        "没有漏掉未加判据的命中判断"))

    # ==================== ④ 高亮要跟着变灰 ====================
    print("\n--- ④ 目标高亮 ---")
    highlight = method(battle, "public void HighlightValidTargets(TargetType targetType)", "private int GetHowManyCardIsValid")
    results.append(check("CardState.destroyed" in highlight,
                         "尸卡也要一起变灰（单独留原色会被看成「这个能打」）"))
    results.append(check("if (IsValidTarget(card, targetType))" in highlight,
                         "变灰与否仍然由 IsValidTarget 决定，不另写一套"))

    # ==================== ⑤ 尸卡从哪来 ====================
    print("\n--- ⑤ 尸卡的来源（这一整套的前提） ---")
    dead = method(battle, "private async Task ProcessDeadUnitAsync", "public async void OnNextTurnButtonPressed")
    results.append(check("deadUnit.setState(CardState.destroyed);" in dead,
                         "阵亡时立刻打成 destroyed —— 上面所有判据都依赖它"))
    results.append(check("_ = PlayDeathPresentationAsync(deadUnit, deathCenter);" in dead,
                         "停留期间不 await，阵亡检查的时序不被拖住"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
