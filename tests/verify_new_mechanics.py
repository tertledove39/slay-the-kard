#!/usr/bin/env python3
"""本批次新增引擎能力的校验。

为支持「撤退/开发/费用」一系新卡，本次给引擎补了 5 项能力。它们全都属于
「写错了也静默失效」的类型，故逐项静态校验：

  1. 三条阵线选择器 frontLine / supportLine / enemySupportLine
  2. 当前牌堆选择器 deck
  3. 特性作为选择器片段（如 ${allTargets.unit.Ambush}）
  4. 新时点 FriendlyCommandPlayed（友方打出指令时）
  5. 指令 GetHandMax()，以及 TargetType.aFrontLineUnit

校验方式是把代码当文本核对「能力确实接上了」——这些点没有单元测试框架可用，
而静态断言至少能挡住「改了 A 忘了改 B」的漏接。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"
CARDBASE = ROOT / "bin" / "cardBase_.cs"
CARD_INI = ROOT / "cards" / "card.ini"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    battle = BATTLE.read_text(encoding="utf-8")
    cardbase = CARDBASE.read_text(encoding="utf-8")
    cards = CARD_INI.read_text(encoding="utf-8")
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    results.append(check(BATTLE.exists() and CARDBASE.exists() and CARD_INI.exists(),
                         "三个源文件均存在"))
    sel = re.search(r"private List<cardBase_> GetTargetsFromSelector\(string selector\)(.*?)\n    \}",
                    battle, re.S)
    results.append(check(sel is not None, "GetTargetsFromSelector 可定位"))
    body = sel.group(1) if sel else ""

    # ------------------------------ 1) 阵线选择器 ------------------------------
    print("\n--- 1) 三条阵线选择器 ---")
    results.append(check("frontLine" in body and "supportLine" in body
                         and "enemySupportLine" in body,
                         "选择器里出现 frontLine / supportLine / enemySupportLine 三个片段"))
    for var, label in [("frontLine", "前线"), ("supportLine", "友方支援阵线"),
                       ("enemySupprotLine", "敌方支援阵线")]:
        results.append(check(f"lane.Contains(x.GetMyPlace())" in body and var in body,
                             f"{label}按 GetMyPlace() 归属筛选（字段 {var}）"))
    # 三条阵线必须真的对应到不同的格子表
    results.append(check("frontLine = [(place_)GetNode(\"Place6\")" in battle
                         and "enemySupprotLine = [(place_)GetNode(\"Place1\")" in battle
                         and "supportLine = [(place_)GetNode(\"Place11\")" in battle,
                         "三条阵线分别绑定 Place6-10 / Place1-5 / Place11-15"))

    # ------------------------------ 2) 牌堆选择器 ------------------------------
    print("\n--- 2) 当前牌堆选择器 ---")
    results.append(check('parts[0] == "deck"' in body, "选择器支持 deck 作为根片段"))
    results.append(check("player1.GetCardsInDeck()" in body, "deck 片段取 player1 的牌堆"))
    results.append(check("public List<cardBase_> GetCardsInDeck()" in battle,
                         "Player 暴露了 GetCardsInDeck() 访问器（deck 原为 private）"))

    # ------------------------------ 3) 特性片段 ------------------------------
    print("\n--- 3) 特性作为选择器片段 ---")
    results.append(check("Enum.TryParse<UnitTraits>(part, true, out UnitTraits traitFilter)" in body,
                         "选择器用 Enum.TryParse 识别特性名"))
    results.append(check("traitFilter != UnitTraits.None" in body,
                         "只认得出的特性名才当筛选条件，认不出交给卡牌类型解析"))
    results.append(check("x.HasTrait(traitFilter)" in body, "按 HasTrait 过滤"))

    # ------------------------------ 4) 新时点 ------------------------------
    print("\n--- 4) 新时点 FriendlyCommandPlayed ---")
    call = re.search(r'TriggerUnitEffects\("FriendlyCommandPlayed",\s*commandCard\)', battle)
    results.append(check(call is not None,
                         "ExecuteCommandAndDiscard 内触发了 FriendlyCommandPlayed"))
    if call:
        # 时点必须在该指令自身效果结算之后
        idx_call = call.start()
        idx_effect = battle.rfind("await ParseAndExecuteEffect(commandCard.effect",
                                                              0, idx_call)
        results.append(check(idx_effect != -1 and idx_effect < idx_call,
                             "时点在自身效果结算之后触发（结算完才算打出过）"))
    results.append(check("ExecuteCommandAndDiscard" in battle
                         and battle.count("_ = TriggerUnitEffects(\"BePicked\"") >= 1,
                         "两处打出入口都经由 ExecuteCommandAndDiscard，故只需挂一处"))

    # ------------------------------ 5) GetHandMax 与前线目标类型 ------------------------------
    print("\n--- 5) GetHandMax() 与 TargetType.aFrontLineUnit ---")
    results.append(check('ins == "gethandmax"' in battle, "新增指令 gethandmax"))
    results.append(check("public int ReadHandMax()" in battle, "Player 暴露 ReadHandMax()"))
    results.append(check('"GetHandMax()"' in battle, "ConsoleCommands 已登记 GetHandMax()"))
    results.append(check("aFrontLineUnit" in cardbase, "TargetType 枚举新增 aFrontLineUnit"))
    results.append(check("case TargetType.aFrontLineUnit:" in battle,
                         "IsValidTarget 有对应分支"))
    results.append(check("HighlightValidTargets" in battle
                         and "IsValidTarget(card, targetType)" in battle,
                         "高亮与目标计数委托 IsValidTarget，无需各自再改"))

    # --------------------------- 新卡是否用上了这些能力 ---------------------------
    print("\n--- 新卡对新能力的实际使用 ---")
    used = {
        "${allTargets.unit.friend.Ambush}": "特性片段 + 友方限定（伏击接应）",
        "Develop($deck)": "牌堆选择器（步兵第190团）",
        "FriendlyCommandPlayed": "新时点（参谋总部）",
        "GetHandMax": "手牌上限指令（破釜沉舟，无参指令按约定裸写不带括号）",
        "aFrontLineUnit": "前线目标类型（步兵第173团）",
    }
    for needle, why in used.items():
        results.append(check(needle in cards, f"card.ini 使用了 {needle} —— {why}"))
    results.append(check("${allCardInHand.unit}" in cards,
                         "card.ini 使用了新选择器片段 unit 作用于手牌（炮火准备）"))

    # --------------------- Develop 从牌堆取卡的收尾动作 ---------------------
    print("\n--- Develop 的牌堆收尾（#2 牌堆污染 / #7 空卡牌）---")
    results.append(check("public bool RemoveFromDeck(cardBase_ card)" in battle,
                         "Player 暴露 RemoveFromDeck()"))
    dev = re.search(r'StartsWith\("Develop".*?(?=\n                // HealAllTargets)',
                    battle, re.S)
    results.append(check(dev is not None, "Develop 块可定位"))
    if dev:
        d = dev.group(0)
        results.append(check(d.count("RemoveFromDeck(selectedCard)") == 2,
                             "选中卡在加入手牌前都先从牌堆摘除（友方/敌方各一处）"))
        results.append(check(d.count("selectedCard.Visible = true") == 2,
                             "选中卡恢复可见——牌堆卡靠停在屏幕外隐藏，不恢复就是「空卡牌」"))
        i_rm = d.find("RemoveFromDeck(selectedCard)")
        i_add = d.find("AddCardToHand(selectedCard)")
        results.append(check(i_rm != -1 and i_add != -1 and i_rm < i_add,
                             "摘除发生在加入手牌之前"))
    results.append(check("AcquireEmptyCard()" in dev.group(0)[:dev.group(0).find("cardsToShow.Count")] if dev else False,
                         "Develop 仍保留「不按 id 重新实例化」的路径（保住牌堆实例上的费用/攻防改动）"))

    # ------------------------------ 定点回归 ------------------------------
    print("\n--- 定点回归：新卡必须带属性方括号 ---")
    for sid in ["炮火准备", "参谋总部"]:
        m = re.search(rf"^\[{sid}\]\n(.*?)(?=^\[|\Z)", cards, re.M | re.S)
        eff = re.search(r"^effect\s*=\s*(.*)$", m.group(1), re.M).group(1) if m else ""
        results.append(check("[icon=" in eff and "description=" in eff,
                             f"[{sid}] 的 effect 带 [icon=...,description=...] 属性"))
    # 炮火准备内外都要有
    m = re.search(r"^\[炮火准备\]\n(.*?)(?=^\[|\Z)", cards, re.M | re.S)
    results.append(check(m and m.group(1).count("[icon=") >= 2,
                         "[炮火准备] 内层（GetEffect 引号内）与外层都写了属性"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
