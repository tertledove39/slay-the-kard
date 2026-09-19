#!/usr/bin/env python3
"""Next 按钮在敌方回合闪烁的回归校验。

现象：敌方回合进行中，Next 按钮每执行完一次敌方行动就闪一下可点击状态。

根因：ForbidControl / AllowControl 原本是一对**扁平标志**（allowControl 只是个
int，没有计数），但调用点是**嵌套**的：

    OnNextTurnButtonPressed:3084  ForbidControl()   <- 外层
    └─ EnemyTurnAsync:2185        ForbidControl()   <- 内层
       └─ Attack:1796             ForbidControl()
          └─ Attack:1999          AllowControl()    <- 最内层直接解锁！

Attack() 结尾无条件调用 AllowControl()，于是每执行完一次攻击，按钮就被置回
Enabled，下一次攻击又立刻禁用 —— 敌方行动几次就闪几次。

修法：把扁平标志改成**嵌套感知**的控制锁，AllowControl() 只在最外层解除时
才真正解锁。所有既有调用点一行都不用改。

本脚本用 Python 复刻计数器语义，按真实嵌套顺序模拟一遍，断言按钮在整个敌方
回合期间**一次都没有**变成可点击；并用旧语义做对照，证明该序列确实会触发闪烁
（即断言不是空转）。
"""
import re
from pathlib import Path
import sys

# 与项目其他测试一致：Windows 控制台默认按本地代码页输出，中文会乱码
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLEFIELD = ROOT / "bin" / "battlefield_.cs"

# 敌方回合的真实嵌套顺序（对应上表四层 + 收尾两处解锁）
ENEMY_TURN_OPS = (
    ["forbid", "forbid"]                      # OnNextTurnButtonPressed, EnemyTurnAsync
    + ["forbid", "allow"] * 3                 # 三次敌方行动，每次一个 Attack
    + ["allow", "allow"]                      # EnemyTurnAsync 收尾, OnNextTurnButtonPressed finally
)


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def simulate_new(ops):
    """复刻修改后的语义：返回每步之后按钮是否处于 Disabled"""
    depth, disabled, trace = 0, False, []
    for op in ops:
        if op == "forbid":
            depth += 1
            disabled = True
        else:
            if depth > 0:
                depth -= 1
            if depth == 0:
                disabled = False
        trace.append(disabled)
    return trace


def simulate_old(ops):
    """复刻修改前的语义（扁平标志）——用于证明该序列确实会闪烁"""
    disabled, trace = False, []
    for op in ops:
        disabled = (op == "forbid")
        trace.append(disabled)
    return trace


