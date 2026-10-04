#!/usr/bin/env python3
"""火炮/轰炸机无视守护、事件期间能开商店与卡组、标准弹药改打「手牌+牌堆」。

⑩ 守护的判定原先对所有兵种一视同仁，火炮与轰炸机也被前排的守护单位挡住。
   豁免必须写在 `IsTargetProtectedByGuardian` 这一个地方——玩家侧的 Attack() 与
   敌方 AI 选目标都调它，规则天然一致，不会出现「玩家能打、AI 不能打」。
⑥ 进事件时只收起任务面板（保留本次抽到的一批），暗幕不再拦鼠标，
   好让世界地图上的商店/卡组按钮还能点到；挡区域按钮的活改由 WorldMap 自己管。
⑦ 标准弹药从「每抽 1 张再减 1 费」改成一次性把手牌与牌堆里的友方卡都减 1 费。
   牌堆里的卡是「已实例化但不在场景树上」的对象，费用滚动动画必须能识别这种情况。
"""
import configparser
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
CARD = ROOT / "bin" / "cardBase_.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
EVENT = ROOT / "bin" / "EventScene.cs"
WORLD = ROOT / "bin" / "WorldMap.cs"
CARD_INI = ROOT / "cards" / "card.ini"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def read_ini(path):
    parser = configparser.RawConfigParser()
    parser.optionxform = str
    parser.read(path, encoding="utf-8")
    return parser


def main():
    results = []
    card = CARD.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    event = EVENT.read_text(encoding="utf-8")
    world = WORLD.read_text(encoding="utf-8")
    cards = read_ini(CARD_INI)

    # ==================== ⑩ 火炮与轰炸机无视守护 ====================
    print("--- ⑩ 火炮/轰炸机无视守护 ---")
    results.append(check("public static bool IgnoresGuardian(CardTypes type)" in card,
                         "豁免规则收敛成一个具名函数"))
    bypass = card[card.index("public static bool IgnoresGuardian"):]
    bypass = bypass[:bypass.index("public void RefreshUnit()")]
    results.append(check("type == CardTypes.Artillery" in bypass and "type == CardTypes.Bomber" in bypass,
                         "火炮与轰炸机都在豁免名单里"))
    results.append(check("IsFriend" not in bypass, "豁免只看兵种、不看阵营——双方同一套规则"))

    # 判定必须排在烟幕等分支之前，否则某些路径会先返回
    guardian = battle[battle.index("private bool IsTargetProtectedByGuardian"):]
    guardian = guardian[:guardian.index("var targetPlace = target.GetMyPlace();")]
    results.append(check("cardBase_.IgnoresGuardian(attacker.cardType)" in guardian,
                         "守护判定里接了这条豁免"))
    results.append(check(
        guardian.index("IgnoresGuardian") < guardian.index("HasSmokeScreenActive"),
        "豁免排在烟幕判定之前，玩家侧与 AI 侧都吃得到",
    ))

    # 单一来源：AI 选目标和玩家攻击都只调这一个函数，不各写一套兵种判断
    results.append(check(battle.count("IsTargetProtectedByGuardian(") >= 3,
                         "守护判定被玩家攻击与敌方 AI 共同调用（同一函数）"))

    # ==================== ⑥ 事件期间能看商店与卡组 ====================
    print("\n--- ⑥ 事件期间允许查看商店与卡组 ---")
    results.append(check("bg.MouseFilter = Control.MouseFilterEnum.Ignore;" in event,
                         "事件暗幕不再拦鼠标"))
    results.append(check("map.EnterEventOverlay();" in event, "进事件时通知世界地图"))
    results.append(check("wm.ExitEventOverlay();" in event, "事件结算后交还"))

    results.append(check("private bool _eventOverlayActive;" in world, "世界地图有事件期状态位"))
    enter = world[world.index("public void EnterEventOverlay()"):world.index("public void ExitEventOverlay()")]
    results.append(check("CloseMissionPanel();" in enter, "收起任务面板"))
    results.append(check("DismissChooseMission" not in enter,
                         "只收面板、不动批次——事件结束后玩家回地图看到的还是同一批"))
    results.append(check("_eventOverlayActive = true;" in enter, "标记事件期"))
    exit_ = world[world.index("public void ExitEventOverlay()"):]
    exit_ = exit_[:exit_.index("/// <summary>只关闭面板")]
    results.append(check("DismissChooseMission();" in exit_, "事件结束才丢弃这一批（烈度已消耗）"))

    area = world[world.index("private void OnAreaPressed(string areaName)"):]
    area = area[:area.index("BattleStateManager.SelectedArea = areaName;")]
    results.append(check("_eventOverlayActive" in area,
                         "事件期点区域按钮被忽略——否则会在事件背后又叠一个任务面板"))

    # 叠层关系：商店 Layer 10 > 事件 Layer 2，天然盖在事件之上
    store_layer = re.search(r"void _on_store_pressed\(\)[\s\S]*?canvasLayer\.Layer = (\d+);", world)
    results.append(check(store_layer is not None and int(store_layer.group(1)) > 2,
                         "商店 CanvasLayer 高于事件层，盖在事件之上"))
    results.append(check("var scene = new EventScene { Layer = 2 };" in event, "事件层仍为 2"))

    # ==================== ⑦ 标准弹药 ====================
    print("\n--- ⑦ 标准弹药：手牌 + 牌堆 全卡 -1 费 ---")
    ammo = cards["标准弹药"]
    effect = ammo["effect"]
    results.append(check("${allCardInHand.friend}" in effect, "遍历手牌"))
    results.append(check("${deck.friend}" in effect, "遍历牌堆"))
    results.append(check(effect.count("subCost(1)") == 2,
                         "两处各减 1 费（手牌一次、牌堆一次）"))
    results.append(check("FriendlyCardDrawn" not in effect and "GetEffect" not in effect,
                         "去掉了「每抽 1 张再 -1」的持续效果"))
    results.append(check("foreach" in effect and effect.count("End&") == 2,
                         "两段 foreach 各有自己的 End&"))
    results.append(check("费用-1" in ammo["description"], "描述同步改成一次性减费"))

    # 牌堆里的卡不在场景树上，改费用时不能去碰 GetTree()
    roll = card[card.index("private async Task AnimateCostRoll("):]
    roll = roll[:roll.index("int steps = Math.Abs(toValue - fromValue);")]
    results.append(check("if (!IsInsideTree()) return;" in roll,
                         "费用滚动动画在「卡不在场景树上」时直接跳过（牌堆卡）"))
    results.append(check(
        roll.index("IsInsideTree") < roll.index("GetNode<Label>(\"cost\")"),
        "这层保护在取节点之前，否则照样空引用",
    ))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
