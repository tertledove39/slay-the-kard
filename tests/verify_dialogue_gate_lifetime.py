#!/usr/bin/env python3
"""GameDialogue 的 playGate 不能在被释放之后再用。

`GameDialogue` 是 **autoload**，所以 `_ExitTree` 只在关闭游戏 / 停止调试时触发——
而那一刻完全可能正好有一段对白在播（`PlayAsync` 停在 `await completion.Task` 上）。
`_ExitTree` 做的三件事里有两件会踩到在飞的 `PlayAsync`：

    1. `completion?.TrySetCanceled()` —— 把上面那个 await 抛出来
    2. `playGate.Dispose()`          —— 而 `finally` 里还有一次 `playGate.Release()`

对着已释放的 SemaphoreSlim 调 Release 会抛 `ObjectDisposedException`。更麻烦的是
`Play()` 是不 await 的 fire-and-forget，异常会存进没人观察的 Task，等 GC 或同步
上下文收尾时才炸，从调用栈上完全看不出跟对白有关。

实机/headless 实测（临时 C# 场景，跑完即删）：

    [PASS] 危险确认：SemaphoreSlim 释放后再 Release 抛
           ObjectDisposedException('System.Threading.SemaphoreSlim')
    [PASS] 老形状确认：gate 被释放后 PlayAsync 抛同一个异常
    [PASS] _ExitTree 之后 PlayAsync 不再抛
    [PASS] _ExitTree 之后 PlayAsync 返回 false（拒绝播放，不硬闯）
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


def main():
    source = (ROOT / "core_logic" / "GameDialogue.cs").read_text(encoding="utf-8")

    exit_tree = body_of(source, "public override void _ExitTree()")
    play = body_of(source, "public async Task<bool> PlayAsync(")
    finally_block = play[play.index("finally"):]

    results = []

    # --- 标记必须在 Dispose 之前置上，否则 finally 那边读到的是旧值 ---
    results.append(check("shutdownStarted = true;" in exit_tree,
                         "_ExitTree 置了 shutdownStarted 标记"))
    results.append(check(exit_tree.index("shutdownStarted = true;")
                         < exit_tree.index("playGate.Dispose()"),
                         "标记写在 playGate.Dispose() 之前"))
    results.append(check("DialogueManager.DialogueEnded -= OnDialogueEnded;" in exit_tree,
                         "_ExitTree 仍然退订 DialogueEnded（原有行为没丢）"))

    # --- 两个用得到 gate 的地方都要拦住 ---
    results.append(check("if (shutdownStarted || !CanPlay(resourcePath))" in play,
                         "PlayAsync 在 WaitAsync 之前就拒绝（关掉游戏后不再排队）"))
    results.append(check(play.index("shutdownStarted")
                         < play.index("await playGate.WaitAsync()"),
                         "这个判断写在 await playGate.WaitAsync() 之前"))
    results.append(check("if (!shutdownStarted)" in finally_block
                         and "playGate.Release();" in finally_block,
                         "finally 里的 playGate.Release() 被 shutdownStarted 包住"))

    # --- 别修过头：正常路径该放还得放 ---
    results.append(check("private readonly SemaphoreSlim playGate" in source,
                         "playGate 仍是 readonly 字段，没被改成可空"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
