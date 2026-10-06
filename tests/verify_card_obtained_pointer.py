#!/usr/bin/env python3
"""「刚入手的卡」指针：凡是让卡进入手牌的效果路径都必须记一笔。

项目里有**两套互相独立**的指针，由两条不同指令读取：

    GetCardsBeingTreated     → Player.lastDrawnCards      （Player.GetLastDrawnCards()）
    GetCardBeingAddToHand    → battlefield_.lastCardAddedToHand

只写其中一个，另一条指令就会拿到**上一次入手的卡**（或空列表）。全程不报错，
只表现为「后续指令作用到了别的卡上」——`[紧急投产]` 的
`Develop($deck)|GetCardsBeingTreated|setCost(0)` 就是这么减不到开发出来的那张卡上的，
因为 Develop 当时两套都没写。

修法是把已有的「加入手牌」指令里那段记账抽成 `RecordCardsObtained()`，
让 Develop 与「加入手牌」指令共用同一处实现（而不是再抄一份）。
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

    m = re.search(r"private void RecordCardsObtained\(List<cardBase_> cards, IsFriend side\)(.*?)\n    \}",
                  battle, re.S)
    results.append(check(m is not None, "RecordCardsObtained(List<cardBase_>, IsFriend) 可定位"))
    body = m.group(1) if m else ""

    # --------------------------- 1) 两套指针都写 ---------------------------
    print("\n--- 1) 两套指针必须都写 ---")
    results.append(check("lastCardAddedToHand = cards[cards.Count - 1];" in body,
                         "写 battlefield_.lastCardAddedToHand（给 GetCardBeingAddToHand 用）"))
    results.append(check("player1.SetLastDrawnCards(cards);" in body,
                         "写 player1.lastDrawnCards（给 GetCardsBeingTreated 用）"))
    results.append(check("player2.SetLastDrawnCards(cards);" in body,
                         "敌方分支同样写 player2"))
    results.append(check("cards == null || cards.Count == 0" in body, "空列表守卫"))

    # --------------------------- 2) 调用点齐全（回归） ---------------------------
    print("\n--- 2) 所有「让卡入手」的效果路径都调了它（回归） ---")
    results.append(check(battle.count("RecordCardsObtained(") == 5,
                         "RecordCardsObtained 共 5 处出现（1 处定义 + 4 处调用）"))
    # Develop 的两个分支
    dev = re.search(r"if \(instruction\.StartsWith\(\"Develop\".*?\n                    \}\n",
                    battle, re.S)
    dev_body = dev.group(0) if dev else ""
    results.append(check("RecordCardsObtained(new List<cardBase_> { selectedCard }, IsFriend.friend);" in battle,
                         "Develop 的友方分支记录了开发出的卡"))
    results.append(check("RecordCardsObtained(new List<cardBase_> { selectedCard }, IsFriend.enemy);" in battle,
                         "Develop 的敌方分支同样记录"))
    # 「加入手牌」指令
    add = re.search(r"List<cardBase_> addedCards = new List<cardBase_>\(\);(.*?)\n                        \}",
                    battle, re.S)
    add_body = add.group(1) if add else ""
    results.append(check("RecordCardsObtained(addedCards, IsFriend.friend);" in add_body,
                         "「加入手牌」指令也改走同一入口，没有留下重复实现"))

    # --------------------------- 3) 单一实现 ---------------------------
    print("\n--- 3) 写入点单一（配置项/知识单一） ---")
    writes = re.findall(r"^\s*lastCardAddedToHand\s*=", battle, re.M)
    results.append(check(len(writes) == 1, "lastCardAddedToHand 只在 RecordCardsObtained 里被赋值一处"))
    results.append(check("lastCardAddedToHand = card;" not in battle,
                         "「加入手牌」指令里原来的内联赋值已移除"))

    # --------------------------- 4) 指令确实读这两套指针 ---------------------------
    print("\n--- 4) 两条指令确实读的是这两套指针（防空转断言） ---")
    results.append(check("ins == \"getcardsbeingtreated\"" in battle
                         and "player1.GetLastDrawnCards()" in battle,
                         "GetCardsBeingTreated 读 Player.GetLastDrawnCards()"))
    results.append(check("ins == \"getcardbeingaddtohand\"" in battle
                         and "targets = new List<cardBase_> { lastCardAddedToHand };" in battle,
                         "GetCardBeingAddToHand 读 battlefield_.lastCardAddedToHand"))

    # --------------------------- 5) 触发用例存在 ---------------------------
    print("\n--- 5) 触发用例确实存在 ---")
    m2 = re.search(r"^\[紧急投产\]\n(.*?)(?=^\[|\Z)", cards, re.M | re.S)
    eff = re.search(r"^effect\s*=\s*(.*)$", m2.group(1), re.M).group(1) if m2 else ""
    results.append(check(eff.startswith("Develop($deck)|GetCardsBeingTreated"),
                         "[紧急投产] = Develop($deck)|GetCardsBeingTreated|... 紧跟其后就用该指针"))
    results.append(check("setCost(0)" in eff, "且用 setCost(0) 减费——正是本 bug 的观察点"))
    # 另一条路径（抽卡）也会写 lastDrawnCards，故给 GetCardsBeingTreated 的语义做交叉验证
    results.append(check("SetLastDrawnCards(new List<cardBase_> { card });" in battle,
                         "DrawCard 仍写 lastDrawnCards（抽牌路径未被改动）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
