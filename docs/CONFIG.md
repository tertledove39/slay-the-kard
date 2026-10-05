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

`playEffect`和`attackEffect`由`EffectRegistry`解析。字段缺失、值为空或名称未注册时不播放视觉效果（未注册的名字会打一行日志，否则「拼错了」会表现成「打了没特效」，很难查）。

**这两个字段都可以写多个特效**，用英文逗号分隔（与`traits`的多值写法一致），例如`attackEffect = bullet,smoke`。多个特效**各自独立开跑、互不等待**，总时长等于最长的那个而不是相加。

> 需要「一个特效演到一半再插另一个」的编排（比如飞掠途中投弹）**拼不出来**——拼接写法只能做到一个演完接一个。那种节奏得由一个特效自己掌握，即下面的 `airstrike`。

已注册的特效：

| 名称 | 场景 | 说明 |
|------|------|------|
| `bullet` | `effects/bullet_effect.tscn` | 从攻击者向目标打出一串子弹，固定 10 发（`ProjectileFlightSeconds = 0.3`） |
| `bombing` | `effects/bombing_effect.tscn` | 航弹，**弹数 = 攻击力**（场景里 `ProjectileCount = 0` 表示用调用方给的数量）；`ProjectileFlightSeconds = 1.5`，比子弹慢得多才有投弹感；**不发声**（`BulletEffect` 里没有任何音效代码，不是靠配置关掉的） |
| `smoke` | `effects/smoke_effect.tscn` | 在指定位置冒一下烟 |
| `flying` | `effects/flying_effect.tscn` | 让**触发它的那张卡**升起（`RiseDuration`，只移动位置 + 轻微放大）-> 原地悬停 `SwaySecondsPerCycle × SwayCycles` 秒 -> 落回（`LandDuration`，连角度一起还原），期间抬高层级压住其他卡；音效取 `[sfx] flyby`。**全程不转角度**（`SwayDegrees` 默认 0 = 完全静止悬停；调大才在卡自己的原始角度上左右摆） |
| `airstrike` | `effects/air_strike_effect.tscn` | **空袭 = 飞掠 + 盘旋时投弹**：起飞 -> （边盘旋边投弹）-> 降落。投的是 `bombing` 子特效，所以弹数仍是攻击力；`StrikeEffectName` 留空则退化成纯飞掠 |

`bombing`与`bullet`共用`BulletEffect`脚本，差别只在场景 Export 出去的「弹体场景」与「弹数」——生成、随机错开、回池那套逻辑不写第二遍。

`airstrike`与`flying`的关系是**继承**：`AirStrikeEffect : FlyingEffect`，只覆写「悬停」那一段（`StayAsync`），升起/降落/还原/音效全部沿用父类，所以「卡飘起来」这套运动也只有一份实现。三个特效的对应关系：

| 想要的节奏 | 写法 |
|---|---|
| 只有飞掠 | `attackEffect = flying` |
| 飞掠与投弹同时开跑（起飞的同时航弹已经在飞） | `attackEffect = flying,bombing` |
| 起飞 -> 盘旋投弹 -> 降落 | `attackEffect = airstrike` |

单位当前攻击力为0时，主动攻击、普通反击和伏击均不会播放`attackEffect`。攻击力大于0但伤害被重甲或免疫修正为0时仍会播放。

#### 通用开火声与`SelfVoicedNames`

每次攻击都会放一声通用的开火声（`battleField.tscn` 的`battleSound`，机枪「哒哒」）。但**攻击特效自带音效时不再叠这一声**——飞机掠过时再来一层机枪声会串味。

判定表在`EffectRegistry.SelfVoicedNames`（当前是`flying`、`airstrike`），通过`EffectRegistry.ReplacesFiringSound(attackEffect)`查询，调用点在`battlefield_.cs`的`Attack()`里、`PlayBattleSound(1)`那一行。

