#!/usr/bin/env python3
"""「await 永不返回」不能把死亡检查的闸门永久关死（BUGS #69）。

上一轮（BUGS #68）修的是闸门被**提前 return** 漏掉 `ResumeDeathCheck`：把暂停
**作用域化**（`using var guard = PauseDeathCheckScoped()`）就堵住了。

本轮是同一句话的**另一半**：`using` 只能覆盖「返回」和「抛异常」，**覆盖不了「挂死」**。

链条：

    cardBase_.MoveToPosition()
      └─ await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame)
           卡被摘出场景树 → GetTree() 返回 null
           → ToSignal(null, …) 的 awaiter **永不完成** → 方法永不返回
    → 上游 AddCardToPlace / Move / Attack 里的 using 守卫**永不 Dispose**
    → pauseDeathCheck 永远停在 1
    → CheckIfAnyUnitDiedAsync() 第一行就 return → 总部血 -4 也不死

**谁会把卡摘出场景树**：`ResourceManager.ReleaseEmptyCard()` 的第一步就是
`GetParent().RemoveChild(card)`，而 `cardBase_.Dead()` / `RemoveCard` 会走到它。

真起 battleField 实测过（修复前 / 修复后）：

    修复前：AddCardToPlace 完成了吗=False  闸门=1  总部 state=placed 防御=-4（不死）
    修复后：AddCardToPlace 完成了吗=True   闸门=0  总部 state=destroyed

同理的开口全项目还有 4 处（`ResourceManager` ×3、`BattleEffectPool` ×1），
一律收口到 `AsyncWait.WaitFrameAsync`。

另外兜一道**看门狗**：闸门连续关闭超过 `DeathCheckGateStuckSeconds` 就强制开闸并
报出「是谁关的」。挂死点不止一处（BUGS #61 是同族），逐个封堵永远会漏，
但「总部永远死不掉」这个**用户可见症状**必须不可能出现。

行为回归（真跑）：`tests/DeathGateHangTest.cs` +
`tests/death_gate_hang_test.tscn`（甲 基线 / 乙 挂死复现 / 丙 看门狗边界）。
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

# 「等一帧」只允许出现在这一个文件里——散着写就一定会再漏一处
WAIT_OWNER = ROOT / "bin" / "AsyncWait.cs"
SCANNED_DIRS = ("bin", "core_logic")


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
    results = []

    battlefield = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    cardbase = (ROOT / "bin" / "cardBase_.cs").read_text(encoding="utf-8")
    bfield_code = strip_comments(battlefield)
    card_code = strip_comments(cardbase)

    # --- 一、等一帧的唯一实现 ---
    results.append(check(WAIT_OWNER.exists(), "bin/AsyncWait.cs 存在"))
    wait_src = WAIT_OWNER.read_text(encoding="utf-8")
    wait_body = body_of(wait_src, "public static async Task WaitFrameAsync(")
    results.append(check("!GodotObject.IsInstanceValid(node)" in wait_body,
                         "WaitFrameAsync 先判 IsInstanceValid（它对已释放对象不抛，必须先短路）"))
    results.append(check("!node.IsInsideTree()" in wait_body, "WaitFrameAsync 再判 IsInsideTree"))
    results.append(check(wait_body.index("IsInstanceValid") < wait_body.index("IsInsideTree()"),
                         "两个判据的顺序正确（先 IsInstanceValid 再 IsInsideTree）"))
    results.append(check("return;" in wait_body.split("await")[0],
                         "已离树/已释放时直接返回，绝不走到 await"))

    # 全项目不该再有人自己写「等一帧」形式的 ToSignal(GetTree(), …)。
    # 只认这一种形状：`ToSignal(GetTree().CreateTimer(x), …)` 是等计时器，不是等帧，
    # GetTree() 为 null 时那行会**抛异常**（using 守卫能兜住），不是本条的挂死形态。
    strays = []
    for folder in SCANNED_DIRS:
        for path in (ROOT / folder).rglob("*.cs"):
            if path == WAIT_OWNER:
                continue
            for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
                if "ToSignal(GetTree()," in line.split("//")[0]:
                    strays.append(f"{path.relative_to(ROOT)}:{lineno}")
    results.append(check(not strays,
                         f"没有别处直接 await ToSignal(GetTree(), …) 等帧（实际 {strays}）"))

    # --- 二、MoveToPosition 进出各挡一次 ---
    move = strip_comments(body_of(card_code, "async public Task MoveToPosition(Vector2 destination"))
    results.append(check("if (!CanAnimate()) return;" in move,
                         "MoveToPosition 进循环前先挡一次（不在树上就不建 Tween、不进等待）"))
    results.append(check("AsyncWait.WaitFrameAsync(this)" in move,
                         "等待改走 AsyncWait.WaitFrameAsync"))
    loop = move[move.index("while ("):]
    results.append(check("if (!CanAnimate()) return;" in loop,
                         "循环体内再挡一次（动画途中被摘出场景树的窗口）"))

    can_animate = strip_comments(body_of(card_code, "private bool CanAnimate()"))
    results.append(check("GodotObject.IsInstanceValid(this)" in can_animate
                         and "IsInsideTree()" in can_animate,
                         "CanAnimate 就是「已释放 / 已离树」这一个判据"))
    results.append(check(can_animate.index("IsInstanceValid") < can_animate.index("IsInsideTree()"),
                         "CanAnimate 判据顺序正确"))

    # --- 三、闸门看门狗 ---
    results.append(check("private const int DeathCheckGateStuckSeconds" in bfield_code,
                         "看门狗阈值是命名常量，不是散落的字面量"))
    watchdog = strip_comments(body_of(bfield_code, "private void ReleaseDeathCheckGateIfStuck()"))
    results.append(check("pauseDeathCheck = 0;" in watchdog, "看门狗真的会开闸"))
    results.append(check("_deathCheckPausedBy" in watchdog and "heldMsec" in watchdog,
                         "告警里带上「谁关的、关了多久」"))
    results.append(check("_deathCheckPausedSinceMsec" in watchdog, "看门狗按时间戳判断"))

    died = strip_comments(body_of(bfield_code, "async Task CheckIfAnyUnitDiedAsync()"))
    head = died[:died.index("if (pauseDeathCheck == 1)")]
    results.append(check("ReleaseDeathCheckGateIfStuck();" in head,
                         "死亡检查开头就先跑看门狗（否则卡死时根本走不到它）"))
    results.append(check("WarnIfHqShouldHaveDied();" in died,
                         "闸门关着时也报「总部为什么没死」，不再静默"))

    # --- 四、关闸门时打戳 + 记名（看门狗要有东西可报）---
    pause = strip_comments(body_of(bfield_code, "void PauseDeathCheck(string reason = \"\")"))
    results.append(check("if (pauseDeathCheck == 0)" in pause,
                         "只在 0→1 那一次打戳，嵌套暂停不冲掉时间戳"))
    results.append(check("_deathCheckPausedSinceMsec = Time.GetTicksMsec();" in pause,
                         "记下关上闸门的时刻"))
    results.append(check("_deathCheckPausedBy = reason;" in pause, "记下关上闸门的方法名"))

    scoped = strip_comments(body_of(bfield_code, "private DeathCheckGuard PauseDeathCheckScoped("))
    results.append(check("CallerMemberName" in scoped,
                         "PauseDeathCheckScoped 用 CallerMemberName 自动填原因（调用方不用改）"))

    # --- 五、行为回归还在 ---
    results.append(check((ROOT / "tests" / "DeathGateHangTest.cs").exists()
                         and (ROOT / "tests" / "death_gate_hang_test.tscn").exists(),
                         "真跑的行为回归（tests/DeathGateHangTest.cs + 场景）在场"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
