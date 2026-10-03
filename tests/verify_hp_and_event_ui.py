#!/usr/bin/env python3
"""血量系统 + 事件界面的三项改动 + 返回按钮 + 商店买血。

本批需求有 7 项，全都是「写错了也不报错、只是行为不对」的类型，故逐项静态校验：

  1. 事件选项资源点不足 → 不可选
  2. 事件选项会加卡 → 悬浮预览（同名去重计数）
  3. 血量系统：开局 5、失败扣血不再立刻结束、area7 失败清零、事件 hp(n)、worldMap 显示
  4. UI 写进场景（不写在代码里）
  5. 任务面板的「返回」按钮 + 返回后重进不重抽
  6. 商店 200 资源点买 1 点血
  7. 用 assest/hearts.png

界面相关的断言一律**读 .tscn**，这本身就是对需求 4 的校验：如果哪天有人把 UI 挪回代码里，
下面「场景里有这个节点」的断言就会红。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]


def read(*parts):
    return (ROOT.joinpath(*parts)).read_text(encoding="utf-8")


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    state = read("bin", "CardRestoration.cs")
    battle = read("bin", "battlefield_.cs")
    runner = read("bin", "EventEffectRunner.cs")
    event_scene = read("bin", "EventScene.cs")
    preview_cs = read("bin", "EventCardPreview.cs")
    preview_tscn = read("bin", "event_card_preview.tscn")
    world_cs = read("bin", "WorldMap.cs")
    world_tscn = read("bin", "worldMap.tscn")
    mission_cs = read("bin", "ChooseMission.cs")
    mission_tscn = read("bin", "chooseMission.tscn")
    store_tscn = read("store.tscn")
    hp_shop = read("StoreHpShop.cs")
    end_cs = read("End.cs")
    panel_tscn = read("bin", "settlement_panel.tscn")

    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    for f in ["bin/CardRestoration.cs", "bin/EventEffectRunner.cs", "bin/EventCardPreview.cs",
              "bin/event_card_preview.tscn", "StoreHpShop.cs"]:
        results.append(check((ROOT / f).exists(), f"{f} 存在"))
    results.append(check((ROOT / "assest" / "hearts.png").exists(), "assest/hearts.png 存在"))

    # --------------------------- 1) 资源点不足不可选 ---------------------------
    print("\n--- 1) 事件选项资源点不足则不可选 ---")
    results.append(check("public static int ParseMaterialCost(string effect)" in runner,
                         "EventEffectRunner 暴露 ParseMaterialCost（只累加花费）"))
    results.append(check("if (v < 0) cost += -v;" in runner,
                         "只把负数算作花费（带收益的选项不因此放宽）"))
    results.append(check("btn.Disabled = cost > BattleStateManager.MaterialPoints;" in event_scene,
                         "选项按钮按余额置 Disabled"))
    results.append(check("需要 {cost} 资源点" in event_scene, "不可选时给出原因文案"))
    results.append(check("Math.Max(0, BattleStateManager.MaterialPoints + amount)" in runner,
                         "执行侧仍有下限 0 的兜底（界面已拦，这里是第二道）"))

    # --------------------------- 2) 悬浮预览 ---------------------------
    print("\n--- 2) 会加卡的选项：悬浮预览（同名去重计数） ---")
    results.append(check("public static List<(CardData card, int count)> ParseAddedCards(string effect)" in runner,
                         "EventEffectRunner 暴露 ParseAddedCards"))
    results.append(check('s.StartsWith("replaceCard(")' in runner and 's.StartsWith("replaceRandomCard(")' in runner,
                         "两类替换效果都计入（需求方确认）"))
    results.append(check("counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;" in runner,
                         "按 id 计数而非逐张罗列——[snowstorm] 有 20 张同名卡"))
    results.append(check("card.MouseFilter = MouseFilterEnum.Ignore;" in preview_cs,
                         "预览卡不吃鼠标，否则会把按钮的 hover 抢掉"))
    results.append(check("public void Show(List<(CardData card, int count)> cards, Control anchor)" in preview_cs
                         and "private Vector2 ResolvePosition(Control anchor)" in preview_cs,
                         "预览按按钮位置定位"))
    results.append(check('_cardPreview = ResourceLoader.Load<PackedScene>(PreviewScenePath)?.Instantiate()' in event_scene,
                         "EventScene 只实例化场景，不自建 UI"))

    # --------------------------- 4) UI 在场景里（需求 4） ---------------------------
    print("\n--- 4) 新增 UI 一律在 .tscn 里（需求 4） ---")
    results.append(check('script = ExtResource("1_script")' in preview_tscn and "StyleBoxFlat" in preview_tscn,
                         "预览面板的布局与配色在 event_card_preview.tscn"))
    results.append(check('[node name="heartPic" type="Sprite2D"' in world_tscn
                         and '[node name="hpNum" type="Label"' in world_tscn,
                         "worldMap.tscn 里有 heartPic / hpNum 节点"))
    results.append(check('[node name="BackButton" type="Button"' in mission_tscn,
                         "chooseMission.tscn 里有 BackButton 节点"))
    results.append(check('[node name="BuyHp" type="Button"' in store_tscn,
                         "store.tscn 里有 BuyHp 节点"))
    results.append(check('[node name="Retreat" type="Control"' in panel_tscn
                         and '[node name="HpInfo" type="Label" parent="Retreat"' in panel_tscn,
                         "settlement_panel.tscn 里有可撤退的 Retreat 面板"))
    results.append(check("Res://" not in preview_cs and "new Panel(" not in preview_cs
                         and "new Label(" not in preview_cs and "new HBoxContainer(" not in preview_cs,
                         "EventCardPreview 里没有代码建 UI（只取场景里的节点）"))

    # --------------------------- 3) 血量系统 ---------------------------
    print("\n--- 3) 血量系统 ---")
    results.append(check("public const int InitialHp = 5;" in state, "开局血量 5"))
    results.append(check("public const int HpPrice = 200;" in state, "买血价格 200 资源点"))
    results.append(check("Hp = InitialHp;" in state, "重置整局进度时血量回到 5"))
    results.append(check("public static int AddHp(int amount)" in state and "Math.Max(0, Hp + amount)" in state,
                         "AddHp 下限 0、无上限"))
    results.append(check("public static bool IsBossBattle()" in state and "pool.ReadBoss()" in state,
                         "boss 战判定用「抽中的关卡 == 该区域配置的 boss」"))
    lose = re.search(r"public static int LoseHpOnBattleDefeat\(\)(.*?)\n    \}", state, re.S)
    lose_body = lose.group(1) if lose else ""
    results.append(check("IsFinalArea(SelectedArea)" in lose_body and "Hp = 0;" in lose_body,
                         "area7（终局区域）失败 → 血量清零"))
    results.append(check("IsBossBattle() ? -2 : -1" in lose_body, "boss 战 -2、其余 -1"))
    # 失败分支
    results.append(check("if (hpLeft > 0) _ = ReturnToWorldMapAfterDefeat(hpBefore - hpLeft, hpLeft);" in battle,
                         "血还有剩 → 回世界地图继续"))
    results.append(check("else _ = ReturnToStartMenuAfterDefeat();" in battle, "血归零 → 走游戏结束"))
    results.append(check("private async Task ReturnToWorldMapAfterDefeat(int hpLost, int hpLeft)" in battle,
                         "新增 ReturnToWorldMapAfterDefeat"))
    results.append(check("await endNode.ShowRetreat(hpLost, hpLeft);" in battle, "战败显示撤退面板"))
    defeat = re.search(r"private async Task ReturnToWorldMapAfterDefeat\(int hpLost, int hpLeft\)(.*?)\n    \}",
                       battle, re.S)
    defeat_body = defeat.group(1) if defeat else ""
    results.append(check("ConsumeAreaIntensity" in defeat_body,
                         "战败同样消耗 1 点烈度（需求方确认）"))
    results.append(check("PostBattleReward" not in defeat_body,
                         "战败不发战后奖励（只有战胜才发）"))
    results.append(check("public async Task ShowRetreat(int hpLost, int hpLeft)" in end_cs,
                         "End.cs 有 ShowRetreat"))
    # worldMap 显示
    results.append(check('path="res://assest/hearts.png"' in world_tscn,
                         "worldMap 用 hearts.png 作血量图标（需求 7）"))
    results.append(check("private void RefreshHp()" in world_cs and "RefreshHp();" in world_cs,
                         "WorldMap 刷新血量数字"))
    results.append(check("hpNum.Text = BattleStateManager.Hp.ToString();" in world_cs,
                         "血量用图标 + 数字显示（无上限，数字承载数量）"))
    # 事件 key
    results.append(check('s.StartsWith("hp(") && s.EndsWith(")")' in runner,
                         "事件支持 hp(n) 键"))
    results.append(check("BattleStateManager.AddHp(s[\"hp(\".Length..^1].ToInt());" in runner,
                         "hp(n) 正负都走同一个键（负数即扣血）"))

    # --------------------------- 5) 返回按钮 ---------------------------
    print("\n--- 5) 任务面板的返回按钮 ---")
    results.append(check("public event Action BackRequested;" in mission_cs,
                         "ChooseMission 只抛事件，不管外面怎么关"))
    results.append(check("_backButton.Pressed += () => BackRequested?.Invoke();" in mission_cs,
                         "返回按钮接上事件"))
    results.append(check("_chooseMissionPanel.BackRequested += CloseMissionPanel;" in world_cs,
                         "WorldMap 接住返回请求"))
    results.append(check("private void CloseMissionPanel()" in world_cs,
                         "CloseMissionPanel 只关面板、**保留**抽到的任务"))
    dismiss = re.search(r"public void DismissChooseMission\(\)(.*?)\n    \}", world_cs, re.S)
    dismiss_body = dismiss.group(1) if dismiss else ""
    results.append(check("_drawnIds = null;" in dismiss_body,
                         "事件完成后丢弃这一批（事件已消耗烈度，下次必须重抽）"))
    close = re.search(r"private void CloseMissionPanel\(\)(.*?)\n    \}", world_cs, re.S)
    close_body = close.group(1) if close else ""
    results.append(check("_drawnIds = null;" not in close_body,
                         "返回时**不**清空——否则「记住了」这条需求失效"))
    results.append(check("if (_drawnArea == areaName && _drawnIds != null)" in world_cs,
                         "同一区域再次进入时复用上次抽到的一批"))
    results.append(check("_drawnArea = areaName;" in world_cs and "private List<string> _drawnIds;" in world_cs,
                         "抽到的批次记在实例字段（换场景自然失效，无需清理）"))

    # --------------------------- 6) 商店买血 ---------------------------
    print("\n--- 6) 商店买血 ---")
    results.append(check("BattleStateManager.MaterialPoints < BattleStateManager.HpPrice" in hp_shop,
                         "资源点不足时不扣减"))
    results.append(check("BattleStateManager.MaterialPoints -= BattleStateManager.HpPrice;" in hp_shop
                         and "BattleStateManager.AddHp(1);" in hp_shop,
                         "扣 200 资源点、加 1 点血"))
    results.append(check("SetupHpShop();" in read("Store.cs"),
                         "Store._Ready 调用 SetupHpShop（拆出去后仍接得上）"))
    results.append(check('GetNodeOrNull<Button>("BuyHp")' in hp_shop, "取的是场景里的 BuyHp 节点"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
