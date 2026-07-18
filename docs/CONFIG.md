# 配置项文档

## 卡牌配置

### cards/card.ini
定义所有卡牌的数据。每个节是一个卡牌。

**卡牌字段说明：**
| 字段 | 说明 | 示例 |
|------|------|------|
| price | 费用 | 1 |
| attack | 攻击力 | 2 |
| defense | 防御力 | 2 |
| icon | 卡牌图片路径 | res://cards/德国步兵.png |
| name | 卡牌名称 | 德国步兵 |
| rarity | 稀有度：Common/Rare/Epic/Legendary/Unobtainable | Unobtainable |
| cardType | 类型：Infantry/Tank/Plane/Bomber/Artillery/Command | Infantry |
| description | 效果描述文字 | 德国国防军标准步兵 |
| effect | 效果脚本 | Deployed:... |
| isHq | 是否总部：0=普通卡,1=总部 | 1 |
| targetType | 目标类型 | NOTarget |
| traits | 特性（逗号分隔） | HeavyArmor,Guardian |
| playEffect | 打出时播放的视觉效果短名称，可省略 | deploy_flash |
| attackEffect | 攻击时播放的视觉效果短名称，可省略 | bullet |

`playEffect`和`attackEffect`由`EffectRegistry`解析。字段缺失、值为空或名称未注册时不播放视觉效果。当前已注册`bullet`；现有单位卡使用该攻击效果，首版尚未配置具体`playEffect`。

单位当前攻击力为0时，主动攻击、普通反击和伏击均不会播放`attackEffect`。攻击力大于0但伤害被重甲或免疫修正为0时仍会播放。

### cards/enemyTurn.ini
定义敌方关卡预设的回合行动脚本。

每个section必须在标题下配置`name`，作为任务选择界面的战斗显示名称。`name`是元数据，不会进入敌人行动队列；缺失时运行时记录错误并回退显示section ID。

**行动格式：**
- `tN=行动` - 第N回合执行的行动
- `everyNt=行动` - 每N回合执行的行动
- `ADD:行动` - 成长型包装指令，将行动添加到永久队列（注册当回合及之后每回合执行）
- `default=行动` - 无特定行动时的默认行动

**行动字符串末尾可附加属性元数据（与card.ini的effect相同格式）：**
```
t1=addToEnemySupportLine(de_tiger)[icon=boss,description=部署虎式重坦]
```
- `icon=` - 意图面板中显示的图标名（对应 `res://assest/{icon}.png`）
- 敌人意图当前支持`boss`、`normalUnit`、`bigUnit`、`heal`、`damage`、`upgrade`，名称区分大小写；缺失或未知名称回退为`boss`。
- `description=` - 意图面板中显示的人话描述
- 元数据由 `StripBracketsOutsideQuotes` 自动剥离，不影响执行
- 每个敌方预设必须配置且仅配置一个固定`tN=ADD:`成长行动；禁止与`everyNt`组合，避免重复注册和叠加失控

### bin/AreaPool.ini
定义区域任务池。area2-9均匀分配50个历史战役和50个事件，敌人值对应 `enemyTurn.ini` section，事件值使用 `event:事件ID`。

### bin/event.ini
定义50个历史背景事件。每个事件包含2至3个选项，效果支持 `none`、`materialPoints(n)`、`replaceCard(id)` 和 `replaceRandomCard(id)`，多个效果使用逗号连接。

`materialPoints(n)` 让玩家获得非负整数 `n` 点战役资源，结果即时同步到世界地图，资源总量最高为 `int.MaxValue`。负数、非整数和超出整数范围的参数不会生效，并记录包含时间和代码位置的错误日志。该效果与战斗内指挥点 `AddPoint(n)` 无关。

### bin/deck.ini
定义玩家的初始卡组。

### Dialogue Manager

`project.godot`启用`addons/dialogue_manager/plugin.cfg`，并注册`DialogueManager`和`GameDialogue`两个autoload。`dialogue_manager/runtime/balloon_path`指定项目气泡`res://core_ui/game_dialogue_balloon.tscn`。对白文件位于`dialogues/`，立绘位于`assest/{normal|happy|sad|angry}.png`。

### configs/music.ini

全局背景音乐槽位配置，供`MusicManager`读取：

- `start_menu`：开始菜单BGM
- `world_map`：世界地图BGM
- `battle`：战斗场景BGM

对应值为`res://`音频资源路径；留空表示该槽位当前不播放音乐。

### bin/setting.ini
定义开始菜单中“设置”场景展示的设置项。每个 section 是一个设置项，支持布尔开关和数值滑块：

| 字段 | 说明 | 示例 |
|------|------|------|
| type | 设置类型：`bool` 或 `float` | float |
| name | 设置界面显示名 | 允许屏幕震动 |
| key | `SettingsManager` 读取使用的唯一键 | allow_screen_shake |
| value | 默认值 | 100 |
| min | `float` 滑块下限 | 0 |
| max | `float` 滑块上限 | 100 |
| step | `float` 滑块每次调整的步长 | 1 |

四项音量的默认值、上下限与步长均可在此文件对应 section 中配置。用户修改结果保存到`user://settings.cfg`，不会改写项目内的默认配置。

## 德军敌人卡牌清单（v2.0）

| ID | 名称 | 费用 | 攻/防 | 类型 | 特性 |
|----|------|------|--------|------|------|
| de_infantry | 德国步兵 | 1 | 2/2 | Infantry | - |
| de_mg42 | MG42机枪组 | 2 | 3/1 | Infantry | Ambush |
| de_panzer4 | 四号坦克 | 3 | 3/3 | Tank | Blitz |
| de_panther | 黑豹坦克 | 4 | 4/4 | Tank | Determination |
| de_tiger | 虎式重坦 | 6 | 6/6 | Tank | HeavyArmor |
| de_stuka | 斯图卡 | 2 | 2/1 | Plane | Shock |
| de_88mm | 88毫米炮 | 3 | 4/2 | Artillery | HeavyArmor,Guardian |
| de_sturmpionier | 突击工兵 | 2 | 3/2 | Infantry | Blitz,Shock |
| de_fallschirmjager | 伞兵 | 2 | 2/2 | Infantry | SmokeScreen |
| de_ss_guard | 党卫军卫队 | 4 | 4/3 | Infantry | Guardian,Determination |
| de_bunker | 混凝土碉堡 | 5 | 2/6 | Artillery | HeavyArmor,Guardian |
| de_volksgrenadier | 国民掷弹兵 | 1 | 1/1 | Infantry | Mobilize |

所有德军敌人卡牌均使用 `res://cards/德国步兵.png` 作为图标，rarity=Unobtainable（不可获得）。

## 敌人预设主题

`cards/enemyTurn.ini` 包含50个按1941-1945时间线组织的历史战役预设。area2-9每关行动密度由9条递增至12条，处决回合由18-20提升至31-34；每关包含一个成长行动，并混合贴膜、刷兵和debuff，详细分配见 `CAMPAIGN_CONTENT.md`。
