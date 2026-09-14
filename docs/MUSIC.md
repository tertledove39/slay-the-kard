# 全局音乐管理

## 方案

项目使用autoload的`MusicManager`实现全局不间断背景音乐。

## 文件

- `core_logic/MusicManager.cs`：全局音乐管理器。
- `configs/music.ini`：BGM槽位配置。

## 配置

`configs/music.ini`

```ini
[music]
start_menu=res://path/to/menu.ogg
world_map=res://path/to/world.ogg
battle=res://path/to/battle.ogg
```

当前留空表示暂未指定音乐资源；填写后无需改代码。

## 场景接入

- `StartMenu` 请求 `start_menu`
- `WorldMap` 请求 `world_map`
- `battlefield_` 请求 `battle`

`Store`、`ChooseMission`、`EventScene`、`PostBattleReward` 不主动切歌，会继承当前场景音乐。

## API

- `MusicManager.Instance?.PlaySlot("start_menu")`
- `MusicManager.Instance?.StopMusic()`
- `MusicManager.Instance?.SetVolumeDb(-6f)`

## 音量分类

- `Master`：所有声音最终汇入此Bus，受主音量修正。
- `Music`：`MusicManager`播放的背景音乐。
- `SFX`：战斗、卡牌等游戏效果音。
- `UI`：按钮等界面声音，目前预留给后续UI音效。

设置菜单使用0至100的滑块修改各Bus音量，0会静音。滑块上下限、默认值与步长由`bin/setting.ini`配置。
