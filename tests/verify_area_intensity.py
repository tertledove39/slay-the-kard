#!/usr/bin/env python3
"""区域战斗烈度（areaTimes）验证

覆盖：
- 冒烟测试：区域池、区域状态、任务面板、通关浮层与对白资源均存在且可解析
- 基本验证：areaTimes 被解析、烈度初始化为 areaTimes、归零时解锁下一区域
- 边界白盒：缺失回退默认值 3、非法值报错回退、已初始化区域不被重置、
  最后一个区域判定、通关浮层层级低于对白气泡
"""
from configparser import ConfigParser
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

POOL = ROOT / "bin" / "AreaPool.ini"
WORLD = ROOT / "bin" / "WorldMap.cs"
STATE = ROOT / "bin" / "CardRestoration.cs"
CHOOSE = ROOT / "bin" / "ChooseMission.cs"
CHOOSE_SCENE = ROOT / "bin" / "chooseMission.tscn"
BATTLE = ROOT / "bin" / "battlefield_.cs"
EVENT = ROOT / "bin" / "EventScene.cs"
VICTORY = ROOT / "bin" / "CampaignVictory.cs"
DIALOGUE = ROOT / "dialogues" / "campaign_victory.dialogue"
BALLOON = ROOT / "core_ui" / "game_dialogue_balloon.tscn"

EXPECTED_DEFAULT = 3
EXPECTED_AREAS = [f"area{i}" for i in range(1, 8)]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    required = [POOL, WORLD, STATE, CHOOSE, CHOOSE_SCENE, BATTLE, EVENT, VICTORY, DIALOGUE, BALLOON]
    missing = [p.name for p in required if not p.exists()]
    if missing:
        print(f"[FAIL] 冒烟测试：缺少文件 {missing}")
        return 1

    pool = ConfigParser(interpolation=None, strict=True)
    pool.optionxform = str
    pool.read(POOL, encoding="utf-8")

    world = WORLD.read_text(encoding="utf-8")
    state = STATE.read_text(encoding="utf-8")
    choose = CHOOSE.read_text(encoding="utf-8")
    choose_scene = CHOOSE_SCENE.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    event = EVENT.read_text(encoding="utf-8")
    victory = VICTORY.read_text(encoding="utf-8")
    dialogue = DIALOGUE.read_text(encoding="utf-8")
    balloon = BALLOON.read_text(encoding="utf-8")

    # --- 冒烟 ---
    results = [
        check(sorted(pool.sections()) == EXPECTED_AREAS, "冒烟：区域池可解析出 area1-area7"),
        check("campaign_victory" in dialogue, "冒烟：通关对白包含 campaign_victory 标题"),
        check("intensityLabel" in choose_scene, "冒烟：任务面板场景包含 intensityLabel 节点"),
    ]

    # --- areaTimes 配置 ---
    configured = {}
    for area in EXPECTED_AREAS:
        if "areaTimes" in pool[area]:
            configured[area] = pool[area]["areaTimes"].strip()
    results.append(
        check(all(v.isdigit() and int(v) > 0 for v in configured.values()),
              f"已配置的 areaTimes 均为正整数（已配置 {len(configured)} 个区域）")
    )
    results.append(
        check(len(configured) > 0, "至少有一个区域配置了 areaTimes（保证解析分支被走到）")
    )

    # --- 解析与默认值 ---
    results.append(
        check(f"DefaultAreaTimes = {EXPECTED_DEFAULT}" in world,
              f"Area 类默认烈度为 {EXPECTED_DEFAULT}")
    )
    results.append(
        check('key.Equals("areaTimes", StringComparison.OrdinalIgnoreCase)' in world,
              "LoadAreaPools 按 key 识别 areaTimes")
    )
    results.append(
        check("area.SetAreaTimes(times)" in world and "GD.PushError" in world,
              "areaTimes 合法时写入，非法时记录错误日志")
    )
    results.append(
        check("areaTimes = Math.Max(1, value)" in world, "areaTimes 下限被限制为 1")
    )
    results.append(
        check("area.AddAnEntry(val)" in world and "continue;" in world,
              "areaTimes 不会被当成可抽取任务")
    )

    # --- 烈度状态 ---
    results.append(
        check("Dictionary<string, int> _areaIntensity" in state, "存在区域剩余烈度状态字典")
    )
    results.append(
        check("public static int ReadAreaIntensity(string areaName)" in state, "提供烈度读取接口")
    )
    results.append(
        check("public static void EnsureAreaIntensity(string areaName, int areaTimes)" in state
              and "_areaIntensity.ContainsKey(areaName)) return;" in state,
              "EnsureAreaIntensity 初始化一次，不重置已进入过的区域")
    )
    results.append(
        check("public static bool ConsumeAreaIntensity(string areaName)" in state, "提供烈度消耗接口")
    )
    results.append(
        check("value--;" in state and "if (value > 0) return false;" in state and "AdvanceArea(areaName);" in state,
              "烈度归零时才调用 AdvanceArea 解锁下一区域")
    )
    results.append(
        check("public static bool IsFinalArea(string areaName)" in state
              and "Array.IndexOf(AreaOrder, areaName) == AreaOrder.Length - 1" in state,
              "IsFinalArea 以 AreaOrder 末项判定最后一个区域")
    )

    # --- 接入点 ---
    results.append(
        check("BattleStateManager.EnsureAreaIntensity(areaName, pool.ReadAreaTimes())" in world,
              "进入区域时按 areaTimes 初始化烈度")
    )
    results.append(
        check('GetNodeOrNull<Label>("intensityLabel")' in choose, "任务面板读取 intensityLabel")
    )
    results.append(
        check("BattleStateManager.ReadAreaIntensity(_areaName)" in choose, "任务面板按区域读取剩余烈度")
    )
    results.append(
        check('$"战斗烈度：{intensity}"' in choose, "任务面板文本格式为「战斗烈度：当前值」")
    )
    results.append(
        check("BattleStateManager.ConsumeAreaIntensity(clearedArea)" in battle, "战斗胜利消耗烈度")
    )
    results.append(
        check("BattleStateManager.ConsumeAreaIntensity(_areaName)" in event, "事件完成消耗烈度")
    )
    results.append(
        check("BattleStateManager.AdvanceArea(" not in battle and "BattleStateManager.AdvanceArea(" not in event,
              "两条路径不再直接调用 AdvanceArea")
    )

    # --- 通关 ---
    results.append(
        check("BattleStateManager.IsFinalArea(clearedArea)" in battle
              and "CampaignVictory.ShowAndReturnToMenu(this)" in battle,
              "战斗路径在最后一个区域归零时进入通关流程")
    )
    results.append(
        check("BattleStateManager.IsFinalArea(_areaName)" in event
              and "CampaignVictory.ShowAndReturnToMenu(parent)" in event,
              "事件路径在最后一个区域归零时进入通关流程")
    )
    results.append(
        check('"res://dialogues/campaign_victory.dialogue"' in victory
              and '"campaign_victory"' in victory,
              "通关浮层播放 campaign_victory 对白")
    )
    results.append(
        check('"res://bin/start_menu.tscn"' in victory, "通关流程结束后返回开始菜单")
    )

    balloon_layer = re.search(r"^layer = (\d+)", balloon, re.M)
    victory_marker = victory.find("OverlayLayer = ")
    victory_layer = int(re.search(r"OverlayLayer = (\d+)", victory).group(1)) if victory_marker >= 0 else -1
    results.append(
        check(balloon_layer is not None and victory_layer < int(balloon_layer.group(1)),
              f"通关浮层层级 {victory_layer} 低于对白气泡层级")
    )

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
