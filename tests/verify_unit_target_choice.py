#!/usr/bin/env python3
"""单位卡「部署后再点一次目标」这条路不能被鼠标移动打断。

i42（部署:使一个友方单位撤退）这类卡的流程是**两段式**：
    拖到空的支援位松手 -> 部署 -> currentInputState = waitingForChoosingTarget
    -> 玩家再点一次场上的目标 -> ResolveTargetedCommandAsync -> 效果才结算

而 `_Input` 开头有一条兜底（拖拽中途鼠标被抢走时救场）。松开左键之后左键就是
抬起的，于是**玩家一移动鼠标去点目标，这条兜底就把待选状态整个清掉**，效果
永远不结算。真跑一场战斗复现过：`CancelCurrentDrag()` 之后 state 变 nil。
"""
from pathlib import Path
import re
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
    source = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    cards = (ROOT / "cards" / "card.ini").read_text(encoding="utf-8")

    input_body = body_of(source, "public override void _Input(InputEvent @event)")
    cancel = body_of(source, "private void CancelCurrentDrag()")
    abandon = body_of(source, "private void AbandonTargetChoice()")
    close_ui = body_of(source, "private void CloseTargetChoiceUi()")

    results = []

    # --- 核心修复：兜底不能吃掉「已部署、正在等玩家点目标」这个状态 ---
    results.append(check("currentInputState != InputState.waitingForChoosingTarget"
                         in input_body,
                         "鼠标移动的兜底排除了 waitingForChoosingTarget"))
    results.append(check(input_body.index("currentInputState != InputState.waitingForChoosingTarget")
                         < input_body.index("CancelCurrentDrag()"),
                         "排除条件写在 CancelCurrentDrag() 之前才拦得住"))

    # --- 兜底仍然要对真正的拖拽生效（别修过头） ---
    results.append(check("InputEventMouseMotion" in input_body
                         and "!Input.IsMouseButtonPressed(MouseButton.Left)" in input_body,
                         "兜底本身还在（拖拽中途丢鼠标仍能救场，只是不再误伤选目标）"))
    results.append(check(all(state in cancel for state in
                             ("CardState.caught", "CardState.commandCardCaught",
                              "CardState.inplaceAndCaught")),
                         "CancelCurrentDrag 仍然收尾三种拖拽状态"))

    # --- 软锁出口：等目标时点到空地能退出去 ---
    results.append(check("AbandonTargetChoice();" in input_body,
                         "点到空地时会调 AbandonTargetChoice()"))
    results.append(check("if (currentInputState != InputState.waitingForChoosingTarget) return;"
                         in abandon,
                         "AbandonTargetChoice 只在等目标时生效（不影响普通点空地）"))
    results.append(check(all(x in abandon for x in ("cardNowChoose = null;",
                                                    "currentInputState = InputState.nil;")),
                         "放弃时会清 cardNowChoose 与 currentInputState"))

    # --- 收尾只写一份：箭头 + 高亮必须成对收起 ---
    results.append(check(all(x in close_ui for x in ("cardBase.Visible = false;",
                                                     "RestoreAllTargetsColor();")),
                         "CloseTargetChoiceUi 收了箭头与高亮"))
    results.append(check("CloseTargetChoiceUi();" in cancel
                         and "CloseTargetChoiceUi();" in abandon,
                         "拖拽取消与放弃选择共用同一份收尾"))
    results.append(check("RestoreAllTargetsColor();" not in cancel
                         and "RestoreAllTargetsColor();" not in abandon,
                         "RestoreAllTargetsColor 没有在别处重复实现（规范 E）"))
    results.append(check("player1?.RefreshMyHand();" in cancel,
                         "CancelCurrentDrag 仍然把拖拽中的卡归位到手牌"))

    # --- 被报的卡本身的配置没被改动 ---
    section = re.search(r"^\[i42\]$(.*?)(?=^\[|\Z)", cards, re.M | re.S)
    body = section.group(1) if section else ""
    results.append(check(section is not None
                         and "setTarget|Retreat" in body
                         and re.search(r"^targetType[ 	]*=[ 	]*aFriendlyUnit", body, re.M),
                         "i42 的配置仍是 部署:setTarget|Retreat + aFriendlyUnit"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
