#!/usr/bin/env python3
"""按钮在**悬停**与**按下**时各自该做什么。

悬停：各场景自己的一份 `AnimateButton`（1.08x / 0.12s 放大）。
按下：**按键音**，统一走 `bin/UiClickSound.cs`（槽位 `[sfx] button` = `General_button2.wav`）。
      目前覆盖：主菜单、任务选择面板、世界地图、商店、卡组查看器、战斗的「下一回合」与「卡组」。

命名提醒：`bin/UiClickSound.cs` 的 `AttachAll` 是**递归**挂的，所以「这个界面有按钮却
没挂上」是能静态查出来的——见下面那张 场景 与 挂载点 的对照表，加了新界面忘了挂会直接红。
"""
from pathlib import Path
import re
import sys

if hasattr(sys.stdout, "reconfigure"):
    # 控制台是 GBK，中文/箭头字符直接 print 会 UnicodeEncodeError
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def main():
    worldmap_cs = (ROOT / "bin" / "WorldMap.cs").read_text(encoding="utf-8")
    worldmap_tscn = (ROOT / "bin" / "worldMap.tscn").read_text(encoding="utf-8")
    choose = (ROOT / "bin" / "ChooseMission.cs").read_text(encoding="utf-8")
    store = (ROOT / "Store.cs").read_text(encoding="utf-8")
    event_scene = (ROOT / "bin" / "EventScene.cs").read_text(encoding="utf-8")
    reward = (ROOT / "bin" / "PostBattleReward.cs").read_text(encoding="utf-8")
    display = (ROOT / "DisplayCard.cs").read_text(encoding="utf-8")
    ui_click = (ROOT / "bin" / "UiClickSound.cs").read_text(encoding="utf-8")
    music = (ROOT / "core_logic" / "MusicManager.cs").read_text(encoding="utf-8")
    sfx_player = (ROOT / "core_logic" / "SfxPlayer.cs").read_text(encoding="utf-8")
    music_ini = (ROOT / "configs" / "music.ini").read_text(encoding="utf-8")
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")

    results = [
        check('public void _on_deck_pressed()' in worldmap_cs and 'BuildDisplayDeck' in worldmap_cs, 'deck button callback uses the shared deck viewer'),
        check('var viewDeckBtn = new Button();' not in worldmap_cs, 'world map no longer creates a dynamic deck button in code'),
        check('[node name="deck" type="Button" parent="."]' in worldmap_tscn, 'worldMap.tscn contains the deck button node'),
        check('[connection signal="pressed" from="deck" to="." method="_on_deck_pressed"]' in worldmap_tscn, 'worldMap.tscn connects the deck button to _on_deck_pressed'),
        check('ConnectHover("store")' in worldmap_cs and 'ConnectHover("deck")' in worldmap_cs, 'world map store and deck buttons get hover effects'),
        check('ConnectHover(_btn1);' in choose and 'ConnectHover(_btn2);' in choose and 'ConnectHover(_btn3);' in choose, 'ChooseMission buttons get hover effects'),
        check('refreshBtn.MouseEntered' in store and 'backBtn.MouseEntered' in store, 'store refresh and back buttons get hover effects'),
        check('btn.MouseEntered += () => AnimateButton(btn, HoverScale);' in event_scene, 'event choice buttons get hover effects'),
        check('btn.MouseEntered += () => AnimateButton(btn, HoverScale);' in reward and 'skipBtn.MouseEntered += () => AnimateButton(skipBtn, HoverScale);' in reward, 'post-battle reward buttons get hover effects'),
        check('button.MouseEntered += () => AnimateButton(button, HoverScale);' in display, 'display deck back button gets a hover effect'),
        check(all(text in file for text, file in [
            ('private const float HoverScale = 1.08f;', worldmap_cs),
            ('private const float HoverDuration = 0.12f;', worldmap_cs),
            ('private const float HoverScale = 1.08f;', choose),
            ('private const float HoverDuration = 0.12f;', choose),
            ('private const float HoverScale = 1.08f;', store),
            ('private const float HoverDuration = 0.12f;', store),
            ('private const float HoverScale = 1.08f;', reward),
            ('private const float HoverDuration = 0.12f;', reward),
            ('private const float HoverScale = 1.08f;', display),
            ('private const float HoverDuration = 0.12f;', display),
        ]), 'hover effects reuse the standard 1.08x / 0.12s parameters')
        ,check('PivotOffset = button.Size / 2f' in worldmap_cs and
               'PivotOffset = control.Size / 2f' in choose and
               'PivotOffset = control.Size / 2f' in store and
               'PivotOffset = button.Size / 2f' in event_scene and
               'PivotOffset = button.Size / 2f' in reward and
               'PivotOffset = button.Size / 2f' in display,
               'button hover scaling uses the control center as pivot'),

        # ==================== 按键音 ====================
        # 主人：世界地图界面所有按钮 + 「下一回合」按钮都用 General_button2。
        # 「哪些按钮要响」是 UI 的事，音频细节留给 MusicManager —— 分工写在这一个类里。
        check('public static void Play() => MusicManager.Instance?.PlaySfx(ClickSlot);' in ui_click,
              'UiClickSound 把播放委托给 MusicManager.PlaySfx（自己不碰音频节点）'),
        check('UiClickSound.AttachAll(this);' in worldmap_cs,
              '世界地图递归挂载**所有**按钮（不是逐个写节点名）'),
        check('UiClickSound.Attach(buttonNextTurn);' in battle,
              '战斗界面的「下一回合」按钮也挂了按键音'),
        check('FindChildren("*", "BaseButton", recursive: true, owned: false)' in ui_click,
              '递归找的是 BaseButton —— Button 与 TextureButton 都覆盖（区域按钮正是 TextureButton）'),
        check('button.HasMeta(AttachedFlag)' in ui_click and 'SetMeta(AttachedFlag, true)' in ui_click,
              '带防重复挂载的记号（同一个按钮被挂两次就是两声）'),

        # 播放侧：声部必须挂在 **autoload** 上。按键音最典型的用法就是
        # 「按下去 → 立刻切场景」（点区域按钮就进战斗），挂场景里会被 QueueFree 掐断。
        check('public void PlaySfx(string slot) => sfxPlayer?.Play(slot);' in music,
              'MusicManager 提供「放一次就完」的入口（自己只管转发）'),
        check('sfxPlayer = new SfxPlayer(this, PickSfx);' in music,
              '声部池挂在 MusicManager 自己身上（autoload 常驻，切场景不会掐断）'),
        check('voices ??= CreateVoices();' in sfx_player and 'host.AddChild(voice);' in sfx_player,
              '声部懒建在宿主节点下（没人按按钮就不花这份钱）'),
        check('nextVoice = (nextVoice + 1) % voices.Length;' in sfx_player,
              '多个声部轮换，连点不会互相掐'),
        check('new AudioStreamPlayer { Bus = Bus }' in sfx_player and 'Bus = "SFX"' in sfx_player,
              '走 SFX 总线（设置界面调的那条）'),
        # 拆分的动机是 300 行红线（规范 B），所以把这条也钉住 —— 否则下次一加功能又涨回去。
        check(len(music.split("\n")) < 300,
              f'MusicManager.cs 仍在 300 行红线内（当前 {len(music.splitlines())} 行）'),
    ]

    # ---- 每个「有按钮的界面」都要挂上，一个都不能漏 ----
    # 这张表就是「哪些界面该响」的唯一清单：加了新界面忘了挂会红，
    # 而不是靠人去数。`Attach` 那一列是**运行时才建的按钮**（`_Ready` 之后才存在，
    # 递归扫不到），必须在创建处单独挂。
    print("\n--- 各界面 与 挂载点 的对照表 ---")
    hookups = [
        ("bin/start_menu.tscn", "bin/StartMenu.cs", "AttachAll(this)", "主菜单"),
        ("bin/chooseMission.tscn", "bin/ChooseMission.cs", "AttachAll(this)", "任务选择面板"),
        ("bin/worldMap.tscn", "bin/WorldMap.cs", "AttachAll(this)", "世界地图"),
        ("store.tscn", "Store.cs", "AttachAll(this)", "商店"),
        ("bin/display_card.tscn", "DisplayCard.cs", "AttachAll(this)", "卡组查看器"),
    ]
    for scene_rel, cs_rel, call, label in hookups:
        scene_text = (ROOT / scene_rel).read_text(encoding="utf-8")
        cs_text = (ROOT / cs_rel).read_text(encoding="utf-8")
        buttons = len(re.findall(r'type="(?:Button|TextureButton)"', scene_text))
        results.append(check(f"UiClickSound.{call};" in cs_text,
                             f"{label}（{scene_rel}）挂上了按键音"))
        results.append(check(buttons > 0,
                             f"{label} 场景里确实有按钮（{buttons} 个）—— 否则这条断言是空跑"))

    # 战斗里两个按钮是 `_Ready` 之后才建的（下一回合在场景里、卡组是代码 new 的），
    # 所以用 Attach 而不是 AttachAll。
    results.append(check("UiClickSound.Attach(buttonNextTurn);" in battle,
                         "战斗的「下一回合」按钮（场景里的节点）"))
    results.append(check("UiClickSound.Attach(viewDeckBtn);" in battle,
                         "战斗的「卡组」按钮（代码里 new 的，递归扫不到，必须在创建处挂）"))
    # 商店的「点卡片购买」不是 Button 而是 _Input + 矩形命中，单独补一声。
    results.append(check("UiClickSound.Play();" in store
                         and store.index("UiClickSound.Play();") < store.index("TryBuyCard(i);"),
                         "商店点卡片买卡时也响一声（放在命中判定之后、买卡之前）"))

    # ---- 事件叠层必须挂在**世界地图**上 ----
    # 曾经的 bug（BUGS.md #58）：叠层挂在任务选择面板上，而下一句 EnterEventOverlay
    # 就把那个面板 QueueFree 了 —— 叠层跟着陪葬，事件界面从不出现，而且
    # `_eventOverlayActive` 永远停在 true，地图从此点不动。
    print("\n--- 事件叠层挂在哪 ---")
    results.append(check("var host = map ?? parent;" in event_scene and "host.AddChild(scene);" in event_scene,
                         "叠层挂到世界地图（拿不到才退回 parent），不挂在会被关掉的任务面板上"))
    results.append(check("parent.AddChild(scene);" not in event_scene,
                         "旧的「直接挂 parent」写法已消失"))
    results.append(check("await CampaignVictory.ShowAndReturnToMenu(host);" in event_scene,
                         "通关界面也传存活的 host（传 parent 会被存活判定挡掉、整段跳过）"))
    show = event_scene[event_scene.index("public static async Task Show("):]
    show = show[:show.index("\n    }")]
    results.append(check(show.index("var host = map ?? parent;") < show.index("map?.EnterEventOverlay();"),
                         "顺序：先挂好叠层，再收起任务面板"))

    # 交叉核对：UiClickSound 里写的槽位名必须在 [sfx] 段里存在，且文件真在、真被 Godot 导入。
    # 少了任何一环都是**静默没声**——测试不查就只能靠耳朵发现。
    slot = re.search(r'ClickSlot = "(.+?)";', ui_click)
    results.append(check(slot is not None, '槽位名是常量（不在调用处裸写）'))
    if slot:
        name = slot.group(1)
        entry = re.search(rf"(?m)^{re.escape(name)}\s*=\s*(.+)$", music_ini)
        results.append(check(entry is not None, f'[sfx] 段配了槽位「{name}」'))
        if entry:
            for rel in entry.group(1).split(","):
                src = ROOT / rel.strip().replace("res://", "")
                results.append(check(src.exists(), f'{name} → {src.name} 存在'))
                results.append(check(Path(str(src) + ".import").exists(),
                                     f'{name} 的 {src.name} 已被 Godot 导入'))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