- 判定按**攻击者**的`attackEffect`走，不看防守方。
- 判定放在注册表而不是卡上或`CardTypes.Bomber`上：写到卡上、或按兵种判，都会让同一个特效配在不同卡上行为不一致。
- 新增自带音效的特效要加进`SelfVoicedNames`。`tests/verify_attack_effects.py`会交叉核对表里每个名字对应的场景**确实挂了`AudioStreamPlayer`**，防止这张表变成假话。

### cards/enemyTurn.ini
定义敌方关卡预设的回合行动脚本。

每个section必须在标题下配置`name`，作为任务选择界面的战斗显示名称。`name`是元数据，不会进入敌人行动队列；缺失时运行时记录错误并回退显示section ID。

**行动格式：**
- `tN=行动` - 第N回合执行的行动
- `everyNt=行动` - 每N回合执行的行动
- `ADD:行动` - 成长型包装指令，将行动添加到永久队列（注册当回合及之后每回合执行）
- `default=行动` - 无特定行动时的默认行动
- `battleStart=效果` - **关卡开局效果**，战斗开始时结算一次。它**不进行动队列**，因此不会作为一条「敌方意图」显示在左侧面板上；效果来源卡固定为敌方总部，条件直接用通用的 `if(...)跳转` 写，可用的 `&hp` 是战役血量（世界地图心形图标后面那个数）。不写这个键的关卡就是没有开局效果，行为与从前一致

```
battleStart=myHq|heal(&hp*10)[icon=heal,description=开局:每有1条命,友方总部额外获得10点防御力]
```
详见 `docs/LOGIC.md` 的「关卡的『开局效果』`battleStart=`」。

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

`boss` 指定该区域的**终局战斗**，值为 `enemyTurn.ini` 的 section 名（如 `boss=Kalinin`），每区域至多一个：

- 烈度**不为 1** 时，boss 不参与抽取，玩家抽不到它；
- 烈度**为 1** 时，任务面板只提供这一场，玩家没有别的选择。

该键同样是区域元数据，不进入可抽取条目列表；boss 是否已写在 `enemyN` 里都可以，两种写法结果一致。规则的唯一实现在 `bin/MissionDrawer.cs`。

抽取组成固定为 **2 战斗 + 1 事件**（`MissionDrawer.BattleCount` / `EventCount`），可选项不足 3 个时用剩余战斗补位，因此事件永远不超过 1 个。**补位只用战斗**，所以只有事件、没有战斗的区域每次只出 1 个按钮。

### 区域内容分配

区域按地理位置命名、按历史时序推进，`entryN` 的事件按各自的历史时点归入对应区域：

| 区域 | 城市 | 时期 | 战斗 `enemyN` | boss | 事件数 |
|------|------|------|--------------|------|--------|
| area1 | 莫斯科 | 1941 秋 – 1942 初 | 3 | Bryansk | 14 |
| area2 | 斯大林格勒 | 1942 夏 – 1943 初 | 5 | RedOctober | 10 |
| area3 | 斯摩棱斯克 | 1943 秋 | **0（待补）** | — | 6 |
| area4 | 哈尔科夫 | 1943 春 | 7 | KharkovMarch | 6 |
| area5 | 库尔斯克 | 1943 夏 | 7 | KharkovAugust | 5 |
| area6 | 明斯克 | 1944 夏 | **0（待补）** | — | 6 |
| area7 | 柏林 | 1945 春 | 1 | — | 5 |

**boss 按约定不写进 `enemyN`**（`MissionDrawer` 在烈度为 1 时单独提供它），因此上表的「战斗」列不含 boss。
`tests/verify_campaign_content.py` 的「孤立内容」提示把 `enemyN` 与 `boss` 一并算作可达引用——早先只算 `enemyN`，
会把 `Bryansk` / `RedOctober` 这类 boss 误报成「游戏内不可达」。

**area3 与 area6 目前只有事件、没有战斗**（`enemyTurn.ini` 里已有对应的 `name` 骨架，行动脚本待补），
这两个区域进任务面板时只会出 1 个按钮（见上一节「补位只用战斗」）。

