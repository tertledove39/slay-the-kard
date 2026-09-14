# 开始菜单与设置

## 启动流程

- `project.godot` 的主场景为 `bin/start_menu.tscn`。
- 开始菜单使用 `assest/table.jpeg` 全屏铺底，左下纵向提供继续、开始、设置和鸣谢按钮。
- 继续和开始都通过 `SceneLoader` 进入 `bin/worldMap.tscn`；设置进入 `bin/settings_menu.tscn`；鸣谢在当前菜单显示文本面板。
- 所有菜单按钮在悬浮时缩放至 1.08 倍，移开后复原。
- 四个按钮通过场景 `pressed` 信号调用 `_on_continue_pressed`、`_on_start_pressed`、`_on_settings_pressed` 和 `_on_credits_pressed`；悬浮动画使用 `Menu/...` 完整节点路径连接。

## 设置配置

配置文件：`bin/setting.ini`

每个 section 定义一个设置项，支持 `bool` 和 `float`：

```ini
[allowScreenShake]
type=bool
name=允许屏幕震动
key=allow_screen_shake
value=true
```

- `type`：设置类型，支持 `bool` 和 `float`。
- `name`：设置界面展示名称。
- `key`：代码读取设置的唯一键。
- `value`：首次运行的布尔值，`true` 或 `false`。

`float` 设置额外使用 `min`、`max` 和 `step` 配置滑块范围与步长。当前提供主音量、配乐音量、效果音量和UI音量四项，默认范围为0至100。

`SettingsManager.Initialize()`读取默认配置和`user://settings.cfg`中的用户值；`GetBool`/`SetBool`及`GetFloat`/`SetFloat`用于跨场景访问。设置界面按类型自动创建`CheckButton`或`HSlider`，修改后立即保存并应用。
