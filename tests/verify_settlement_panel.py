#!/usr/bin/env python3
"""战斗结算面板验证

背景：结算面板原先用代码绘制（End.cs 的 ShowSettlement），并且把物资点的
评分系数又写了一遍——陆军 *4、空军 *5、总部 /3——而 CalculateMaterialPoints
里早已改成 *3、*4、/2 过。结果是面板四行明细相加不等于底部显示的总额。

现在系数集中在 bin/BattleScore.cs，计算与显示共用同一组函数；面板本身
搬到 bin/settlement_panel.tscn，End.cs 只负责填数值与等待确认。

覆盖：
- 冒烟：场景文件存在且能解析出所需节点
- 基本：battleField.tscn 确实挂载了该场景；End.cs 按名取到各节点
- 回归：计算与显示都不再自己写系数算术，只调用 BattleScore
- 边界：系数常量只在 BattleScore.cs 定义一处
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SCENE = ROOT / "bin" / "settlement_panel.tscn"
END_CS = ROOT / "End.cs"
BATTLE_CS = ROOT / "bin" / "battlefield_.cs"
SCORE_CS = ROOT / "bin" / "BattleScore.cs"
BATTLE_SCENE = ROOT / "bin" / "battleField.tscn"
CARD_BASE_CS = ROOT / "bin" / "cardBase_.cs"

# 面板需要 End.cs 按名取得的节点
REQUIRED_NODES = [
    "Settlement", "Defeat",
    "LandRow", "AirRow", "DeadRow", "HqRow", "EnemyHqRow",
    "Total", "ConfirmButton", "ReturnButton",
]

# 原始系数算术的写法（出现即说明又绕过了 BattleScore）
RAW_ARITHMETIC = {
    "End.cs": [r"landKilled\s*\*", r"airKilled\s*\*", r"hqDefenceLost\s*/"],
    "battlefield_.cs": [r"_battleEnemyLandKilled\s*\*", r"_battleEnemyAirKilled\s*\*", r"hqLost\s*/"],
}


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
    missing = [p.name for p in (SCENE, END_CS, BATTLE_CS, SCORE_CS, BATTLE_SCENE) if not p.exists()]
    if missing:
        print(f"[FAIL] 冒烟测试：缺少文件 {missing}")
        return 1

    scene = SCENE.read_text(encoding="utf-8")
    end = END_CS.read_text(encoding="utf-8")
    battle = BATTLE_CS.read_text(encoding="utf-8")
    score = SCORE_CS.read_text(encoding="utf-8")
    battle_scene = BATTLE_SCENE.read_text(encoding="utf-8")

    node_names = re.findall(r'^\[node name="([^"]+)"', scene, re.M)
    results = [
        check(len(node_names) > 0, f"冒烟：场景解析出 {len(node_names)} 个节点"),
    ]

    # --- 场景结构 ---
    absent = [n for n in REQUIRED_NODES if n not in node_names]
    results.append(check(not absent, f"场景包含全部所需节点（缺 {absent}）"))

    results.append(
        check("res://bin/settlement_panel.tscn" in battle_scene,
              "battleField.tscn 挂载了结算面板场景")
    )
    results.append(
        check('instance=ExtResource' in battle_scene and 'name="SettlementOverlay"' in battle_scene,
              "面板以实例方式挂在 end 节点下，根节点名为 SettlementOverlay")
    )
    results.append(
        check(scene.count("visible = false") >= 2,
              "两个面板默认隐藏，由代码控制显示时机")
    )
    results.append(
        check("uid://" not in scene,
              "手写场景不写 uid（沿用 settings_menu.tscn 等先例，避免与现有资源撞车）")
    )

    # --- End.cs 取节点 ---
    resolved = [n for n in REQUIRED_NODES if f'"/{n}"' in end or f'"{n}"' in end]
    results.append(
        check(len(resolved) == len(REQUIRED_NODES),
              f"End.cs 按名引用全部节点（未引用 {sorted(set(REQUIRED_NODES) - set(resolved))}）")
    )

    # --- 输入可达性 ---
    # Godot 的 GUI 拾取按树序、后加入者优先，不读 z_index（z_index 只管绘制）。
    # 全屏遮罩是 MouseFilter.Stop 且在 _Ready 中才 AddChild，若面板排在它之前，
    # 按钮永远收不到点击。
    results.append(
        check("MoveChild(panelRoot, GetChildCount() - 1)" in end,
              "面板在 _Ready 中被移到全屏遮罩之后，否则按钮收不到点击")
    )
    results.append(
        check('_overlay.MouseFilter = Control.MouseFilterEnum.Stop' in end,
              "遮罩保持 Stop（变暗时拦住对战场操作），故必须靠树序让面板优先拾取")
    )

    # --- 回归：不再自己写系数 ---
    for filename, patterns in RAW_ARITHMETIC.items():
        source = end if filename == "End.cs" else battle
        hits = [p for p in patterns if re.search(p, source)]
        results.append(
            check(not hits,
                  f"{filename} 不含评分系数的原始算术（命中 {hits}）——系数应只来自 BattleScore")
        )

    results.append(
        check("BattleScore.Total(" in battle,
              "CalculateMaterialPoints 通过 BattleScore.Total 计算总额")
    )
    results.append(
        check(len(re.findall(r"BattleScore\.\w+\(", end)) >= 5,
              "结算面板每行明细各自调用 BattleScore 的分量函数")
    )

    # --- 对敌方总部的伤害 ---
    # 击杀曾是唯一的得分来源，于是「敌方不派兵、只加固总部」的关卡（加里宁）
    # 无论打得多好都结算为 0。此分量取自卡牌自身的 LoseDefence 累计值，
    # 治疗不会抵消，因此能如实反映"边打边回血"的关卡里玩家打出的总量。
    card = CARD_BASE_CS.read_text(encoding="utf-8")
    lose_defence = extract_method(card, "public async Task LoseDefence(int n)")
    results.append(
        check(bool(lose_defence) and "totalDefenceLost" in lose_defence,
              "LoseDefence 累加 totalDefenceLost（伤害统计的唯一累加点）")
    )
    results.append(
        check("ReadTotalDefenceLost" in card,
              "卡牌暴露 ReadTotalDefenceLost 供结算读取")
    )
    results.append(
        check("EnemyHqDamagePerPoint" in score and "EnemyHqDamagePoints" in score,
              "BattleScore 提供对敌方总部伤害的分值换算")
    )
    results.append(
        check("ReadTotalDefenceLost()" in battle and "LastBattleEnemyHqDamage" in battle,
              "CalculateMaterialPoints 读取该累计值并写入结算字段")
    )
    results.append(
        check("LastBattleEnemyHqDamage = 0" in
              (ROOT / "bin" / "CardRestoration.cs").read_text(encoding="utf-8"),
              "整局重置时清零该字段")
    )

    # --- 边界：系数只定义一处 ---
    for const in ("LandKillPoints", "AirKillPoints", "HqDefencePerPenalty"):
        results.append(
            check(f"public const int {const}" in score,
                  f"BattleScore 以具名常量定义 {const}")
        )

    other_files_with_const = [
        name for name, src in (("End.cs", end), ("battlefield_.cs", battle))
        if "LandKillPoints" in src or "AirKillPoints" in src
    ]
    results.append(
        check(not other_files_with_const,
              f"系数常量不在别处重复定义（重复于 {other_files_with_const}）")
    )

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
