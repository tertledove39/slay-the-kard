#!/usr/bin/env python3
"""死亡检查的闸门不能在任何路径上被永久关死（BUGS #68）。

老板报的：「友方总部被攻击后血量变为 0 却没有死亡」。截图里 Moscow 的防御
已经是 **-4**，游戏还在继续。

`pauseDeathCheck` 是个**普通标志位**（`PauseDeathCheck()` 置 1、`ResumeDeathCheck()`
置 0），**不是计数器**。`CheckIfAnyUnitDiedAsync()` 第一行就是：

    if (pauseDeathCheck == 1) return;

所以只要有一条路径忘了恢复、或者中途抛了异常，闸门就**永久关死**——之后场上
任何单位（**包括总部**）血降到 0 以下都不会死。

真起 battleField 实测过：

    闸门关死时：总部防御 -4，跑过死亡检查后 state 仍是 placed（活着）
    手动开闸后：同一个总部立刻 state = destroyed

具体那一条：`Move()` 里「手牌 -> 非自己支援阵线的落点」这条非法路径直接 `return`，
没有 `ResumeDeathCheck()`。

修法不是逐个补 Resume（补漏永远是治标），而是**把暂停作用域化**：
`using var guard = PauseDeathCheckScoped();` —— 离开作用域一定恢复，
提前 return 和抛异常都覆盖。已有的显式 `ResumeDeathCheck()` **必须保留**：
它们刻意把恢复安排在「动画播完、自己的死亡检查之前」，那是有意义的时机。
因为 `ResumeDeathCheck()` 是幂等的（直接置 0），两者共存无副作用。
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def body_of(source, signature):
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
    code = strip_comments(source)
    results = []

    guard = body_of(code, "private readonly struct DeathCheckGuard : IDisposable")
    scoped = body_of(code, "private DeathCheckGuard PauseDeathCheckScoped()")

    # --- 作用域守卫本身 ---
    results.append(check("_field.PauseDeathCheck();" in guard,
                         "DeathCheckGuard 构造时就暂停"))
    results.append(check("Dispose() => _field.ResumeDeathCheck();" in guard,
                         "Dispose 一定恢复（using 覆盖提前 return 与抛异常）"))
    results.append(check("PauseDeathCheckScoped() => new DeathCheckGuard(this);" in scoped,
                         "工厂方法返回守卫"))

    # --- 三个暂停点全部改成作用域写法 ---
    # 直接搜 "PauseDeathCheck();"——只应剩定义和第 112 行守卫内部那一句。
    bare = [line for line in code.splitlines()
            if "PauseDeathCheck();" in line and "void PauseDeathCheck()" not in line]
    results.append(check(len(bare) == 1,
                         f"裸的 PauseDeathCheck() 只剩守卫内部那一句（实际 {len(bare)} 处）"))

    for sig, label in (("public async Task Attack(cardBase_ from,cardBase_ to)", "Attack"),
                       ("async Task Move(cardBase_ card, place_ position)", "Move"),
                       ("async Task AddCardToPlace(cardBase_ card, place_ place)", "AddCardToPlace")):
        body = strip_comments(body_of(code, sig))
        results.append(check("using var deathCheckGuard = PauseDeathCheckScoped();" in body,
                             f"{label} 用 using 作用域暂停"))

    # --- 已有的显式恢复是"有意义的时机"，不能顺手删掉 ---
    for sig, label in (("public async Task Attack(cardBase_ from,cardBase_ to)", "Attack"),
                       ("async Task Move(cardBase_ card, place_ position)", "Move"),
                       ("async Task AddCardToPlace(cardBase_ card, place_ place)", "AddCardToPlace")):
        body = strip_comments(body_of(code, sig))
        # 用正则而不是猜缩进——「按空白对齐」的断言本项目已经错过好几次了
        results.append(check(
            re.search(r"ResumeDeathCheck\(\);\s*\n\s*await CheckIfAnyUnitDiedAsync\(\);", body)
            is not None,
            f"{label} 末尾仍「先恢复、再自查死亡」（那是有意义的时机）"))

    # --- 别修过头：闸门本身还是标志位语义 ---
    results.append(check("pauseDeathCheck = 1;" in code and "pauseDeathCheck = 0;" in code,
                         "Pause/Resume 仍是置 1 / 置 0（幂等，所以多调一次无害）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
