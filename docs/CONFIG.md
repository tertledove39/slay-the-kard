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
定义区域任务池，按 `[area1]`～`[area7]` 分区。敌人值对应 `enemyTurn.ini` section，事件值使用 `event:事件ID`。每个区域按钮对应一个分区，段名必须与 `bin/worldMap.tscn` 中的区域按钮节点名一致。

`areaTimes` 是该区域的**战斗烈度**：进入区域时任务选择面板显示该值，每完成一场战斗或一个事件减 1，归零时解锁下一区域。缺失时使用默认值 3（`Area.DefaultAreaTimes`）；非正整数会被记录错误并回退默认值。该键是区域元数据，不会被当作可抽取任务。

### bin/event.ini
定义50个历史背景事件。每个事件包含2至3个选项，效果支持 `none`、`materialPoints(n)`、`replaceCard(id)` 和 `replaceRandomCard(id)`，多个效果使用逗号连接。

`materialPoints(n)` 让玩家获得非负整数 `n` 点战役资源，结果即时同步到世界地图，资源总量最高为 `int.MaxValue`。负数、非整数和超出整数范围的参数不会生效，并记录包含时间和代码位置的错误日志。该效果与战斗内指挥点 `AddPoint(n)` 无关。

### bin/deck.ini
定义玩家的初始卡组。

### Dialogue Manager

`project.godot`启用`addons/dialogue_manager/plugin.cfg`，并注册`DialogueManager`和`GameDialogue`两个autoload。`dialogue_manager/runtime/balloon_path`指定项目气泡`res://core_ui/game_dialogue_balloon.tscn`。对白文件位于`dialogues/`，立绘位于`assest/{normal|happy|sad|angry}.png`。

### configs/music.ini

全局背景音乐槽位配置，供`MusicManager`读取。`[music]`段下**每个键都是一个槽位**，`LoadConfig()`遍历全部键，新增槽位无需改代码。

- `start_menu`：开始菜单BGM
- `world_map`：世界地图BGM
- `battle`：战斗场景BGM（通用）
- `battleBGM_<敌人预设名>`：某场战斗的专属BGM，预设名为`cards/enemyTurn.ini`的section名。未配置时自动回退到`battle`

| 约定 | 说明 |
|------|------|
| 值格式 | `res://`音频资源路径；多首曲目用英文逗号分隔，播放时随机抽取一首 |
| 空值 | 留空表示该槽位当前不播放音乐 |
| 大小写 | 槽位名忽略大小写 |
| 注释 | 只能用分号`;`；`iniHandler`不把`#`当注释，含`=`的`#`行会成为垃圾键 |

完整说明见`docs/MUSIC.md`。

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

## 德军敌人卡牌清单

名称一列取自 `cards/card.ini` 的 `name` 字段；关卡行动描述里写单位名时必须与此一致（`tests/verify_campaign_content.py` 会校验）。

| ID | 名称 | 费用 | 攻/防 | 类型 | 特性 | 效果 |
|----|------|------|--------|------|------|------|
| de_infantry | 第1步兵团 | 1 | 2/2 | Infantry | - | - |
| de_mg42 | 火力小组 | 2 | 3/1 | Infantry | Ambush | 敌方回合开始时对随机敌方单位造成1点伤害 |
| de_panzer4 | 四号坦克 | 3 | 3/3 | Tank | Blitz | - |
| de_panther | 黑豹坦克 | 4 | 4/4 | Tank | Determination | - |
| de_tiger | 虎式重坦 | 6 | 6/6 | Tank | HeavyArmor | - |
| de_tigerKing | 虎王 | 12 | 10/10 | Tank | HeavyArmor | 敌方回合开始时使1个友方单位获得+2+2 |
| de_stuka | 斯图卡 | 2 | 2/1 | Plane | Shock | 攻击时对随机敌方单位造成1点伤害 |
| de_88mm | 88毫米炮 | 3 | 4/2 | Artillery | HeavyArmor,Guardian | - |
| de_sturmpionier | 突击工兵 | 2 | 3/2 | Infantry | Blitz,Shock | - |
| de_fallschirmjager | 伞兵 | 2 | 2/2 | Infantry | SmokeScreen | 敌方回合开始时获得+1+1 |
| de_ss_guard | 党卫军卫队 | 4 | 4/3 | Infantry | Guardian,Determination | - |
| de_bunker | 混凝土碉堡 | 5 | 2/6 | Artillery | HeavyArmor,Guardian | - |
| de_volksgrenadier | 国民掷弹兵 | 1 | 1/1 | Infantry | Mobilize | - |
| de_ufo | 火星飞碟 | 12 | 12/12 | Bomber | HeavyArmor | - |
| de_karl | 卡尔臼炮 | 6 | 5/5 | Artillery | Immunity | 敌方回合开始时失去1点防御力；亡语：对敌方总部造成等同于自身攻击力的伤害 |
| de_nebelwerfer | 涅贝尔维尔弗 | 3 | 2/2 | Artillery | - | 敌方回合开始时压制1个随机敌方单位 |
| de_befehlspanzer | 装甲指挥车 | 3 | 1/5 | Tank | Guardian | 敌方回合开始时所有友方单位获得+1攻击力 |
| de_brummbar | 灰熊突击炮 | 4 | 3/4 | Artillery | HeavyArmor | 受到伤害时获得+1攻击力 |

所有德军敌人卡牌均使用 `res://cards/德国步兵.png` 作为图标，rarity=Unobtainable（不可获得）。

描述写法的视角约定：`card.ini` 中德军卡的描述按**卡牌主人视角**写，因此玩家方在描述里是「敌方」。这与指令的绝对语义对应如下：

| 描述里写 | 实际指令 |
|---|---|
| 友方单位 / 友方总部 | `GetAllEnemyUnits`、`GetRandomEnemyUnit`、`enemyHq` |
| 敌方单位 / 敌方总部 | `GetAllFriendUnits`、`GetRandomFriendUnit`、`myHq` |

先例：`de_tigerKing` 写「使1个友方单位获得+2+2」，代码用 `GetRandomEnemyUnit`。

## 敌人预设主题

`cards/enemyTurn.ini` 的每个section是一关，由 `bin/AreaPool.ini` 分配到area1-7。每个section的`name`为任务界面显示的中文名，其余`tN=`、`everyNt=`、`default=`键为敌方行动脚本，语法详见 `LOGIC.md` 的「敌方行动脚本键」一节。
