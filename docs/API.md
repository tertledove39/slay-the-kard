# 对外API

## GameDialogue

位置：`core_logic/GameDialogue.cs`；节点：`/root/GameDialogue`。

| API | 返回值 | 说明 |
|-----|--------|------|
| `PlayAsync(string resourcePath, string title = "start")` | `Task<bool>` | 排队播放指定Dialogue Manager资源和标题，等待播放结束 |
| `Play(string resourcePath, string title = "start")` | `void` | 启动播放但不等待，适合普通UI回调 |

开始菜单调用返回`false`且不播放。资源按路径缓存，多次播放不会重复加载。

## MusicManager

位置：`core_logic/MusicManager.cs`；节点：`/root/MusicManager`。

| API | 返回值 | 说明 |
|-----|--------|------|
| `PlaySlot(string slot)` | `void` | 按`configs/music.ini`中的槽位名播放背景音乐；槽位可配多首曲目（逗号分隔），随机抽取并避开正在播放的那首；相同槽位重复请求不会重播 |
| `PlayBattleSlot(string enemyPreset)` | `void` | 战斗BGM入口：优先`battleBGM_<预设名>`槽位，未配置时回退到`BattleSlot`（`"battle"`） |
| `HasSlot(string slot)` | `bool` | 槽位是否配置了至少一首曲目 |
| `StopMusic()` | `void` | 停止当前BGM |
| `SetVolumeDb(float db)` | `void` | 设置全局BGM音量 |

常量`BattleBgmPrefix`（`"battleBGM_"`）与`BattleSlot`（`"battle"`）供调用方拼接槽位名，避免写死字符串。
