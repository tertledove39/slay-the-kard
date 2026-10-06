# Dialogue Manager 对白系统

## 结构

- 插件：`addons/dialogue_manager/`，版本3.10.4。
- 全局接口：`core_logic/GameDialogue.cs`，autoload名称为`GameDialogue`。
- 项目气泡：`core_ui/game_dialogue_balloon.tscn`。
- 表情控制：`core_ui/GameDialogueBalloon.cs`。
- 立绘位置：`core_ui/game_dialogue_balloon.tscn` 的 `Portrait` 节点，靠最左侧（x 0→420），底部停在对话框上方（y 240→680），避免遮挡底部对话框与中部任务卡。
- 对白内容：`dialogues/*.dialogue`。
- 示例：`dialogues/example.dialogue`。
- 战役通关：`dialogues/campaign_victory.dialogue`，标题 `campaign_victory`，由 `bin/CampaignVictory.cs` 在最后一个区域烈度归零时播放。

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
德军军官: 所有单位进入战斗位置。 [#angry]
- 继续进攻
    苏军指挥员: 突破他们的防线。 [#happy]
- 谨慎推进
    苏军指挥员: 先确认敌军火力。 [#normal]
=> END
```

标签必须带 `#`：每行可使用 `[#normal]`、`[#happy]`、`[#sad]`、`[#angry]` 切换 `assest/` 下同名 PNG 立绘，也兼容 `[#expression=happy]` 形式。使用 `[#portrait=none]` 隐藏立绘。多个标签用逗号分隔，如 `[#happy, portrait=none]`。

**写成 `[normal]`（缺少 `#`）不会被识别为标签，而会作为正文原样显示在对话框里。** 解析规则见 `addons/dialogue_manager/compiler/compiler_regex.gd` 的 `TAGS_REGEX = \[#(?<tags>.*?)\]`。

没有标签时使用`normal.png`；指定图片不存在时记录带时间和代码位置的警告并回退`normal.png`。

选项块之后、缩进回到基级的行会继续接回主线，不必在每个分支里重复同一句。

## 气球实现说明

`core_ui/game_dialogue_balloon.tscn` 内嵌的 `ExampleBalloon` 子节点实例化的是插件自带的 `example_balloon.tscn`，其根节点挂的是 **GDScript** 实现 `example_balloon.gd`。

因此 `GameDialogueBalloon.Start()` **不能**把它强制转换为 C# 的 `DialogueManagerRuntime.ExampleBalloon`（节点底层类型是 `CanvasLayer`），而是按方法名动态调用 `start(with_dialogue_resource, title, extra_game_states)`。立绘切换依赖的 `got_dialogue` 信号由 `DialogueManager` 单例发出，与内层气球用哪种语言实现无关。

`happy.png`、`angry.png`、`normal.png`、`sad.png` 均已存在，四种表情标签都可直接使用。

对话框外观（无边框、黑色半透明底色、角色名字号）由 `core_ui/GameDialogueBalloon.cs` 的 `ApplyBalloonStyle()` 通过节点级 theme override 设置，因此不需要改动 `addons/` 下的插件资源。

## 战斗调用

在需要暂停流程的位置使用`await PlayAsync`，例如敌方行动前：

```csharp
await GetNode<GameDialogue>("/root/GameDialogue").PlayAsync(
    "res://dialogues/example.dialogue",
    "battle_warning"
);
```

不要在需要严格顺序的战斗流程中调用`Play`，否则战斗会在对白显示期间继续执行。
