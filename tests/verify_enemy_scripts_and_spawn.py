#!/usr/bin/env python3
"""敌方脚本里的「弃置玩家的牌」、按费用刷兵、以及关卡开局效果。

三件事的共同点是：**失效时一声不吭**。写错了既没有报错也没有效果，
只能靠对局里「怎么没反应」发现。所以这里逐条钉住。

④ DiscardRandomly 是按「效果来源卡的阵营」派发的。写在 enemyTurn.ini 里的战役行动，
   来源卡是敌方总部，于是它去弃敌方自己的手牌——那是空的，对玩家毫无影响。
   新增 DiscardPlayerRandomly 专管「敌方让玩家弃牌」。
⑤ addANewUnitToBattlefieldWithCostAndType 的第二个参数原先只认字面量数字，
   传 &result 时正则整体不匹配、指令静默失效（[改装] 刷不出兵就是这个）。
⑪ battleStart= 关卡开局效果：战斗开始时结算一次，不进行动队列（不显示成敌方意图）。
"""
import configparser
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"
CARD_INI = ROOT / "cards" / "card.ini"
ENEMY_INI = ROOT / "cards" / "enemyTurn.ini"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def read_ini(path):
    """enemyTurn.ini / card.ini 都是大小写敏感的普通 ini，用 RawConfigParser。"""
    parser = configparser.RawConfigParser()
    parser.optionxform = str
    parser.read(path, encoding="utf-8")
    return parser