> **待处理**：`AreaPool.ini` 头注释把 area3 标为「斯摩棱斯克(1943 秋)」、area4 标为「哈尔科夫(1943 春)」，
> 但哈尔科夫战役在 1943 年 2–3 月、斯摩棱斯克进攻战役在 8–10 月，**时序是倒的**。要么对调 area3/area4，
> 要么订正年代标注。本表按现状（文件实际配置）如实记录，未擅自改动。

同一事件可出现在多个区域（`event.ini` 共 25 个事件均已分配，无遗漏、无重复引用）。全程性事件跨越多个区域：`strange_command`（1941–42 通信混乱）、`tank_crew_replacement`（1941–42 乘员补充）、`deep_battle_doctrine`（纵深作战条令）、`guards_title`（近卫称号）、`snowstorm`（冬季）。专属时点的事件只归一个区域，如 `citadel_intelligence`(1943 春 → 库尔斯克)、`maskirovka_bagration`(1944 夏 → 明斯克)、`reichstag_banner`(1945 → 柏林)、`order_227` 与 `operation_uranus`(1942 → 斯大林格勒)。

同一事件可出现在多个区域（`event.ini` 共 25 个事件均已分配，无遗漏、无重复引用）。全程性事件跨越多个区域：`strange_command`（1941–42 通信混乱）、`tank_crew_replacement`（1941–42 乘员补充）、`deep_battle_doctrine`（纵深作战条令）、`guards_title`（近卫称号）、`snowstorm`（冬季）。专属时点的事件只归一个区域，如 `citadel_intelligence`(1943 春 → 库尔斯克)、`maskirovka_bagration`(1944 夏 → 明斯克)、`reichstag_banner`(1945 → 柏林)、`order_227` 与 `operation_uranus`(1942 → 斯大林格勒)。

### bin/event.ini
定义50个历史背景事件。每个事件包含2至3个选项，效果支持 `none`、`materialPoints(n)`、`hp(n)`、`replaceCard(id)` 和 `replaceRandomCard(id)`，多个效果使用逗号连接。

**选项可否点击由资源点决定**：`materialPoints` 的负数部分累加即为该选项的花费，余额不足时按钮置灰并显示「（需要 N 资源点）」。执行侧另有一层下限 0 的兜底。

**悬浮预览**：选项若含 `replaceCard` / `replaceRandomCard`，鼠标悬浮时会在按钮上方显示将被加入的卡（`bin/event_card_preview.tscn`，**只画卡，无底板、无边框、无文字**）。同名卡去重后最多显示 2 张——实测一个选项最多只有 2 种不同的卡；`[snowstorm]` 的第 3 个选项有 20 张同名「埋伏」，去重后就只是一张。

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

#### `[sfx]` 段：音效槽位

与`[music]`**同格式**（逗号分隔、随机抽一条），区别只在语义：音效没有「播完再切」的调度，**每次播放各抽一条**。代码走`MusicManager.PickSfx(slot)`，返回`AudioStream`（带缓存，不重复读盘），调用方自己赋值给`AudioStreamPlayer.Stream`再播。

- `dead`：单位阵亡时的爆炸音效。素材范围是`assest/爆炸3.wav`～`爆炸21.wav`，**下划线开头的未采用版本不列入**（`_爆炸16`/`_爆炸17`/`_爆炸19`）
- `flyby`：`flying` 特效的飞掠音效（`assest/飞机飞过_单位.wav`）。调用方是特效自己（`effects/flying_effect.tscn` 的 `SfxSlot`），不经过 `battlefield_`。`airstrike` 继承`FlyingEffect`，所以起飞时也响这一声

调用方：`battlefield_.PlayDeadSound()`，槽位名常量是`DeadSfxSlot`。抽不到（槽位没配/加载失败）时保留场景里原有的那条，不会变成没声音。

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
