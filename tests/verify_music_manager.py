#!/usr/bin/env python3
"""全局背景音乐管理器验证：多曲目槽位随机播放、战斗专属BGM与回退、切换场景不掐断当前曲目。"""
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

    # 供白盒检查用的方法体切片
    playslot_body = music.split('public void PlaySlot(string slot)')[1].split('/// <summary>立刻起播')[0]
    startslot_body = music.split('private void StartSlot(string slot)')[1].split('/// <summary>曲目自然播完')[0]
    finished_body = music.split('private void OnTrackFinished()')[1].split('/// <summary>')[0]

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

    # --------------------- 基本验证：切换场景不掐断当前曲目 ---------------------
    print("\n--- 基本验证：播完再切 ---")
    switch = [
        check('player.Finished += OnTrackFinished;' in music,
              "订阅 AudioStreamPlayer.Finished，能在曲末那一刻动作"),
        check('private string pendingSlot = "";' in music, "以 pendingSlot 记录待切换槽位"),
        check('private void OnTrackFinished()' in music, "提供曲末回调"),
    ]

    # PlaySlot 只排队，绝不自己起播：真正换曲只发生在 StartSlot
    switch += [
        check('player.Play()' not in playslot_body,
              "PlaySlot 体内没有任何起播调用，换曲时机完全交给曲末回调"),
        check('pendingSlot = slot;' in playslot_body, "不同槽位只记入待切换队列"),
        check('if (!player.Playing)' in playslot_body and 'StartSlot(slot);' in playslot_body,
              "当前无曲目在播时立即起播，不必等待"),
        check('if (currentSlot == slot)' in playslot_body and 'pendingSlot = "";' in playslot_body,
              "切回正在播放的槽位时撤销排队，继续把这首放完"),
    ]

    # 曲末：有待切换就切过去，否则留在当前槽位续播；两者都清空队列
    switch += [
        check('!string.IsNullOrEmpty(pendingSlot) ? pendingSlot : currentSlot' in finished_body,
              "曲末优先切到待切换槽位，没有则留在当前槽位续播"),
        check('pendingSlot = "";' in finished_body, "曲末消费掉待切换队列，避免重复触发"),
        check('StartSlot(slot);' in finished_body, "曲末由 StartSlot 真正起播"),
    ]

    # 内建循环必须关掉，否则曲目永不结束、Finished 永不触发
    switch += [
        check('private static void DisableBuiltinLoop(AudioStream stream)' in music,
              "抽出 DisableBuiltinLoop 统一处理内建循环"),
        check('case AudioStreamMP3 mp3: mp3.Loop = false;' in music, "MP3 关闭内建循环"),
        check('case AudioStreamOggVorbis ogg: ogg.Loop = false;' in music, "OGG 关闭内建循环"),
        check('AudioStreamWav wav: wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;' in music,
              "WAV 关闭内建循环（Godot 4.5 类名为 AudioStreamWav）"),
        check('DisableBuiltinLoop(stream);' in startslot_body, "起播前关闭内建循环"),
        check('Loop = true' not in music and 'LoopModeEnum.Forward' not in music,
              "源码中不再有任何开启内建循环的写法"),
    ]

    switch += [
        check('pendingSlot = "";' in music.split('public void StopMusic()')[1].split('public void SetVolumeDb')[0],
              "StopMusic 一并清空待切换队列"),
    ]

    # --------------------------- 边界情况白盒测试 ---------------------------
    print("\n--- 边界白盒测试 ---")
    edge = [
        check('Dictionary<string, string[]> slotPaths = new(StringComparer.OrdinalIgnoreCase)' in music,
              "槽位名忽略大小写，写错大小写不会静默失效"),
        check('if (paths.Length > 0) slotPaths[key] = paths;' in music,
              "空值槽位不进入槽位表，HasSlot 返回 false 而非空路径"),
        check('if (path == currentPath && player.Playing)' in startslot_body,
              "换槽位抽到同一首时保持连续，不从头重播"),
        check('GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: failed to load {path}");' in music,
              "资源加载失败记录带时间与代码位置的警告"),
        check('if (!string.IsNullOrEmpty(currentSlot) && currentSlot != slot) StartSlot(currentSlot);' in startslot_body,
              "载入失败时退回当前槽位续播，避免一个坏路径让整局静音"),
        check('currentSlot != slot' in startslot_body,
              "载入失败的退回带槽位相等判断，递归深度最多两层"),
        check('GD.Print($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: PlaySlot({slot}) -> {path}");' in music,
              "播放记录带时间与代码位置的日志"),
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

    # 多曲槽位的曲目数，用于确认「曲末续播」确有实际意义
    for ln in live_lines:
        key, val = ln.split("=", 1)
        n = len([x for x in val.split(",") if x.strip()])
        if n > 1:
            info(f"槽位 {key.strip()} 配了 {n} 首，曲末会随机续播同槽位的另一首")

    results = smoke + basic + switch + edge + regression
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
