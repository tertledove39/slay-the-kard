#!/usr/bin/env python3
"""整局进度重置验证

覆盖：
- 冒烟测试：BattleStateManager 提供整局重置入口
- 基本验证：重置会清空卡组、恢复仅 area1 解锁、清零资源点与上局战斗统计
- 边界白盒：战败与通关两个整局结束入口都调用重置；设置菜单回主菜单不重置；
  内容缓存（卡牌/事件/区域池）不属于本局状态，不能被重置
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

STATE = ROOT / "bin" / "CardRestoration.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
VICTORY = ROOT / "bin" / "CampaignVictory.cs"
SETTINGS = ROOT / "bin" / "SettingsMenu.cs"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    required = [STATE, BATTLE, VICTORY, SETTINGS]
    missing = [p.name for p in required if not p.exists()]
    if missing:
        print(f"[FAIL] 冒烟测试：缺少文件 {missing}")
        return 1

    state = STATE.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    victory = VICTORY.read_text(encoding="utf-8")
    settings = SETTINGS.read_text(encoding="utf-8")

    marker = "public static void ResetCampaignProgress()"
    if marker not in state:
        print("[FAIL] 冒烟测试：BattleStateManager 未提供 ResetCampaignProgress")
        return 1

    reset = state[state.index(marker):]
    reset = reset[:reset.index("\n    }")]

    results = [
        check("DeckCardIds.Clear()" in reset, "重置清空持久化卡组ID"),
        check("IsDeckInitialized = false" in reset, "重置允许卡组重新从 deck.ini 加载"),
        check("Deck.Clear()" in reset, "重置释放上一局的卡牌节点引用"),
        check("UnlockedArea[area] = area == AreaOrder[0] ? 1 : 0" in reset, "重置后仅第一个区域解锁"),
        check("_areaIntensity.Clear()" in reset, "重置清空区域战斗烈度"),
        check("MaterialPoints = 0" in reset, "重置清零物资点"),
        check(
            all(f"{field} = 0" in reset for field in (
                "LastBattleLandKilled", "LastBattleAirKilled", "LastBattleFriendlyDead",
                "LastBattleHqDefenceLost", "LastBattlePointsGained")),
            "重置清零上局战斗统计",
        ),
        check("StoreCardQueue.Clear()" in reset and "StoreCurrentSlots = null" in reset, "重置清空商店库存与槽位"),
        check("IsCampaignMode = false" in reset and "battlefield = null" in reset, "重置清除战役标记与上一局战场引用"),
    ]

    # 内容缓存属于配置，不能被本局重置清掉
    results.append(
        check(
            not re.search(r"(_allCards|_allEvents|_areaPools)\s*=\s*null", reset),
            "重置不影响 card.ini / event.ini / AreaPool.ini 的内容缓存",
        )
    )
    results.append(
        check("ResetCampaignProgress" not in settings, "设置菜单返回主菜单不触发重置"),
    )

    defeat = battle[battle.index("private async Task ReturnToStartMenuAfterDefeat()"):]
    defeat = defeat[:defeat.index("SceneLoader.ChangeSceneAsync")]
    results.append(
        check("BattleStateManager.ResetCampaignProgress()" in defeat, "战败回主菜单时重置本局进度"),
    )

    results.append(
        check("BattleStateManager.ResetCampaignProgress()" in victory and "StartMenuPath" in victory,
              "战役通关回主菜单时重置本局进度"),
    )

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
