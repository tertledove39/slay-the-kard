#!/usr/bin/env python3
"""时点触发里的效果打死人之后，必须有人做死亡检查。

实机反馈：「场上有女狙击手，打出一张机动防御之后，有单位变成 0 血但是没死亡」。

原因：`ExecuteCommandAndDiscard` 里的死亡检查排在**时点之前**——

    await ParseAndExecuteEffect(...);      // 指令自身的效果
    await CheckIfAnyUnitDiedAsync();       // 只覆盖到上面这一步
    await TriggerUnitEffects("FriendlyCommandPlayed", commandCard);   // 时点跑完函数就结束了

而 `damage(n)` 是**缓存型变更**，由 `ParseAndExecuteEffect` 末尾的 `ExecuteChangeLists()`
落地——所以时点跑完时目标防御确实已经是 0，只是没人再查一次死亡。

本测试把「时点之后要有死亡检查」这件事钉住，顺带钉住另外两处已经写对了的地方
（`Move` / `AddCardToPlace`），防止有人重构时把它们删掉。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"
CARD_INI = ROOT / "cards" / "card.ini"
BUGS = ROOT / "docs" / "BUGS.md"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def block(lines, start_marker):
    """从含 start_marker 的行取到该函数结束（按花括号配平）。"""
    i = next(k for k, l in enumerate(lines) if start_marker in l)
    depth, started = 0, False
    for j in range(i, len(lines)):
        depth += lines[j].count("{") - lines[j].count("}")
        if "{" in lines[j]:
            started = True
        if started and depth == 0:
            return "\n".join(lines[i:j + 1])
    raise AssertionError(f"未找到 {start_marker} 的函数体")


def main():
    results = []
    text = BATTLE.read_text(encoding="utf-8")
    lines = text.split("\n")
    card_ini = CARD_INI.read_text(encoding="utf-8")

    # ==================== 本次修复的这一条 ====================
    print("--- 「打出指令」时点之后必须有死亡检查 ---")
    ecc = block(lines, "private async void ExecuteCommandAndDiscard")
    trigger = ecc.index('TriggerUnitEffects("FriendlyCommandPlayed"')
    results.append(check("CheckIfAnyUnitDiedAsync" in ecc, "ExecuteCommandAndDiscard 里有死亡检查"))
    results.append(check(
        ecc.rindex("CheckIfAnyUnitDiedAsync") > trigger,
        "最后一次死亡检查排在 FriendlyCommandPlayed 时点**之后**（这次 bug 的修复点）",
    ))
    results.append(check(
        ecc.count("CheckIfAnyUnitDiedAsync") >= 2,
        "时点前后各一次：指令自身效果一次、时点效果一次",
    ))

    # 时点必须是函数的最后一个动作之前→说明确实挂在末尾
    tail = ecc[trigger:]
    results.append(check(
        tail.index("CheckIfAnyUnitDiedAsync") < tail.rindex("}"),
        "时点之后的死亡检查在函数返回前执行",
    ))

    # ==================== 观察点：女狙击手 ====================
    print("\n--- 观察点：女狙击手 ---")
    m = re.search(r"(?ms)^\[女狙击手\](.*?)(?=^\[)", card_ini)
    results.append(check(m is not None, "card.ini 里有 [女狙击手]"))
    sniper = m.group(1) if m else ""
    results.append(check("FriendlyCommandPlayed" in sniper, "女狙击手挂在 FriendlyCommandPlayed 时点上"))
    results.append(check("damage(" in sniper, "女狙击手的效果是造成伤害（能打死人）"))
    results.append(check("GetRandomEnemyTarget" in sniper, "目标取自随机敌方（所以要靠死亡检查收尾）"))

    # ==================== 已经写对的邻居，防止被重构删掉 ====================
    print("\n--- 邻居：别处在时点之后也有死亡检查 ---")
    move = block(lines, "async Task Move(cardBase_ card, place_ position)")
    results.append(check(
        'TriggerUnitEffects("Moving"' in move
        and move.index("CheckIfAnyUnitDiedAsync") > move.index('TriggerUnitEffects("Moving"'),
        "Move：Moving 时点之后恢复检查并查死亡",
    ))
    results.append(check("ResumeDeathCheck();" in move, "Move：先恢复死亡检查再查"))

    place = block(lines, "async Task AddCardToPlace(cardBase_ card, place_ place)")
    results.append(check(
        place.index("CheckIfAnyUnitDiedAsync") > place.index('TriggerUnitEffects("FriendlyUnitEnteringField"'),
        "AddCardToPlace：入场时点之后查死亡",
    ))
    results.append(check("ResumeDeathCheck();" in place, "AddCardToPlace：先恢复死亡检查再查"))

    # ==================== 同类缺口：FriendlyCardDrawn ====================
    print("\n--- 同类缺口：FriendlyCardDrawn（本批一并修掉） ---")
    drawn = block(lines, "public async Task TriggerFriendlyCardDrawn(cardBase_ card)")
    results.append(check("public async Task TriggerFriendlyCardDrawn" in text,
                         "TriggerFriendlyCardDrawn 改成可 await（不再是发后不理）"))
    results.append(check("_ = TriggerUnitEffects(\"FriendlyCardDrawn\"" not in text,
                         "不再有发后不理的 FriendlyCardDrawn 调用"))
    results.append(check(
        drawn.index("CheckIfAnyUnitDiedAsync") > drawn.index('TriggerUnitEffects("FriendlyCardDrawn"'),
        "时点之后补了死亡检查",
    ))
    results.append(check("await battlefield.TriggerFriendlyCardDrawn(card);" in text,
                         "唯一的调用点（Player.DrawCard）改成 await"))

    # 待弃置移除必须有日志，否则「卡莫名被弃」查不出来
    print("\n--- 待弃置移除的日志 ---")
    results.append(check("[Discard] 待弃置移除" in text,
                         "待弃置单位被移除时有日志（含 id / 阵营 / isHq）"))
    results.append(check("id={card.id}" in text and "isHq={card.isHq}" in text,
                         "日志带得够定位：卡 id 与是不是总部"))

    bugs = BUGS.read_text(encoding="utf-8")
    results.append(check("FriendlyCardDrawn" in bugs,
                         "该缺口在 docs/BUGS.md 里留了档"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
