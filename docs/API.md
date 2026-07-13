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
| `PlaySlot(string slot)` | `void` | 按`configs/music.ini`中的槽位名播放背景音乐；相同槽位重复请求不会重播 |
| `StopMusic()` | `void` | 停止当前BGM |
| `SetVolumeDb(float db)` | `void` | 设置全局BGM音量 |
