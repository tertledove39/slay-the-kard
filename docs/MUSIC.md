# 全局音乐管理

## 方案

项目使用autoload的`MusicManager`实现全局不间断背景音乐。`MusicManager`常驻一个`AudioStreamPlayer`，场景切换不会中断播放。

## 文件

- `core_logic/MusicManager.cs`：全局音乐管理器。
- `configs/music.ini`：BGM槽位配置。

## 配置

`configs/music.ini`的`[music]`段，**每个键是一个槽位**，`LoadConfig()`遍历该段全部键，因此新增槽位不需要改代码：

```ini
[music]
start_menu=res://path/to/menu.mp3
world_map=res://path/to/world.mp3
battle=res://path/to/battle.mp3,res://path/to/battle_alt.mp3
```

规则：

| 规则 | 说明 |
|------|------|
| 一槽多曲 | 值可用英文逗号分隔多首曲目，播放时随机抽取一首；只写一首即固定播放它 |
| 不连续重复 | 多曲槽位抽到正在播放的那首时会重新抽，避免连续两遍同一首 |
| 空值 | 留空（或只有空白）的槽位不进入槽位表，`HasSlot()`对其返回false |
| 槽位名 | 忽略大小写，`battleBGM_DonBend`与`battleBGM_donbend`等价 |
| 注释符 | **只能用分号`;`**。`bin/iniHandler.cs`的解析器只把`;`开头当注释，含`=`的`#`行会被当成一个键名 |

### 战斗专属BGM

槽位名前缀`battleBGM_`加敌人预设名（即`cards/enemyTurn.ini`的section名）即为该战斗的专属BGM：

```ini
battleBGM_DonBend=res://assest/music/配乐2.mp3
battleBGM_berlin_final_battle=res://assest/music/配乐3.mp3,res://assest/music/配乐4.mp3
```

`battlefield_`进场时调用`PlayBattleSlot(BattleStateManager.ResolveEnemyPreset())`：该槽位有配置就播专属曲，**没配置就自动回退到通用`battle`槽位**，因此只为部分战斗配曲是安全的，其余战斗照常播放通用战斗曲。

专属槽位由`[music]`段的通用键解析自动产生，`LoadConfig()`中没有任何`battleBGM_`专用分支。

## 场景接入

- `StartMenu` 请求 `start_menu`
- `WorldMap` 请求 `world_map`
- `battlefield_` 请求 `battleBGM_<敌人预设名>`，回退 `battle`

`Store`、`ChooseMission`、`EventScene`、`PostBattleReward` 不主动切歌，会继承当前场景音乐。

## API

- `MusicManager.Instance?.PlaySlot("start_menu")` — 播放指定槽位
- `MusicManager.Instance?.PlayBattleSlot(enemyPreset)` — 播放战斗BGM，含专属槽位回退
- `MusicManager.Instance?.HasSlot("battleBGM_X")` — 槽位是否配置了曲目
- `MusicManager.Instance?.StopMusic()`
- `MusicManager.Instance?.SetVolumeDb(-6f)`
- 常量：`MusicManager.BattleBgmPrefix`（`"battleBGM_"`）、`MusicManager.BattleSlot`（`"battle"`）

每次实际切换曲目都会打印带时间与代码位置的日志，便于确认抽中了哪一首。

## 已知限制

- **同时只能播放一首**：只有一个`AudioStreamPlayer`，不支持BGM叠加环境音。
- **循环只覆盖MP3**：源码中为`if (stream is AudioStreamMP3 mp3) mp3.Loop = true;`。改用`.ogg`或`.wav`需同时补上`AudioStreamOggVorbis.Loop`与`AudioStreamWAV.LoopMode`，否则播完即静音。

## 音量分类

- `Master`：所有声音最终汇入此Bus，受主音量修正。
- `Music`：`MusicManager`播放的背景音乐。
- `SFX`：战斗、卡牌等游戏效果音。
- `UI`：按钮等界面声音，目前预留给后续UI音效。

设置菜单使用0至100的滑块修改各Bus音量，0会静音。滑块上下限、默认值与步长由`bin/setting.ini`配置。
