#!/usr/bin/env python3
"""全局背景音乐管理器验证：多曲目槽位随机播放、战斗专属BGM与回退。"""
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition

def info(message):
    print(f"[INFO] {message}")

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def main():
    project = read("project.godot")
    music = read("core_logic/MusicManager.cs")
    config = read("configs/music.ini")
    start = read("bin/StartMenu.cs")
    world = read("bin/WorldMap.cs")
    battle = read("bin/battlefield_.cs")
    state = read("bin/CardRestoration.cs")

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    smoke = [
        check('MusicManager="*res://core_logic/MusicManager.cs"' in project, "MusicManager 已注册为 autoload"),
        check('public static MusicManager Instance' in music, "MusicManager 暴露全局实例"),
        check('public void PlaySlot(string slot)' in music, "MusicManager 暴露 PlaySlot"),
        check('public void StopMusic()' in music, "MusicManager 暴露 StopMusic"),
        check('public void SetVolumeDb(float db)' in music, "MusicManager 暴露 SetVolumeDb"),
        check('[music]' in config, "music.ini 存在 [music] 段"),
        check('start_menu=' in config and 'world_map=' in config and 'battle=' in config,
              "music.ini 含预期的三个基础槽位"),
    ]

    # ------------------------------ 基本验证 ------------------------------
    print("\n--- 基本验证：一个槽位多首曲目 ---")
    basic = [
        check('Dictionary<string, string[]> slotPaths' in music,
              "槽位值以曲目数组存储，一个槽位可容纳多首"),
        check("char[] PathSeparator = { ',' };" in music, "以英文逗号作为曲目分隔符"),
        check('raw.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries)' in music,
              "拆分时忽略空项，容忍尾随逗号"),
        check('if (!string.IsNullOrEmpty(trimmed)) result.Add(trimmed);' in music,
              "拆分后逐项 Trim，容忍逗号两侧空格"),
        check('private string PickPath(string[] paths)' in music, "提供曲目随机抽取函数"),
        check('private readonly Random rnd = new();' in music, "随机源为常驻实例，不在每次播放时重建"),
    ]

    print("\n--- 基本验证：多曲目随机规则 ---")
    pick_body = music.split('private string PickPath(string[] paths)')[1].split('public void StopMusic')[0]
    basic += [
        check('if (paths.Length == 1) return paths[0];' in pick_body, "单曲槽位不做随机，直接播放"),
        check('rnd.Next(paths.Length)' in pick_body, "多曲槽位在全部曲目中随机取一首"),
        check('if (paths[index] == currentPath)' in pick_body, "抽中正在播放的那首时触发避让"),
        check('(index + 1 + rnd.Next(paths.Length - 1)) % paths.Length' in pick_body,
              "避让使用非零随机偏移，保证换到确实不同的一首"),
    ]

    print("\n--- 基本验证：战斗专属BGM ---")
    basic += [
        check('public const string BattleBgmPrefix = "battleBGM_";' in music,
              "定义 battleBGM_ 槽位前缀常量"),
        check('public const string BattleSlot = "battle";' in music, "定义通用战斗槽位常量"),
        check('public bool HasSlot(string slot)' in music, "提供 HasSlot 供回退判断"),
        check('public void PlayBattleSlot(string enemyPreset)' in music, "提供 PlayBattleSlot 入口"),
        check('PlaySlot(HasSlot(specific) ? specific : BattleSlot);' in music,
              "专属槽位未配置时回退到通用 battle 槽位"),
        check('BattleBgmPrefix + enemyPreset' in music, "专属槽位名由前缀加敌人预设名拼成"),
        check('MusicManager.Instance?.PlayBattleSlot(BattleStateManager.ResolveEnemyPreset())' in battle,
              "battlefield_ 以当前敌人预设请求战斗BGM"),
        check('PlaySlot("battle")' not in battle, "battlefield_ 不再写死通用战斗槽位"),
        check('public static string ResolveEnemyPreset()' in state, "BattleStateManager 提供敌人预设解析"),
        check('return IsCampaignMode ? SelectedEnemy : DefaultEnemyPreset;' in state,
              "非战役模式回退到 DefaultEnemyPreset"),
        check('public const string DefaultEnemyPreset = "berlin";' in state,
              "默认敌人预设收敛为具名常量"),
        check(state.count('"berlin"') == 1, "berlin 字面量只在常量定义处出现一次"),
    ]

    # --------------------------- 边界情况白盒测试 ---------------------------
    print("\n--- 边界白盒测试 ---")
    edge = [
        check('Dictionary<string, string[]> slotPaths = new(StringComparer.OrdinalIgnoreCase)' in music,
              "槽位名忽略大小写，写错大小写不会静默失效"),
        check('if (paths.Length > 0) slotPaths[key] = paths;' in music,
              "空值槽位不进入槽位表，HasSlot 返回 false 而非空路径"),
    ]

    # 同一槽位重复进入不重新抽曲：该判断必须早于随机抽取
    same_slot_at = music.find('if (currentSlot == slot && player.Playing) return;')
    pick_call_at = music.find('string path = PickPath(paths);')
    edge.append(check(0 <= same_slot_at < pick_call_at,
                      "同槽位重入的提前返回早于随机抽取，重复进场景不会换曲"))

    edge += [
        check('if (currentPath == path && player.Playing)' in music,
              "换槽位抽到同一首时保持连续，不从头重播"),
        check('GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: failed to load {path}");' in music,
              "资源加载失败记录带时间与代码位置的警告并返回"),
        check('GD.Print($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: PlaySlot({slot}) -> {path}");' in music,
              "播放记录带时间与代码位置的日志"),
        check('if (stream is AudioStreamMP3 mp3) mp3.Loop = true;' in music,
              "MP3 背景音乐循环播放"),
        check('AudioStreamOggVorbis.Loop' in music and 'AudioStreamWAV.LoopMode' in music,
              "源码注明非 MP3 格式需补的循环设置，避免后续改用 ogg/wav 时静音"),
    ]

    # 注释符陷阱：iniHandler 只认 ';'，含 = 的 '#' 行会被当成键
    music_section = config.split('[music]', 1)[1]
    hash_key_lines = [ln for ln in music_section.splitlines()
                      if ln.lstrip().startswith('#') and '=' in ln]
    edge.append(check(not hash_key_lines,
                      "music.ini 未使用 # 注释：iniHandler 只识别 ';'，含 = 的 # 行会成为垃圾槽位"))

    # 配置里真实生效的音频资源必须存在（注释行里的示例不算）
    live_lines = [ln for ln in music_section.splitlines()
                  if '=' in ln and not ln.lstrip().startswith(';')]
    paths = re.findall(r'res://([^\s,;]+)', "\n".join(live_lines))
    missing = sorted({p for p in paths if not (ROOT / p).exists()})
    edge.append(check(not missing, f"music.ini 实际引用的音频资源均存在（缺失：{missing}）"))

    # ------------------------------ 回归验证 ------------------------------
    print("\n--- 回归验证 ---")
    # battleBGM_ 走 [music] 段的通用键解析，LoadConfig 的代码内不应出现专用分支
    load_body = "\n".join(
        ln for ln in music.split('private void LoadConfig()')[1].splitlines()
        if not ln.strip().startswith('//'))
    regression = [
        check('MusicManager.Instance?.PlaySlot("start_menu")' in start, "StartMenu 请求菜单音乐"),
        check('MusicManager.Instance?.PlaySlot("world_map")' in world, "WorldMap 请求地图音乐"),
        check(config.count('res://assest/music/配乐1.mp3') == 3, "三个基础槽位仍指向同一首，跨场景不重播"),
        check('battleBGM' not in load_body,
              "battleBGM_ 由通用键解析自动成为槽位，LoadConfig 无专用分支"),
    ]

    # 未被配置专属BGM的战斗必须回退，故实际战斗中至少能听到通用战斗曲
    configured = set(re.findall(r'^\s*(battleBGM_\w+)\s*=', music_section, re.M))
    presets = set(re.findall(r'^\[(\w+)\]', read("cards/enemyTurn.ini"), re.M))
    covered = sorted(configured & presets)
    if covered:
        info(f"已配置专属BGM的战斗：{covered}")
    else:
        info(f"尚无战斗配置专属BGM（enemyTurn.ini 共 {len(presets)} 个预设），全部回退到通用 battle 槽位")

    results = smoke + basic + edge + regression
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
