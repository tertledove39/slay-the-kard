#!/usr/bin/env python3
"""卡牌对象被「回收再复用」时的生命周期状态。

这一批实机反馈里最凶的一条：**卡牌会莫名被弃掉、弃完还不回正**。
根因不在弃置逻辑本身，而在卡牌对象的复用——`SetCardInformation` 是每张卡唯一的
初始化入口，对象池（`ResourceManager.AcquireEmptyCard*`）与
`GetCardTemplate().Duplicate()`（卡组重建、战后奖励）两条路径都要走它，
而它原先只重置数值、没重置这两项：

- `shouldBeRemoved` 残留 1 —— 手牌也在 `cardInPlaces` 里（`AddCardToHand` 会调
  `AddToBattleField`），下一次死亡检查会把它当「待弃置」弃掉并移除。
- `isDiscarding` 残留 true —— `RefreshAllCardDisplayOrder` 会一直跳过它，卡不回正。

还有一条相邻的：三个属性 Label 的 `LabelSettings` 会被多张卡共用，而
`FlashAttributeWithColor` 直接改 `LabelSettings.FontColor`——改一张就连累全部
（「打出标准弹药后卡牌奖励里的 cost 全变绿、数值却没变」）。
"""
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
CARD = ROOT / "bin" / "cardBase_.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


def main():
    results = []
    card = CARD.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")

    setinfo = method(card, "public void SetCardInformation(CardData cardData)", "RefreshState();")

    # ==================== 对象池状态泄漏 ====================
    print("--- 复用卡牌时必须归零的生命周期状态 ---")
    results.append(check("shouldBeRemoved = 0;" in setinfo,
                         "SetCardInformation 重置 shouldBeRemoved"))
    results.append(check("isDiscarding = false;" in setinfo,
                         "SetCardInformation 重置 isDiscarding"))
    results.append(check("ChangeList.Clear();" in setinfo,
                         "SetCardInformation 清空挂起的变更（同一个卡可能带着上一轮的 ChangeList）"))

    # 置位点必须唯一（只有弃置流程会置位），否则说明有人在别处又把它写脏了
    results.append(check(card.count("shouldBeRemoved = 1;") == 1,
                         "shouldBeRemoved 的置位点全项目只有一处（弃置流程）"))

    # 单点保证：所有从池/模板取卡的路径都要经过 SetCardInformation
    results.append(check(battle.count("AcquireEmptyCard") >= 4 and "AcquireEmptyCardSync" in battle,
                         "对象池取卡路径确实存在（这条 bug 的前提）"))
    reward = (ROOT / "bin" / "PostBattleReward.cs").read_text(encoding="utf-8")
    results.append(check("GetCardTemplate().Duplicate()" in reward
                         and "cardBase_ GetCardTemplate()" in battle,
                         "模板 Duplicate 路径确实存在（另一条复用来源）"))

    # ==================== 属性颜色不得跨卡串色 ====================
    print("\n--- 属性文字颜色必须由各卡实例独占 ---")
    results.append(check("private void OwnAttributeLabelSettings(string labelName)" in card,
                         "有让 Label 独占 LabelSettings 的辅助函数"))
    own = method(card, "private void OwnAttributeLabelSettings(string labelName)",
                 "private string BuildTraitPrefix()")
    results.append(check("label.LabelSettings.Duplicate() as LabelSettings" in own,
                         "确实复制了一份而不是继续共用"))
    results.append(check("label?.LabelSettings == null" in own, "LabelSettings 为空时不炸"))

    for name in ["attack", "defence", "cost"]:
        results.append(check(f'OwnAttributeLabelSettings("{name}");' in setinfo,
                             f"SetCardInformation 里对 {name} 调用了独占化"))

    # 改色前必须确认不是「共用资源 + 看不见的卡」
    # 判据自 BUGS #69 起收成一个命名谓词 CanAnimate()（已释放 / 已离树），
    # 本文件与 AnimateCostRoll、MoveToPosition 共用它，不再各写各的。
    flash = method(card, "private async void FlashAttributeWithColor(",
                   'if (targetLabel == null)')
    results.append(check("if (!CanAnimate())" in flash,
                         "FlashAttributeWithColor 对不在场景树上的卡直接返回"))
    results.append(check(flash.index("CanAnimate()") < flash.index("GetNode<Label>"),
                         "这层保护在取节点之前"))

    # ==================== 待弃置单位不得被回合刷新复活 ====================
    print("\n--- 已宣布弃置的单位不能因回合刷新而复活 ---")
    refresh = method(card, "public void RefreshUnit()", "public void DisableCombatAbility()")
    results.append(check("shouldBeRemoved == 1" in refresh,
                         "RefreshUnit 里挡了 shouldBeRemoved == 1"))
    results.append(check(
        refresh.index("shouldBeRemoved == 1") > refresh.index("IsActionForbidden()"),
        "这层钳制排在恢复之后（和禁止行动类特性同一条规矩）",
    ))
    # 根因链：撤退只关能力，死亡检查之前还会跑一次敌方回合开头的刷新
    retreat = method(battle, "private async Task RetreatUnit(cardBase_ unit)",
                     "await Task.Delay(RetreatSettleDelayMs);")
    results.append(check("unit.DisableCombatAbility();" in retreat and "ChangeType.DiscardCard" in retreat,
                         "撤退同时关能力并挂待弃置标记"))
    enemy_turn = method(battle, "async Task EnemyTurnAsync()", "await ApplyEnemyTurnStartTraits();")
    results.append(check("RefreshCardsInField(IsFriend.enemy);" in enemy_turn,
                         "敌方回合开头确实会刷新全部敌方单位——所以必须在 RefreshUnit 里挡"))

    # ==================== 总部不得离开支援阵线 ====================
    print("\n--- 总部只应待在支援阵线 ---")
    # 实机复现：控制台敲 retreat 会把友方总部塞进手牌，之后 discardrandomly 抽中它
    # 就直接 RemoveCard(myHq) 判负。根因是 RetreatUnit 从不检查目标是不是总部
    # （控制台是 ParseAndExecuteEffect(cmd, myHq, null, myHq)，targets 就是总部）。
    retreat = method(battle, "private async Task RetreatUnit(cardBase_ unit)",
                     "await Task.Delay(RetreatSettleDelayMs);")
    results.append(check("unit.isHq == HQ.hq" in retreat,
                         "RetreatUnit 拒绝总部（唯一能把场上卡变成手牌卡的出口）"))
    results.append(check(retreat.index("isHq == HQ.hq") < retreat.index("AddCardToHand"),
                         "这层拦截在任何回手牌动作之前"))

    add_hand = method(battle, "public async Task AddCardToHand(cardBase_ card)", "maxHandSize")
    results.append(check("isHq == HQ.hq" in add_hand and "[AddCardToHand]" in add_hand,
                         "AddCardToHand 对总部留日志（进手牌的唯一入口，便于追调用方）"))

    discard_random = method(battle, "public async Task DiscardRandomly(int count)",
                            "public async Task DiscardCardsWithName")
    results.append(check("isHq == HQ.hq" in discard_random and "continue;" in discard_random,
                         "DiscardRandomly 跳过总部（双保险，且留日志）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
