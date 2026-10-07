#!/usr/bin/env python3
"""指挥点电表（MeterLabel）的数位不能错位（BUGS #65）。

老板报的：「点数为 11 的时候**前面的 1** 有时不显示」。

真起一个 MeterLabel 跑各种时序，等停稳后读每条滚条的 y 反推字符：

    === 修复前 ===
    [1..12]   目标 12  实际显示 32   <- 十位错
    [9,10,11] 目标 11  实际显示 91   <- 十位停在 9
    [1->11]   目标 11  实际显示 1_   <- 前导位空了
    [稳 9,10,11] 目标 11  实际显示 11  OK   <- 只有「动画没被打断」时才对
    DisplayImmediate(12) -> 读回 21        <- 下标映射反了
    DisplayImmediate(34) -> 读回 43

    === 修复后 ===
    五组全 OK，DisplayImmediate(12/21/34/11) 全部正读。

两个独立的缺陷：
  B1 `DisplayImmediate` 用 `_strips[displayCount - 1 - i]`，而 `RebuildStrips`
     （`_windows[0]` 摆在 x=0）和 `AnimateTo`（`int stripIdx = pos;`）都用正序。
     平时被下一帧的 `AnimateTo` 盖过去，只有某一位这一步没变（1→11 的个位）才露馅。
  B2 `AnimateTo` 打断时只 `Kill()` 掉 tween，条带停在半路；新动画又只补「数字变了」
     的那几位，停错的那一位就永远不纠正。
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def strip_comments(text):
    """去掉 `//` 注释再断言。

    代码里那段说明正好写着「原先写的是 `_strips[displayCount - 1 - i]`，是反的」，
    直接搜原文会命中自己的注释——这类假阳性本项目已经踩到第四次了。
    """
    return "\n".join(line.split("//")[0] for line in text.splitlines())


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


def main():
    source = (ROOT / "bin" / "MeterLabel.cs").read_text(encoding="utf-8")
    code = strip_comments(source)

    immediate = body_of(code, "public void DisplayImmediate(int value)")
    animate = body_of(code, "public async Task AnimateTo(int targetValue)")
    rebuild = body_of(code, "private void RebuildStrips()")
    results = []

    # --- 三处的下标方向必须一致：strip[0] 是最左窗口 ---
    results.append(check("_strips[i].Position" in immediate,
                         "DisplayImmediate 用正序下标 `_strips[i]`"))
    results.append(check("_strips[displayCount - 1 - i]" not in code,
                         "反序写法 `_strips[displayCount - 1 - i]` 已消失"))
    results.append(check("int stripIdx = pos;" in animate,
                         "AnimateTo 仍然把字符串下标直接当条带下标"))
    results.append(check("totalW - (_digitCount - i) * _digitW, 0)" in rebuild,
                         "RebuildStrips 仍然把 _windows[0] 摆在 x=0（最左）"))

    # --- 打断时必须先 snap 回当前值 ---
    cancel = animate[animate.index("if (_isAnimating)"):animate.index("_isAnimating = true;")]
    results.append(check("DisplayImmediate(_currentValue);" in cancel,
                         "打断旧动画时 snap 回 `_currentValue`（否则停错的位永远不纠正）"))
    results.append(check(cancel.index("t?.Kill()") < cancel.index("DisplayImmediate(_currentValue)"),
                         "snap 写在 Kill 之后（先停掉再对齐）"))
    results.append(check("_activeTweens.Clear();" in cancel and "_isAnimating = false;" in cancel,
                         "打断时的原有清理没丢"))

    # --- 别修过头：只补变化位这条优化要留着 ---
    results.append(check("if (oldStr[pos] == newStr[pos])" in animate,
                         "「数字没变的位不动画」这条优化保留（不然每位都滚一遍）"))
    results.append(check("if (targetValue == _currentValue)" in animate,
                         "同值早退保留"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
