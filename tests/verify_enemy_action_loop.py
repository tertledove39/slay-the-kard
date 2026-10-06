#!/usr/bin/env python3
"""敌方行动循环（移动 -> 攻击 -> 还有人动得了就回到移动）的结构回归。

行为本身是**真跑一场战斗**验的（见 docs/TEST.md 第二十六轮）：起一次
battleField.tscn，把轻步兵摆到前线、四号坦克摆到敌方支援阵线，跑完整
EnemyPerformActionsAsync，断言坦克在同一回合内推进到了前线。这里只钉住
那些「一改就会静默退化」的结构——尤其是三个返回值契约和循环判据。
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def strip_comments(text):
    """去掉 `//` 注释后再断言。

    推进阶段里有一段注释专门解释「为什么**不**在这里补 HaveAttacked()」，
    直接搜原文会被自己的注释命中——这类假阳性踩过不止一次了。
    """
    return "\n".join(line.split("//")[0] for line in text.splitlines())


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


def main():
    source = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")

    loop = body_of(source, "async Task EnemyPerformActionsAsync()")
    advance = body_of(source, "private async Task<bool> EnemyAdvancePhaseAsync()")
    attack = body_of(source, "private async Task<bool> EnemyAttackPhaseAsync()")
    corpses = body_of(source, "private async Task WaitForCorpsesClearedAsync()")
    turn = body_of(source, "async Task EnemyTurnAsync()")

    results = []

    # --- 循环骨架：两个阶段都被包在同一个有界循环里 ---
    results.append(check("for (int round = 0; round < MaxEnemyActionRounds; round++)" in loop,
                         "移动与攻击被包在同一个有界循环里"))
    results.append(check("await EnemyAdvancePhaseAsync()" in loop
                         and "await EnemyAttackPhaseAsync()" in loop,
                         "循环体内按 移动 -> 攻击 的顺序调两个阶段"))
    results.append(check(re.search(r"if \(!anybodyMoved && !anybodyAttacked\) return;", loop)
                         is not None,
                         "循环判据是「场上有没有变化」，两个标志缺一不可"))

    # --- 回归：判据退化成「还有没有能移动的单位」会死循环 ---
    # 空军/炮兵/已经在后方的单位永远满足 CheckIfCanMove()，推进阶段却永远不会动它们。
    results.append(check("CheckIfCanMove" not in loop,
                         "循环判据里没有 CheckIfCanMove（它会死循环：空军/炮兵永远「能动」）"))

    # --- 回归：只看 anybodyMoved 会在第 1 轮就退出 ---
    # 前线被我方占住时推进阶段是空转的，攻击打死挡路的之后必须还能再转一轮。
    results.append(check("anybodyAttacked" in loop.split("if (!anybodyMoved")[0],
                         "攻击阶段的结果参与了循环判据（否则打死挡路的也不会补位）"))

    # --- 刚打完要等尸体从格子上收走 ---
    results.append(check("if (anybodyAttacked) await WaitForCorpsesClearedAsync();" in loop,
                         "攻击轮之后等尸体收走（阵亡单位要在场上留一拍才解绑格子）"))
    results.append(check("CardState.destroyed" in corpses and "DeathSettleTimeoutSeconds" in corpses,
                         "等尸体是「轮询 destroyed + 硬上限」而不是死等"))
    results.append(check("for (double waited = 0; waited < DeathSettleTimeoutSeconds;" in corpses,
                         "等尸体有明确上限，不会把敌方回合拖死"))

    # --- 返回值契约 ---
    results.append(check("private async Task<bool> EnemyAdvancePhaseAsync()" in source
                         and "return anybodyMoved;" in advance,
                         "推进阶段返回「这一轮有没有人真的移动过」"))
    results.append(check("private async Task<bool> EnemyAttackPhaseAsync()" in source
                         and "return anybodyAttacked;" in attack,
                         "攻击阶段返回「这一轮有没有人真的打出去过」"))
    results.append(check("anybodyMoved = true;" in advance,
                         "推进阶段只在真的 await Move(...) 之后才记「动过」"))

    # --- 四处优先级分支都必须记账，漏一处就会提前收敛 ---
    results.append(check(attack.count("await Strike(") == 4
                         and attack.count("async Task Strike(") == 1,
                         "四处优先级分支全部走 Strike，不会漏记某一处"))

    # --- 命名常量，不留魔鬼数字 ---
    results.append(check(all(re.search(rf"private const (int|double) {name} = ", source)
                             for name in ("MaxEnemyActionRounds", "DeathSettlePollSeconds",
                                          "DeathSettleTimeoutSeconds")),
                         "三个上限/间隔都是命名常量"))

    # --- 回归：推进后补 HaveAttacked() 会把 attackAble 减成 -1 ---
    results.append(check("HaveAttacked" not in strip_comments(advance),
                         "推进阶段不再补 HaveAttacked（会减成 -1，吃掉「额外+1次攻击」）"))

    # --- 前提：敌方单位得先有行动力，循环才有东西可转 ---
    results.append(check(turn.index("RefreshCardsInField(IsFriend.enemy)")
                         < turn.index("EnemyPerformActionsAsync()"),
                         "敌方回合先刷新行动力再调 AI（否则行动力全是 0，循环空转）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
