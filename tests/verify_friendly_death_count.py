#!/usr/bin/env python3
"""阵亡统计验证

背景：阵亡/战果计数原先写在 RemoveCard() 内部，但 RemoveCard 是通用的卡牌
移除函数——弃牌、指令卡结算、ShowCardChoice 清理选项卡、手牌溢出都会调用它。
计数没有 state 判断，于是这些非阵亡的移除被一并算作阵亡，并连带扣减
CalculateMaterialPoints 的物资点。现已把计数移到 ProcessDeadUnitAsync，
它是死亡流程的唯一入口，上游已用 IsDeadPlacedUnit 筛过。

覆盖：
- 冒烟：能定位并提取两个方法
- 基本：RemoveCard 不含计数；ProcessDeadUnitAsync 含三个计数
- 边界：计数受 isHq != HQ.hq 守卫（总部不计入战果/阵亡）
- 边界：保留显式 friend/enemy 判断（IsFriend 另有 neutral/enemyNeutral，
  写成裸 else 会把中立单位算作友方阵亡）
- 回归：会调用 RemoveCard 的非死亡路径确实存在，证明该约束有意义
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BATTLE = ROOT / "bin" / "battlefield_.cs"
CARD = ROOT / "bin" / "cardBase_.cs"

COUNTERS = ["_battleEnemyLandKilled++", "_battleEnemyAirKilled++", "_battleFriendlyDead++"]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def extract_method(source, signature):
    """按大括号配对截取方法体，找不到时返回空串。"""
    start = source.find(signature)
    if start < 0:
        return ""
    brace = source.find("{", start)
    if brace < 0:
        return ""
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[brace : index + 1]
    return ""


def main():
    if not BATTLE.exists() or not CARD.exists():
        print("[FAIL] 冒烟测试：缺少 battlefield_.cs 或 cardBase_.cs")
        return 1

    source = BATTLE.read_text(encoding="utf-8")
    card_source = CARD.read_text(encoding="utf-8")

    remove_card = extract_method(source, "public void RemoveCard(cardBase_ card)")
    process_dead = extract_method(source, "private async Task ProcessDeadUnitAsync(cardBase_ deadUnit)")

    results = [
        check(bool(remove_card), "冒烟：可提取 RemoveCard"),
        check(bool(process_dead), "冒烟：可提取 ProcessDeadUnitAsync"),
    ]

    # --- 基本：计数位置 ---
    results.append(
        check(
            not any(counter in remove_card for counter in COUNTERS),
            "RemoveCard 不含阵亡/战果计数（弃牌等非阵亡移除不会被计入）",
        )
    )
    missing = [counter for counter in COUNTERS if counter not in process_dead]
    results.append(
        check(not missing, f"ProcessDeadUnitAsync 含全部三个计数（缺 {missing}）")
    )

    # --- 边界：总部守卫 ---
    results.append(
        check(
            re.search(r"if\s*\(\s*deadUnit\.isHq\s*!=\s*HQ\.hq\s*\)", process_dead) is not None,
            "计数受 deadUnit.isHq != HQ.hq 守卫，总部不计入",
        )
    )

    # --- 边界：不能写成裸 else ---
    friend_branch = re.search(
        r"else\s+if\s*\(\s*deadUnit\.GetIsFriend\(\)\s*==\s*IsFriend\.friend\s*\)",
        process_dead,
    )
    bare_else = re.search(r"\}\s*else\s*\{[^}]*_battleFriendlyDead\+\+", process_dead)
    enum_values = re.findall(r"^\s{4}(\w+),?\s*$", extract_method(card_source, "public enum IsFriend"), re.M)
    results.append(
        check(
            friend_branch is not None and bare_else is None,
            f"友方分支保留显式判断，未写成裸 else（IsFriend 实为 {len(enum_values)} 值：{'/'.join(enum_values)}）",
        )
    )

    # --- 回归：非死亡路径确实调用 RemoveCard ---
    discard_paths = {
        "CardDiscardAndRemove": "public async Task CardDiscardAndRemove(cardBase_ card)",
        "ShowCardChoice": "private async Task<cardBase_> ShowCardChoice",
    }
    for name, signature in discard_paths.items():
        body = extract_method(source, signature)
        results.append(
            check("RemoveCard(" in body, f"回归：{name} 仍会调用 RemoveCard（该约束有意义）")
        )

    results.append(
        check(
            source.count("cardInPlaces.Remove(card)") >= 1,
            "回归：RemoveCard 仍负责从 cardInPlaces 移除节点",
        )
    )

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