def main():
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    results.append(check(BATTLEFIELD.exists(), "bin/battlefield_.cs 存在"))
    src = BATTLEFIELD.read_text(encoding="utf-8")
    results.append(check("void AllowControl()" in src and "void ForbidControl()" in src,
                         "AllowControl / ForbidControl 均存在"))
    results.append(check(re.search(r"int controlLockDepth\s*=\s*0\s*;", src) is not None,
                         "已引入嵌套计数字段 controlLockDepth"))
    results.append(check("buttonNextTurn.Disabled" in src,
                         "按钮禁用状态由这两个函数统一控制"))

    # --------------------------- 实现语义是否到位 ---------------------------
    print("\n--- 需求基本验证：AllowControl 只在最外层解锁 ---")
    allow_body = re.search(r"void AllowControl\(\)\s*\{(.*?)\n    \}", src, re.S)
    forbid_body = re.search(r"void ForbidControl\(\)\s*\{(.*?)\n    \}", src, re.S)
    results.append(check(allow_body is not None and forbid_body is not None,
                         "两个函数体均可定位"))

    if allow_body and forbid_body:
        ab, fb = allow_body.group(1), forbid_body.group(1)
        # 剥掉注释再断言，注释里会提到这些名字
        ab_code = "\n".join(l for l in ab.splitlines() if not l.strip().startswith("//"))
        fb_code = "\n".join(l for l in fb.splitlines() if not l.strip().startswith("//"))
        results.append(check("controlLockDepth++" in fb_code,
                             "ForbidControl 自增嵌套计数"))
        results.append(check(re.search(r"controlLockDepth\s*--", ab_code) is not None,
                             "AllowControl 递减嵌套计数"))
        results.append(check(ab_code.count("controlLockDepth") >= 2
                             and re.search(r"if\s*\(\s*controlLockDepth\s*>\s*0\s*\)\s*\n\s*return\s*;",
                                           ab_code) is not None,
                             "AllowControl 在计数仍大于 0 时提前返回，不解锁"))
        results.append(check(ab_code.index("controlLockDepth") < ab_code.index("allowControl = 0"),
                             "嵌套判断位于解锁语句之前（顺序不能颠倒）"))

    # ------------------ 行为模拟：敌方回合全程不得出现可点击 ------------------
    print("\n--- 行为模拟：整个敌方回合期间按钮不得变为可点击 ---")
    new_trace = simulate_new(ENEMY_TURN_OPS)
    old_trace = simulate_old(ENEMY_TURN_OPS)

    def flickers(trace):
        """除最后一步（回合真正结束）外，出现过 Enabled 即为闪烁"""
        return [i for i, d in enumerate(trace[:-1]) if d is False]

    new_flicker = flickers(new_trace)
    old_flicker = flickers(old_trace)

    print(f"        修改后 Disabled 时序: {new_trace}")
    print(f"        修改前 Disabled 时序: {old_trace}")
    results.append(check(not new_flicker,
                         f"修改后敌方回合全程均为 Disabled，无闪烁（异常步：{new_flicker}）"))
    results.append(check(bool(old_flicker),
                         f"对照：修改前同一序列确会闪烁 {len(old_flicker)} 次"
                         "（证明本断言不是空转）"))
    results.append(check(new_trace[-1] is False,
                         "回合真正结束时按钮恢复可点击"))

    # --------------------------- 边界情况白盒测试 ---------------------------
    print("\n--- 边界白盒测试 ---")
    # 我方回合单次攻击：Attack 是最外层，1 -> 0，必须照常解锁
    solo = simulate_new(["forbid", "allow"])
    results.append(check(solo == [True, False],
                         "我方回合单独一次攻击（深度 1→0）照常解锁，未影响原有效果"))

    # 未配对的 AllowControl（深度已为 0）行为必须与修改前一致
    unpaired = simulate_new(["allow"])
    results.append(check(unpaired == [False],
                         "深度为 0 时的 AllowControl 仍解锁（未配对的调用行为不变）"))

    # 嵌套深度不得为负
    deep = simulate_new(["allow", "allow", "allow"])
    results.append(check(len(deep) == 3 and all(d is False for d in deep),
                         "多次未配对 AllowControl 不会让计数变负、不会卡死"))

    # 深度 2 的一次配对：内层解除后必须仍禁用
    nested = simulate_new(["forbid", "forbid", "allow"])
    results.append(check(nested == [True, True, True],
                         "嵌套深度 2 时，内层一次 AllowControl 后仍然禁用"))

    # ------------------- 全项目调用点配对情况（防止新引入失衡） -------------------
    print("\n--- 调用点配对审查 ---")
    attack = re.search(r"public async Task Attack\(cardBase_ from,cardBase_ to\)(.*?)\n    /// <summary>\n    /// 将一张卡牌",
                       src, re.S)
    results.append(check(attack is not None, "Attack() 函数体可定位"))
    if attack:
        body = "\n".join(l for l in attack.group(1).splitlines()
                         if not l.strip().startswith("//"))
        results.append(check(body.count("ForbidControl()") == 1,
                             "Attack() 只在开头 ForbidControl 一次"))
        results.append(check(body.count("AllowControl()") >= 1,
                             "Attack() 含 AllowControl 解除"))
        code_lines = [l.strip() for l in body.splitlines() if l.strip()]
        # 捕获范围含函数收尾的右大括号，先剥掉才是最后一条语句
        while code_lines and code_lines[-1] in ("}", "{"):
            code_lines.pop()
        results.append(check(bool(code_lines) and code_lines[-1] == "AllowControl();",
                             "Attack() 结尾以 AllowControl() 收尾（这正是闪烁的来源，"
                             "须由计数保护而非删除）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
