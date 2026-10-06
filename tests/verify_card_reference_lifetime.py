#!/usr/bin/env python3
"""跨 await 持有的卡牌引用，用之前必须重新确认节点还活着。

真机崩溃（老板给的日志）：

    System.ObjectDisposedException: Cannot access a disposed object.
    Object name: 'cardBase_'.
        GodotObject.base.cs:93  Godot.GodotObject.GetPtr
        GodotObject.base.cs:160 Godot.GodotObject.ToString
        battlefield_.cs:3572    EnemyAttackPhaseAsync        <- 就是那句 GD.Print
        ...  EnemyPerformActionsAsync -> EnemyTurnAsync -> RunTurnTransitionAsync

敌方 AI 的 `attackers` 是进入阶段时拍下的**快照**，之后每个单位之间要
`await Task.Delay(500)`。这 500ms 里那张卡可能已经没了——最典型的是玩家在
敌方回合里点「保存并退出」/「认输」，两者都 `SceneLoader.ChangeSceneAsync`
把 battlefield_ 连同它下面全部卡片、格子一起释放。

`GD.Print($"... {attacker} ...")` 要把节点插值成字符串，走的是原生指针，
对已释放的包装直接抛；`IsInsideTree()` 同理。**而 `getState()` 这类纯托管
字段读取还活着**——所以光靠「状态 != destroyed」拦不住，实测：
已释放的卡上 `card != null && card.getState() != destroyed` 仍然返回 true。
唯一安全的判据是 `GodotObject.IsInstanceValid`（它自己对已释放对象不抛）。
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def strip_comments(text):
    """去掉 `//` 注释再定位。

    `WaitForCorpsesClearedAsync` 里那段注释解释了「为什么 `GetTree()` 会抛」，
    直接搜 `GetTree()` 会先命中注释——这类假阳性本项目已经踩到第三次了。
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

    can_be_selected = body_of(source, "private static bool CanBeSelected(cardBase_ card)")
    attack = body_of(source, "private async Task<bool> EnemyAttackPhaseAsync()")
    advance = body_of(source, "private async Task<bool> EnemyAdvancePhaseAsync()")
    loop = body_of(source, "async Task EnemyPerformActionsAsync()")
    corpses = strip_comments(body_of(source, "private async Task WaitForCorpsesClearedAsync()"))

    results = []

    # --- 判据：唯一一处「这张卡还能不能用」必须带上存活检查 ---
    results.append(check("GodotObject.IsInstanceValid(card)" in can_be_selected,
                         "CanBeSelected 增加了 IsInstanceValid（节点已释放 → false）"))
    results.append(check("card.getState() != CardState.destroyed" in can_be_selected,
                         "CanBeSelected 仍然管「已阵亡还留在场上」那条老规则"))
    results.append(check(can_be_selected.index("IsInstanceValid")
                         < can_be_selected.index("getState()"),
                         "存活检查排在状态检查之前"))

    # --- 攻击阶段：确认必须放在 await 之后 ---
    # 放在 await 之前等于没放：快照里的卡正是在那个 await 期间没的。
    delay = attack.index("await Task.Delay(500)")
    guard = attack.index("CanBeSelected(attacker)")
    results.append(check(guard > delay,
                         "攻击阶段在 await Task.Delay(500) **之后**才确认 attacker"))
    print_index = attack.index('GD.Print($"Processing enemy unit: {attacker}')
    results.append(check(print_index > guard,
                         "那句会抛异常的 GD.Print 排在确认之后（它就是崩溃栈顶）"))
    results.append(check("if (attacker == null) continue;" not in attack,
                         "旧的 `if (attacker == null)` 已换成 CanBeSelected"
                         "（null 判断拦不住已释放的包装）"))

    # --- 推进阶段：快照里的 eCard 同样要确认 ---
    results.append(check("if (!CanBeSelected(eCard)) continue;" in advance,
                         "推进阶段每轮先确认 eCard 还活着"))
    results.append(check(advance.index("CanBeSelected(eCard)")
                         < advance.index("eCard.isHq == HQ.hq"),
                         "确认写在摸 eCard 任何成员之前"))

    # --- 整场战斗被释放时的总闸 ---
    # 阶段里第一件事就是摸 frontLine 那些 place_ 节点，对已释放的包装一样会抛，
    # 所以必须在两阶段之前拦。
    for name, body in (("EnemyPerformActionsAsync", loop),
                       ("WaitForCorpsesClearedAsync", corpses)):
        results.append(check("GodotObject.IsInstanceValid(this)" in body
                             and "IsInsideTree()" in body,
                             f"{name} 里有「战斗还在吗」的总闸"))
    results.append(check(loop.index("IsInstanceValid(this)")
                         < loop.index("await EnemyAdvancePhaseAsync()"),
                         "总闸写在两个阶段之前"))
    results.append(check(corpses.index("IsInstanceValid(this)") < corpses.index("GetTree()"),
                         "总闸写在 WaitForCorpsesClearedAsync 的 GetTree() 之前"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
