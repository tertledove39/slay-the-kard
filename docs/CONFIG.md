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

### cards/enemyTurn.ini
定义敌方关卡预设的回合行动脚本。

**行动格式：**
- `tN=行动` - 第N回合执行的行动
- `everyNt=行动` - 每N回合执行的行动
- `ADD:行动` - 添加行动到永久队列（每回合执行）
- `default=行动` - 无特定行动时的默认行动

**行动字符串末尾可附加属性元数据（与card.ini的effect相同格式）：**
```
t1=addToEnemySupportLine(de_tiger)[icon=boss,description=部署虎式重坦]
```
- `icon=` - 意图面板中显示的图标名（对应 `res://assest/{icon}.png`）
- `description=` - 意图面板中显示的人话描述
- 元数据由 `StripBracketsOutsideQuotes` 自动剥离，不影响执行

### bin/AreaPool.ini
定义区域任务池。area2-9均匀分配50个历史战役和50个事件，敌人值对应 `enemyTurn.ini` section，事件值使用 `event:事件ID`。

### bin/event.ini
定义50个历史背景事件。每个事件包含2至3个选项，效果仅使用 `none`、`replaceCard(id)` 和 `replaceRandomCard(id)`。

### bin/deck.ini
定义玩家的初始卡组。

### Dialogue Manager

`project.godot`启用`addons/dialogue_manager/plugin.cfg`，并注册`DialogueManager`和`GameDialogue`两个autoload。`dialogue_manager/runtime/balloon_path`指定项目气泡`res://core_ui/game_dialogue_balloon.tscn`。对白文件位于`dialogues/`，立绘位于`assest/{normal|happy|sad|angry}.png`。

### bin/setting.ini
定义开始菜单中“设置”场景展示的设置项。每个 section 是一个设置项，当前支持布尔开关：

| 字段 | 说明 | 示例 |
|------|------|------|
| type | 设置类型，当前仅支持 `bool` | bool |
| name | 设置界面显示名 | 允许屏幕震动 |
| key | `SettingsManager` 读取使用的唯一键 | allow_screen_shake |
| value | 初始布尔值 | true |

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

`cards/enemyTurn.ini` 包含50个按1941-1945时间线组织的历史战役预设。area2-9的处决回合依次由18-20提升至31-34，详细分配见 `CAMPAIGN_CONTENT.md`。