def main():
    results = []
    battle = BATTLE.read_text(encoding="utf-8")
    cards = read_ini(CARD_INI)
    battles = read_ini(ENEMY_INI)

    # ==================== ④ 敌方要能弃置玩家的手牌 ====================
    print("--- ④ 敌方弃置玩家手牌 ---")
    results.append(check("DiscardPlayerRandomly" in battle, "新指令 DiscardPlayerRandomly 已实现"))
    results.append(check(
        '"DiscardPlayerRandomly()"' in battle,
        "已登记进 ConsoleCommands（Tab 补全用）",
    ))

    # 新指令必须明确作用于 player1，而不是跟着来源阵营走
    i = battle.index('instruction.StartsWith("DiscardPlayerRandomly"')
    branch = battle[i:battle.index('instruction.StartsWith("DiscardRandomly"', i)]
    results.append(check("_ = player1.DiscardRandomly(count);" in branch,
                         "固定弃置 player1 的手牌，不看来源阵营"))
    results.append(check("sourceCard?.GetIsFriend()" not in branch,
                         "分支里不出现按阵营派发的旧写法"))

    # 旧指令保持原语义（card.ini 里「抽3张弃1张」那种自弃自己的卡还要用它）
    results.append(check(
        "player1.DiscardRandomly(count);" in battle and "player2.DiscardRandomly(count);" in battle,
        "DiscardRandomly 仍按来源阵营派发，老卡不受影响",
    ))

    # 配置侧：战役行动里的「弃置友方N张卡」必须改用新指令
    offenders = []
    for section in battles.sections():
        for key, value in battles[section].items():
            if re.search(r"(?<![A-Za-z])DiscardRandomly\s*\(", value):
                offenders.append(f"{section}.{key}")
    results.append(check(not offenders, f"战役行动里不再有裸的 DiscardRandomly（{len(offenders)} 条）"))
    for item in offenders:
        print(f"       仍是旧指令: {item}")

    # 描述必须与数字一致：t6 的说明曾写「弃2张」而实际是 1 张
    mismatched = []
    for section in battles.sections():
        for key, value in battles[section].items():
            m = re.search(r"DiscardPlayerRandomly\((\d+)\)", value)
            d = re.search(r"弃(\d+)张", value)
            if d and not m:
                mismatched.append(f"{section}.{key}: 说明写了数量却没用 DiscardPlayerRandomly")
            if m and d and m.group(1) != d.group(1):
                mismatched.append(f"{section}.{key}: 代码弃{m.group(1)}张，说明写{d.group(1)}张")
    results.append(check(not mismatched, f"弃牌数量说明与代码一致（不符 {len(mismatched)} 条）"))
    for item in mismatched:
        print(f"       {item}")

    # ==================== ⑤ 按费用刷兵要吃表达式 ====================
    print("\n--- ⑤ addANewUnitToBattlefieldWithCostAndType ---")
    j = battle.index('instruction.StartsWith("addANewUnitToBattlefieldWithCostAndType"')
    spawn = battle[j:battle.index("// GetPoint() - 获得指挥点", j)]

    # 关键回归：不能再出现只认字面量数字的 (\d+) 正则
    old_regex = re.search(r'Regex\.Match\(instruction,\s*@"\\\(\(\[\^,\]\+\)[^"]*\\d\+', spawn)
    results.append(check(old_regex is None, "不再用只认字面量数字的 (\\d+) 正则"))
    results.append(check("EvaluateExpression(match.Groups[2].Value" in spawn,
                         "第二个参数交给 EvaluateExpression 求值，&result 这类表达式可用"))
    results.append(check("int.Parse(" not in spawn, "不再用 int.Parse 直接吞字面量"))

    # 静默失效的三条出路都必须留日志，否则下次还是查不出来
    results.append(check("[SpawnByCost] 参数解析失败" in spawn, "参数解析失败有日志"))
    results.append(check("[SpawnByCost] 卡池里没有可用的" in spawn, "找不到可选卡时有日志"))
    results.append(check("[SpawnByCost]" in spawn and "支援阵线已满" in spawn,
                         "支援阵线满员（唯一会「什么都不做」的正当理由）有日志"))

    # 配置侧：[改装] 用的正是表达式写法，是这个 bug 的观察点
    modifier = cards["改装"]
    results.append(check("&result" in modifier["effect"],
                         "[改装] 的 effect 仍以 &result 传费用——修好后它才有兵可刷"))

    # ==================== ⑪ battleStart= 开局效果 ====================
    print("\n--- ⑪ 关卡开局效果 battleStart= ---")
    results.append(check('private const string BattleStartKey = "battleStart";' in battle,
                         "键名收敛成常量，不散落字面量"))
    results.append(check("private string _battleStartEffect" in battle, "有存放开局效果的字段"))
    results.append(check("private async Task RunBattleStartEffectAsync()" in battle, "有开局效果的执行函数"))
    results.append(check("await ParseAndExecuteEffect(_battleStartEffect, enemyHq, null);" in battle,
                         "来源卡固定为敌方总部"))

    # 「若xxx则xxx」靠效果脚本自己的 if 跳转 + &hp 变量，不写死成特例
    results.append(check('case "hp":' in battle and "BattleStateManager.Hp" in battle,
                         "新增 &hp 变量读战役血量"))
    results.append(check("/10" not in battle[battle.index('case "hp":'):battle.index('case "hp":') + 200],
                         "&hp 读的是原始血量，没有偷偷做换算"))

    # 开局效果不能进行动队列，否则会作为一条「敌方意图」显示在左侧面板上
    queue = battle[battle.index("enemyActionQueue.Clear();"):battle.index("GD.Print($\"Loaded enemy action queue")]
    results.append(check("key.Equals(BattleStartKey, StringComparison.OrdinalIgnoreCase)" in queue
                         and "_battleStartEffect = value;" in queue and "continue;" in queue,
                         "battleStart 被单独摘出，不会进队列"))
    results.append(check("enemyActionQueue.Clear();" in queue and "_battleStartEffect = \"\";" in queue,
                         "换关卡时开局效果一并清空，不沿用上一关"))

    # 时序：开局效果必须排在起手抽牌/换牌之前
    start = battle[battle.index("private async Task StartBattleAsync()"):]
    start = start[:start.index("private async Task StartOpeningHandAsync()")]
    results.append(check("await RunBattleStartEffectAsync();" in start
                         and start.index("RunBattleStartEffectAsync") < start.index("StartOpeningHandAsync"),
                         "先结算开局效果，再走起手抽牌与换牌"))
    results.append(check("_ = StartBattleAsync();" in battle and "_ = StartOpeningHandAsync();" not in battle,
                         "_Ready 接的是 StartBattleAsync"))

    # 柏林那关确实写了
    results.append(check("battleStart" in battles["berlin_final_battle"],
                         "[berlin_final_battle] 配了 battleStart"))
    berlin = battles["berlin_final_battle"].get("battleStart", "")
    results.append(check("myHq|heal(&hp*10)" in berlin,
                         "柏林：每有 1 条命，友方总部 +10 防御"))
    results.append(check(re.search(r"\[icon=\w+,\s*description=[^\]]+\]", berlin) is not None,
                         "开局效果也按约定带 icon/description"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
