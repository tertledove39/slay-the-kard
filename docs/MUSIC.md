# 全局音乐管理

## 方案

项目使用autoload的`MusicManager`实现全局不间断背景音乐。`MusicManager`常驻一个`AudioStreamPlayer`，场景切换不会中断播放。

**切换场景不会掐断当前曲目**：请求新槽位时只入队，等当前曲目自然播完再切。详见下节。

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

## 切换时机：播完再切

`PlaySlot()`**不会立即换曲**。当前有曲目在播时，新槽位只记入`pendingSlot`，真正换曲发生在曲末：

| 情形 | 行为 |
|------|------|
| 当前无曲目在播（首次进场景、已停止） | 立即起播，不排队 |
| 请求的槽位就是正在播的那个 | 撤销排队，继续把这首放完 |
| 请求别的槽位 | 排队；连续切换时后者覆盖前者，记住最后一次请求 |
| 曲末，队列非空 | 切到排队的槽位 |
| 曲末，队列为空 | 从**当前槽位**再随机取一首续播（单曲槽位即等于原来的循环） |

实现要点：

- 订阅`AudioStreamPlayer.Finished`驱动曲末判断。该信号只在流**非循环**时触发。
- 因此`DisableBuiltinLoop()`在起播前关掉三种格式的内建循环（`AudioStreamMP3.Loop`、`AudioStreamOggVorbis.Loop`、`AudioStreamWav.LoopMode`），循环改由曲末回调重新起播实现。这同时修掉了旧版"只有MP3设了循环、ogg/wav播完即静音"的问题。
- 关掉内建循环意味着曲末重新起播处会有一个极短的接缝，这是换取"能在曲末精确切槽位"的代价。
- 槽位的曲目因此被当作播放列表使用：`battle`槽位配了 12 首，曲末会在其中随机续播，而不是把同一首放到底。

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
- **切换有等待**：进入战斗后可能要等世界地图的曲子放完才会响战斗曲，最长等于一首曲子的时长。这是"不掐断当前曲目"的直接代价。
- **切点无淡入淡出**：换曲发生在曲末的自然边界，但边界处仍是硬切，没有交叉淡化。

## 音量分类

- `Master`：所有声音最终汇入此Bus，受主音量修正。
- `Music`：`MusicManager`播放的背景音乐。
- `SFX`：战斗、卡牌等游戏效果音。
- `UI`：按钮等界面声音，目前预留给后续UI音效。

设置菜单使用0至100的滑块修改各Bus音量，0会静音。滑块上下限、默认值与步长由`bin/setting.ini`配置。
