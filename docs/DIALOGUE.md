# Dialogue Manager 对白系统

## 结构

- 插件：`addons/dialogue_manager/`，版本3.10.4。
- 全局接口：`core_logic/GameDialogue.cs`，autoload名称为`GameDialogue`。
- 项目气泡：`core_ui/game_dialogue_balloon.tscn`。
- 表情控制：`core_ui/GameDialogueBalloon.cs`。
- 对白内容：`dialogues/*.dialogue`。
- 示例：`dialogues/example.dialogue`。

开始菜单`res://bin/start_menu.tscn`禁止播放对白，其他场景均可调用。并发请求按调用顺序等待，避免多个气泡重叠。

## C#调用

需要等待对白结束后继续执行时：

```csharp
GameDialogue dialogue = GetNode<GameDialogue>("/root/GameDialogue");
bool completed = await dialogue.PlayAsync(
    "res://dialogues/example.dialogue",
    "battle_warning"
);
```

按钮等无需等待的入口：

```csharp
GetNode<GameDialogue>("/root/GameDialogue").Play(
    "res://dialogues/example.dialogue",
    "world_map_intro"
);
```

`PlayAsync`返回`false`表示当前位于开始菜单、路径为空或资源加载失败，返回`true`表示对白正常结束。

## 编写对白

```text
~ battle_warning
德军军官: 所有单位进入战斗位置。 [angry]
- 继续进攻
    苏军指挥员: 突破他们的防线。 [happy]
- 谨慎推进
    苏军指挥员: 先确认敌军火力。 [normal]
=> END
```

每行可使用`[normal]`、`[happy]`、`[sad]`、`[angry]`切换`assest/`下同名PNG立绘，也兼容`[expression=happy]`形式。使用`[portrait=none]`隐藏立绘。没有标签时使用`normal.png`；指定图片不存在时记录带时间和代码位置的警告并回退`normal.png`。

当前存在`happy.png`、`angry.png`、`normal.png`，尚未发现`sad.png`。补充`res://assest/sad.png`后无需修改代码。

## 战斗调用

在需要暂停流程的位置使用`await PlayAsync`，例如敌方行动前：

```csharp
await GetNode<GameDialogue>("/root/GameDialogue").PlayAsync(
    "res://dialogues/example.dialogue",
    "battle_warning"
);
```

不要在需要严格顺序的战斗流程中调用`Play`，否则战斗会在对白显示期间继续执行。
