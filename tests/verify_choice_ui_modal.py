#!/usr/bin/env python3
"""选择界面（ShowCardChoice）必须是模态的：控制锁不能吃掉它的点击。

`_Input` 里原本的顺序是

    if (ReadControlState() == 1) return;          // 控制锁判定在前
    ...
    if (isShowingChoiceUI) { HandleChoiceCardClick(...); return; }   // 选择界面判定在后

平时没事——从手牌打出的指令卡（如 `[紧急投产]` 的 `Develop($deck)`）不在锁里。
但「友方回合开始时」这类时点的效果嵌在回合切换的锁里面：

    OnNextTurnButtonPressed → ForbidControl → RunTurnTransitionAsync → FriendlyTurnBegin

`[步兵第190团]` 的 effect 正是 `FriendlyTurnBegin:Develop($deck)`。此时选择界面
已经弹出、正等玩家点卡，可 `allowControl == 1`，每一次点击都在控制锁那行被吃掉，
`HandleChoiceCardClick` 永远收不到事件——**卡看得见，却一张都点不动，且不报任何错**。

故本测试把「选择界面判定必须早于控制锁判定」钉成回归断言。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"
CARD_INI = ROOT / "cards" / "card.ini"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    battle = BATTLE.read_text(encoding="utf-8")
    cards = CARD_INI.read_text(encoding="utf-8")
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    results.append(check(BATTLE.exists() and CARD_INI.exists(), "源文件与卡表均存在"))
    start = battle.find("public override void _Input(InputEvent @event)")
    end = battle.find("public override void _Process(double delta)")
    results.append(check(start != -1 and end != -1 and start < end, "_Input 与 _Process 可定位"))
    body = battle[start:end]

    # ------------------- 1) 选择界面判定早于控制锁判定 -------------------
    print("\n--- 1) 选择界面判定早于控制锁判定（回归） ---")
    idx_choice = body.find("if (isShowingChoiceUI")
    idx_gate = body.find("if (ReadControlState() == 1) return;")
    results.append(check(idx_choice != -1, "_Input 里有 isShowingChoiceUI 分支"))
    results.append(check(idx_gate != -1, "_Input 里有控制锁判定"))
    results.append(check(idx_choice != -1 and idx_gate != -1 and idx_choice < idx_gate,
                         "选择界面判定位于控制锁判定之前——否则锁住时选择界面点不动"))
    results.append(check("HandleChoiceCardClick(GetGlobalMousePosition());" in body,
                         "两支都仍调用 HandleChoiceCardClick（命中判定只有一处实现）"))
    results.append(check(body.count("HandleChoiceCardClick(") == 1,
                         "HandleChoiceCardClick 在 _Input 里只调用一次，没有留下重复分支"))

    # ------------------- 2) 触发场景确实存在 -------------------
    print("\n--- 2) 触发场景确实存在（不是空转断言） ---")
    # 卡牌按 section ID 索引（GetCard() 查的就是它），「步兵第190团」的 ID 是 i190
    m = re.search(r"^\[i190\]\n(.*?)(?=^\[|\Z)", cards, re.M | re.S)
    results.append(check(m is not None and "步兵第190团" in m.group(1),
                         "卡表里 [i190] 存在且其 name 为 步兵第190团"))
    eff = re.search(r"^effect\s*=\s*(.*)$", m.group(1), re.M).group(1) if m else ""
    results.append(check(eff.startswith("FriendlyTurnBegin:"),
                         "它的 effect 以 FriendlyTurnBegin: 开头（时点触发）"))
    results.append(check("Develop($deck)" in eff,
                         "它的效果是 Develop($deck)，会走 ShowCardChoice"))

    transition = re.search(r"public async void OnNextTurnButtonPressed\(\)(.*?)\n    \}", battle, re.S)
    tbody = transition.group(1) if transition else ""
    results.append(check("ForbidControl();" in tbody and "RunTurnTransitionAsync()" in tbody,
                         "OnNextTurnButtonPressed 把 RunTurnTransitionAsync 包在 ForbidControl 里"))
    results.append(check("AllowControl();" in tbody,
                         "且要到 RunTurnTransitionAsync 返回之后才 AllowControl"))

    # ------------------- 3) 对照组：手牌打出的指令卡不受此影响 -------------------
    print("\n--- 3) 对照：手牌打出的指令卡本来就能点 ---")
    m2 = re.search(r"^\[紧急投产\]\n(.*?)(?=^\[|\Z)", cards, re.M | re.S)
    eff2 = re.search(r"^effect\s*=\s*(.*)$", m2.group(1), re.M).group(1) if m2 else ""
    results.append(check(eff2.startswith("Develop($deck)"),
                         "[紧急投产] 无时点前缀，从手牌打出，故一直正常——与实机反馈一致"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
