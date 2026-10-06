#!/usr/bin/env python3
"""存档 / 读档：暂停菜单（战斗 + 世界地图）、Continue 读档、Start 覆盖确认。

需求：
  · 战斗界面按 ESC 或点左上角设置 → 菜单：A 调音量 B 认输 C 保存并退出
  · 世界地图同样位置也有设置按钮 → 菜单：A 调音量 B 放弃 C 保存并退出
  · 「保存并退出」存下当前进度（含战斗 id，如 berlin）
  · Continue 有档就直接进对应的战斗 / 世界地图
  · Start 时若已有档，先问是否覆盖；是 → 删档，否 → 退回主菜单

几条**结构性**的约定，测试守的就是它们：
  · 存档只写一处（`SaveManager`），两个界面都调它，不各写一份序列化
  · 「认输」走**既有**的失败链路（总部防御归零 → 死亡判定 → `RemoveCard(myHq)`），
    不另写一套失败流程——否则以后改血量规则会漏掉认输这条路
  · 二次确认只有一份实现（`UiConfirm`），音量滑条行只有一份（`SettingRow`）
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
SAVE = ROOT / "bin" / "SaveManager.cs"
PAUSE = ROOT / "bin" / "PauseMenu.cs"
PAUSE_SCENE = ROOT / "bin" / "pause_menu.tscn"
CONFIRM = ROOT / "bin" / "UiConfirm.cs"
ROW = ROOT / "bin" / "SettingRow.cs"
STATE = ROOT / "bin" / "CardRestoration.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
WORLDMAP = ROOT / "bin" / "WorldMap.cs"
START = ROOT / "bin" / "StartMenu.cs"
SETTINGS_MENU = ROOT / "bin" / "SettingsMenu.cs"
BATTLE_SCENE = ROOT / "bin" / "battleField.tscn"
WORLDMAP_SCENE = ROOT / "bin" / "worldMap.tscn"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    results = []

    # ==================== ① 冒烟 ====================
    print("--- ① 冒烟：文件都在 ---")
    for path in (SAVE, PAUSE, PAUSE_SCENE, CONFIRM, ROW):
        results.append(check(path.exists(), f"{path.relative_to(ROOT)} 存在"))

    save = SAVE.read_text(encoding="utf-8")
    pause = PAUSE.read_text(encoding="utf-8")
    pause_scene = PAUSE_SCENE.read_text(encoding="utf-8")
    confirm = CONFIRM.read_text(encoding="utf-8")
    row = ROW.read_text(encoding="utf-8")
    state = STATE.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    worldmap = WORLDMAP.read_text(encoding="utf-8")
    start = START.read_text(encoding="utf-8")
    settings_menu = SETTINGS_MENU.read_text(encoding="utf-8")
    battle_scene = BATTLE_SCENE.read_text(encoding="utf-8")
    worldmap_scene = WORLDMAP_SCENE.read_text(encoding="utf-8")

    # ==================== ② 存档内容 ====================
    print("\n--- ② 存档存了什么 ---")
    results.append(check('private const string SavePath = "user://save.cfg";' in save,
                         "存档落在 user://save.cfg（与 settings.cfg 同一套路）"))
    results.append(check("public static bool HasSave()" in save
                         and "public static void Save()" in save
                         and "public static bool Load()" in save
                         and "public static void Delete()" in save,
                         "Save / Load / Delete / HasSave 四个入口齐全"))

    # 每一个要存的字段都得真的写进去（少一个就是「读档后进度对不上」）
    for field, note in [
        ('"enemy"', "战斗 id（如 berlin）"),
        ('"area"', "所在区域"),
        ('"inBattle"', "存档时在不在战斗中 —— 决定读档进哪个场景"),
        ('"hp"', "血量"),
        ('"points"', "物资点"),
        ('"deck"', "卡组"),
        ('"queue"', "商店库存队列"),
        ('"slotCount"', "商店货架数"),
    ]:
        results.append(check(field in save, f"存了 {note}（{field}）"))
    results.append(check("foreach (var pair in BattleStateManager.UnlockedArea)" in save,
                         "存了全部 7 个区域的解锁状态"))
    results.append(check("BattleStateManager.ReadAllAreaIntensity()" in save,
                         "存了各区域的剩余烈度"))
    results.append(check("BattleStateManager.RestoreAreaIntensity(intensity)" in save,
                         "读档时把烈度整表写回"))

    # 版本不匹配要拒绝，而不是拿半截数据把状态弄坏
    results.append(check("if (version != Version)" in save and "return false;" in save,
                         "存档版本对不上就忽略（宁可重开一局，也不要用脏数据）"))
    results.append(check("if (pair.Value > 0)" in state,
                         "烈度只收 >0 的项（0 与「没进过」在 ReadAreaIntensity 里都是「没有」）"))

    # ==================== ③ 读档回哪个场景 ====================
    print("\n--- ③ 读档后进哪个场景 ---")
    results.append(check('BattleStateManager.IsCampaignMode ? "res://bin/battleField.tscn" : "res://bin/worldMap.tscn"'
                         in save,
                         "战斗中存的 → 回那一场战斗；世界地图存的 → 回世界地图"))
    results.append(check("SaveManager.ResolveScenePath()" in start,
                         "主菜单「继续」按这条判定决定进哪个场景"))

    # ==================== ④ 暂停菜单 ====================
    print("\n--- ④ 暂停菜单 ---")
    results.append(check("public static PauseMenu Show(Node host, PauseAction action1, PauseAction action2" in pause,
                         "两个动作**由调用方传入**（一个菜单服务战斗与世界地图）"))
    results.append(check("if (IsOpen) return null;" in pause, "已经开着就不会叠第二个"))
    results.append(check("SettingRow.AddFloatSlider(list, item)" in pause
                         and "SettingRow.AddFloatSlider(list, item)" in settings_menu,
                         "音量滑条与设置界面**共用** SettingRow（不是第二份滑块实现）"))
    results.append(check("UiConfirm.AskAsync(this, text)" in pause,
                         "不可逆动作走共用的二次确认"))
    results.append(check("_confirming = true;" in pause,
                         "确认框开着时菜单的 ESC 让位（那时 ESC 是「取消」）"))
    for node in ('name="Panel"', 'name="VolumeRows"', 'name="Actions"'):
        results.append(check(node in pause_scene, f"pause_menu.tscn 里有 {node}"))
    # 场景层级与代码里的取节点路径必须一致 —— 写错了是运行时才炸的空引用
    for path in ("Panel/Margin/Rows/Actions", "Panel/Margin/Rows/VolumeRows"):
        last = path.split("/")[-1]
        results.append(check(f'GetNode<VBoxContainer>("{path}")' in pause,
                             f"代码按 {path} 取节点"))
        results.append(check(f'name="{last}"' in pause_scene and path.count("/") == 3,
                             f"场景里 {last} 就嵌在三层之下（层数对上）"))

    # ==================== ⑤ 两个界面各自的动作 ====================
    print("\n--- ⑤ 战斗 / 世界地图各传什么动作 ---")
    results.append(check('Text = "认输"' in battle, "战斗：动作一是「认输」"))
    results.append(check('Text = "保存并退出"' in battle, "战斗：动作二是「保存并退出」"))
    results.append(check('Text = "放弃"' in worldmap, "世界地图：动作一是「放弃」"))
    results.append(check('Text = "保存并退出"' in worldmap, "世界地图：动作二是「保存并退出」"))

    # 认输必须走既有失败链路
    surrender = battle[battle.index("private async Task SurrenderAsync()"):]
    surrender = surrender[:surrender.index("\n    }")]
    results.append(check("await myHq.LoseDefence(myHq.ReadDefence());" in surrender,
                         "认输 = 玩家总部防御归零（不是另写一套失败）"))
    results.append(check("await CheckIfAnyUnitDiedAsync();" in surrender,
                         "然后走既有的死亡判定 → RemoveCard(myHq) → 扣血/回图/游戏结束"))
    results.append(check("SaveManager.Save();" in battle and "SaveManager.Save();" in worldmap,
                         "两个界面的「保存并退出」都调同一个 SaveManager.Save()"))

    # 放弃要连进度一起清
    abandon = worldmap[worldmap.index("private async Task AbandonAsync()"):]
    abandon = abandon[:abandon.index("\n    }")]
    results.append(check("SaveManager.Delete();" in abandon
                         and "BattleStateManager.ResetCampaignProgress();" in abandon,
                         "放弃 = 删档 + 重置整局进度（不然「开始」进来会是脏状态）"))

    # ==================== ⑥ 开关方式：ESC 与左上角按钮 ====================
    print("\n--- ⑥ ESC 与左上角设置按钮 ---")
    for name, text in (("战斗", battle), ("世界地图", worldmap)):
        results.append(check("escEvent.Keycode == Key.Escape && !PauseMenu.IsOpen" in text,
                             f"{name}：ESC 只在菜单没开时响应（不会一按就开了又关）"))
        results.append(check('GetNodeOrNull<Button>("SettingsButton")' in text,
                             f"{name}：左上角设置按钮接的是同一个入口"))
    for scene, name in ((battle_scene, "battleField"), (worldmap_scene, "worldMap")):
        results.append(check('[node name="SettingsButton" type="Button"' in scene,
                             f"{name}.tscn 里有左侧设置按钮节点（UI 写在场景里，不写死在代码里）"))

    # ==================== ⑦ 主菜单 ====================
    print("\n--- ⑦ 主菜单的 继续 / 开始 ---")
    results.append(check("if (!SaveManager.Load())" in start, "「继续」先读档"))
    results.append(check("RefreshContinueButton();" in start
                         and "button.Disabled = !SaveManager.HasSave();" in start,
                         "没有可读的档就把「继续」灰掉（比点下去没反应清楚）"))
    results.append(check("SaveManager.HasSave()" in start and "SaveManager.Delete();" in start
                         and "UiConfirm.AskAsync(this," in start,
                         "「开始」时若已有档先问是否覆盖，答是才删档"))
    results.append(check("if (!overwrite) return;" in start,
                         "答否就不进新局（留在主菜单）"))

    # ==================== ⑧ 「只写一份」的三处 ====================
    print("\n--- ⑧ 不许有第二份实现 ---")
    # 只数 `new ...` —— 注释里也会提到这两个名字（本文件的设计说明就提了）
    results.append(check(confirm.count("new TaskCompletionSource") == 1
                         and confirm.count("new ConfirmationDialog") == 1,
                         "二次确认只有 UiConfirm 一份"))
    results.append(check(all("new TaskCompletionSource" not in text
                             and "new ConfirmationDialog" not in text
                             for text in (pause, start, battle, worldmap)),
                         "PauseMenu / StartMenu / 战斗 / 世界地图 里都没有第二份确认框"))
    results.append(check("new HSlider" not in settings_menu,
                         "SettingsMenu 里没有第二份滑条构建（已收进 SettingRow）"))
    results.append(check(row.count("new HSlider") == 1, "SettingRow 是滑条行的唯一实现"))

    # 场景里声明的每个动作按钮文案都得真被传到菜单里
    for text, where in (("认输", battle), ("放弃", worldmap), ("保存并退出", battle), ("保存并退出", worldmap)):
        results.append(check(f'Text = "{text}"' in where, f"「{text}」真的传给了 PauseMenu（{where is battle and '战斗' or '世界地图'}）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
