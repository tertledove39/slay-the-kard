#!/usr/bin/env python3
"""战斗里的「行动能力」与「动作收尾」时序。

本批修的四件事，都属于「状态该结束时没结束」：

① 拖拽中被截图工具抢走鼠标 → 「松开左键」永远送不进来 → 卡永久停在 caught。
   收尾不能指望一个可能永远不来的事件，必须有外部兜底。
② 全场刷新只在 EnemyTurnAsync 开头跑一次 → 敌方回合里被效果刷进场的单位
   （近卫步兵272团亡计拉出的 IS-2）到友方回合还没被刷过，整回合动不了。
③ RetreatUnit 的敌方分支漏了 DisableCombatAbility → 弃置动画播着还能被 AI 选去攻击。
④ 多张单位卡弃置是一张播完再播下一张，3 张要 10 秒。

这几条全是「看起来在对局里偶发」的问题，靠静态断言钉住最省事。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    """截取从 start 到其后的 end 之间的源码。"""
    i = text.index(start)
    return text[i:text.index(end, i)]


def main():
    results = []
    battle = BATTLE.read_text(encoding="utf-8")

    # ============================ ① 拖拽的兜底收尾 ============================
    print("--- ① 拖拽被外部打断时要能安全取消 ---")
    results.append(check("private void CancelCurrentDrag()" in battle, "有 CancelCurrentDrag()"))
    cancel = method(battle, "private void CancelCurrentDrag()", "public override void _Notification")

    # 归位：还在手上/地上的两种中间态都要还原，否则 RefreshMyHand 永远跳过它
    results.append(check("CardState.inHand" in cancel, "caught 的卡还原成 inHand"))
    results.append(check("CardState.placed" in cancel, "inplaceAndCaught 的卡还原成 placed"))
    results.append(check("RestoreAllTargetsColor()" in cancel, "取消高亮，恢复单位颜色"))
    results.append(check("RefreshMyHand()" in cancel, "重新排列手牌，让卡真正归位"))

    # 必须在控制锁判定之前兜底：收尾不能被「当前不允许操作」挡住
    motion_guard = '!Input.IsMouseButtonPressed(MouseButton.Left)'
    results.append(check(motion_guard in battle, "核对物理左键状态作为兜底"))
    results.append(check(
        battle.index(motion_guard) < battle.index("if (ReadControlState() == 1) return;"),
        "兜底判定位于控制锁之前（否则解锁前永远轮不到它）",
    ))

    # 截图工具/Alt-Tab 会抢焦点，这条是最可靠的一路
    results.append(check(
        "public override void _Notification(int what)" in battle,
        "有 _Notification 处理窗口失焦",
    ))
    results.append(check(
        "NotificationApplicationFocusOut" in battle and "CancelCurrentDrag();" in battle,
        "失焦时调用 CancelCurrentDrag",
    ))

    # ============================ ② 按阵营刷新 ============================
    print("\n--- ② 行动能力按阵营、各自回合开头刷新 ---")
    results.append(check(
        "void RefreshAllCardInField()" not in battle,
        "「一次性刷全部」的旧函数已移除",
    ))
    results.append(check(
        "void RefreshCardsInField(IsFriend side)" in battle,
        "改为带阵营参数的 RefreshCardsInField(side)",
    ))
    results.append(check(
        "RefreshCardsInField(IsFriend.enemy);" in battle,
        "敌方回合开头刷敌方",
    ))
    results.append(check(
        "RefreshCardsInField(IsFriend.friend);" in battle,
        "友方回合开头刷友方——这正是 IS-2 能动起来的原因",
    ))

    enemy_turn = method(battle, "async Task EnemyTurnAsync()", "await ApplyEnemyTurnStartTraits();")
    results.append(check(
        "RefreshCardsInField(IsFriend.enemy);" in enemy_turn,
        "敌方刷新仍在敌方回合开头，时序未变",
    ))

    friendly_begin = method(battle, "await EnemyTurnAsync();", "await ApplyTurnStartTraits();")
    results.append(check(
        "RefreshCardsInField(IsFriend.friend);" in friendly_begin,
        "友方刷新在 FriendlyTurnBegin 时点之前（时点里读到的 attackCountThisTurn 仍是 0）",
    )
    )
    results.append(check(
        friendly_begin.index("RefreshCardsInField(IsFriend.friend);")
        < friendly_begin.index('TriggerUnitEffects("FriendlyTurnBegin"'),
        "刷新确实排在 FriendlyTurnBegin 之前，不是之后",
    ))

    # ============================ ③ 撤退立刻禁战 ============================
    print("\n--- ③ 被撤退/弃置的单位在动画期间不能攻击 ---")
    retreat = method(battle, "private async Task RetreatUnit(cardBase_ unit)", "await Task.Delay(RetreatSettleDelayMs);")
    # 两种结局都要先关战斗能力：回手牌那条分支、以及敌方直接弃置那条分支
    results.append(check(
        retreat.count("unit.DisableCombatAbility();") == 2,
        "友方回手牌与敌方弃置两条分支都关了战斗能力",
    ))
    discard_branch = retreat[retreat.index("无法返回手牌或敌方单位"):]
    results.append(check(
        discard_branch.index("DisableCombatAbility") < discard_branch.index("AddChange(ChangeType.DiscardCard"),
        "弃置分支里关能力发生在挂待弃置标记之前",
    ))
    results.append(check("await Task.Delay(100);" not in battle, "撤退缓冲不再是裸的魔鬼数字"))

    # ============================ ④ 弃置错开起飞 ============================
    print("\n--- ④ 多张单位卡弃置要错开，不能一张播完再播下一张 ---")
    results.append(check("DiscardStaggerSeconds" in battle, "错开间隔是具名常量，不是魔鬼数字"))
    m = re.search(r"private const float DiscardStaggerSeconds = ([\d.]+)f;", battle)
    results.append(check(m is not None and float(m.group(1)) == 0.5, "间隔为 0.5 秒"))
    results.append(check("private async Task DiscardUnitsWithStagger(" in battle, "有批量弃置的辅助函数"))
    stagger = method(battle, "private async Task DiscardUnitsWithStagger(", "private async Task FinishUnitDiscard(")
    results.append(check("Task.WhenAll(tasks)" in stagger, "动画并行播放后统一等待"))
    # 注意括号：`a in b is False` 在 Python 里是链式比较，会变成 (a in b) and (b is False)
    results.append(check(("DiscardCard()" in stagger) is False, "辅助函数自己不播动画——交给 FinishUnitDiscard"))
    results.append(check(
        "if (i < cards.Count - 1)" in stagger,
        "单张时不额外等待，行为与从前一致",
    ))
    results.append(check(
        "card.isDiscarding = true;" in stagger,
        "置 isDiscarding，动画期间不被刷新显示顺序打回默认 ZIndex",
    ))
    # 关战斗能力要在起飞之前一次性做完
    loop = method(battle, "var discardUnits = allCards.Where", "deathCheckRequested |= deadUnits.Count > 0;")
    results.append(check(
        loop.index("DisableCombatAbility") < loop.index("await DiscardUnitsWithStagger"),
        "先统一关掉全部战斗能力，再开始播动画",
    ))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
