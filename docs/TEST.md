# 测试设计

## 对白系统

测试脚本：`tests/verify_game_dialogue.py`。

### 冒烟测试

- Dialogue Manager与`GameDialogue`均已注册autoload。
- 自定义气泡场景已配置并位于高层CanvasLayer。
- 项目可以通过.NET构建。

### 基本验证

- 同时提供等待式`PlayAsync`和非等待式`Play`。
- 示例对白包含地图、战斗和缺失立绘回退标题。
- happy、angry、normal素材存在并可被场景引用。

### 边界白盒测试

- 开始菜单被唯一排除。
- 并发播放请求串行等待。
- 全局静态事件在节点退出时解除订阅。
- sad素材缺失时回退normal立绘。
- 对白和气泡资源缺失时记录错误并返回失败，不阻塞调用流程。

## 性能优化批次A/B

测试脚本：`tests/verify_performance_batches_ab.py`。

- 死亡检查使用门闩和循环，不存在未等待递归。
- change list、Dead效果、Trait结算和抽牌保持串行等待。
- 回合入口拒绝重复触发并在`finally`恢复状态。
- 箭头绘制已恢复原始实现，专项测试只验证原始函数存在，不再约束批次B箭头优化。
- 卡牌和商店移动会取消旧Tween。
- 商店主题颜色只在状态变化时写入。
- Tooltip使用局部GUI输入且只在图标变化时重算文本尺寸。

## 伏击特性

测试脚本：`tests/verify_ambush.py`。

- 伏击受攻击方兵种限制：火炮/轰炸机攻击时不触发伏击，与普通反击共用同一条 `receivesCounterAttack` 判断。
- 伏击在攻击伤害之前结算；击杀攻击方时跳过攻击伤害。
- 普通反击仅在未触发伏击时执行。
- 冲击消耗在伏击判定之前，绕过伏击且不消耗目标伏击状态。

## 按钮动态效果

测试脚本：`tests/verify_button_animations.py`。

- `worldMap.tscn`中的`deck`按钮连接到`_on_deck_pressed`。
- `WorldMap.cs`不再动态创建卡组按钮。
- WorldMap的`store`与`deck`按钮使用统一hover缩放Tween。
- ChooseMission、Store、EventScene、PostBattleReward、DisplayCard中的按钮使用统一1.08倍/0.12秒hover缩放。 
- 所有按钮hover缩放前都会把`PivotOffset`设为控件中心，避免围绕锚点缩放。

## 全局背景音乐

测试脚本：`tests/verify_music_manager.py`。

- 冒烟测试：`MusicManager`已注册为autoload并暴露`Instance`、`PlaySlot`、`StopMusic`、`SetVolumeDb`；`configs/music.ini`含`start_menu`、`world_map`、`battle`三个基础槽位。
- 基本验证（一槽多曲）：槽位值以`string[]`存储，逗号分隔且忽略空项与两侧空格；随机源为常驻`Random`实例。`PickPath`在单曲时直接返回、多曲时随机抽取，抽中正在播放的那首时用非零随机偏移绕开。
- 基本验证（战斗专属BGM）：`battleBGM_`前缀与`battle`回退槽位为具名常量；`PlayBattleSlot`在专属槽位未配置时回退；`battlefield_`以`BattleStateManager.ResolveEnemyPreset()`请求BGM且不再写死`PlaySlot("battle")`；`berlin`字面量只在`DefaultEnemyPreset`常量定义处出现一次。
- 基本验证（播完再切）：`_Ready`订阅`AudioStreamPlayer.Finished`；`PlaySlot`体内**不得出现任何`player.Play()`**，换曲时机完全交给曲末回调；不同槽位只记入`pendingSlot`；当前无曲目在播时立即起播；切回正在播放的槽位时撤销排队。曲末回调优先切到排队槽位、无排队则留在当前槽位续播，并消费掉队列。
- 基本验证（内建循环）：`DisableBuiltinLoop`对MP3/OGG/WAV三种格式关闭内建循环并在起播前调用；源码中不得再出现任何开启内建循环的写法——内建循环下曲目永不结束，`Finished`不触发，「播完再切」就无从实现。
- 边界白盒测试：槽位名忽略大小写；空值槽位不进入槽位表；换槽位抽到同一首不重播；资源加载失败记录带时间与代码位置的警告并退回当前槽位续播（退回带槽位相等判断，递归深度最多两层）；播放日志含时间与代码位置；`StopMusic`一并清空排队。
- 配置陷阱：`music.ini`不得出现含`=`的`#`注释行——`bin/iniHandler.cs`只把`;`当注释，此类行会成为垃圾槽位；实际生效的槽位引用的音频资源必须存在于磁盘。
- 回归验证：`StartMenu`、`WorldMap`仍请求各自槽位；三个基础槽位指向同一首以保证跨场景不重播；`battleBGM_`由`[music]`段通用键解析自动产生，`LoadConfig`中无专用分支。
- `Store`、`ChooseMission`、`EventScene`和`PostBattleReward`不主动切歌，继承当前音乐。

## 音量设置

测试脚本：`tests/verify_volume_settings.py`。

- 设置配置包含主音量、配乐音量、效果音量和UI音量及可配置范围。
- 设置界面按配置动态创建数值滑块，并将用户值保存到`user://settings.cfg`。
- `Master`、`Music`、`SFX`和`UI` Audio Bus分类存在。
- 背景音乐使用`Music`，当前战斗及卡牌音效使用`SFX`。

## 卡牌视觉效果

测试脚本：`tests/verify_card_effects.py`。

- `CardData`和运行时卡牌支持可选`playEffect`与`attackEffect`。
- 两条卡牌配置加载路径都安全读取可选字段。
- `Effect`提供位置列表和可选时间参数，`EffectRegistry`负责短名称映射。
- 原有子弹攻击表现由`BulletEffect`管理，`Bullet`成为`Effect`子类。
- 出牌、主动攻击、反击和伏击路径调用对应视觉效果。
- 现有非指令卡配置`attackEffect=bullet`，未配置字段的卡牌不播放效果。
- `smoke`效果正确注册并切分4×4图集，单位实际死亡时在移除前快照的中心位置播放。
- 烟雾帧序列与透明度由单个Tween驱动，不再逐帧创建Timer。
- 战斗初始化预加载并预渲染Effect资源，Bullet与Smoke播放后回到战场级对象池。
- Bullet相关随机偏移与发射间隔使用共享随机数生成器。

## 音量、机动防御与奖励界面回归

测试脚本：`tests/verify_reward_and_t70_fixes.py`及`tests/verify_volume_settings.py`。

- 音量系统在应用设置前确保`Music`、`SFX`、`UI` Bus存在，并在重复初始化时重新应用用户音量。
- 机动防御生成的T-70跳过生成当回合的结束时点，在下个友方回合结束时消灭。
- 卡牌奖励界面使用原生尺寸卡牌，三组奖励位于垂直滚动区域，跳过按钮固定可用。

## 友方总部失败流程

测试脚本：`tests/verify_hq_defeat_flow.py`。

- 友方总部被消灭后只启动一次失败流程。
- End界面显示暗幕、灰色国徽、“战斗失败”和“返回主菜单”按钮。
- 玩家点击后直接进入开始菜单，不结算物资、完成区域或显示卡牌奖励。

## Attribute悬浮提示

测试脚本：`tests/verify_attribute_tooltips.py`。

- Attribute图标通过不消费事件的全局鼠标移动检测显示说明，不依赖可能被遮挡的GUI路由。
- 面板重建、指令卡复用和隐藏对象池卡牌会清理旧悬浮状态。

## 商店卡牌显示状态

测试脚本：`tests/verify_store_card_presentation.py`。

- 商店复用同一卡牌节点时，单位、指令和总部显示状态都由当前CardData重新赋值。
- 指令卡刷新为`su76m`等单位卡后会恢复普通卡框、攻防标签和单位UI。

## 敌人意图图标

测试脚本：`tests/verify_enemy_intent_icons.py`。

- 意图面板按每条行动的`icon`元数据加载`normalUnit`、`bigUnit`、`heal`、`damage`或`upgrade`。
- 图标缺失或名称未知时回退到`boss`，不再为所有行动硬编码同一图标。

## 战斗名称配置

测试脚本：`tests/verify_battle_names_in_ini.py`。

- 敌人战斗section各自包含`name`，任务界面直接读取该名称。
- `name`不会进入敌人行动队列，缺失时记录错误并回退显示section ID。
- 旧`HistoricalBattleNames.cs`和未使用的`EnemyDisplayNames`字典已删除。

## 战役内容一致性

测试脚本：`tests/verify_campaign_content.py`。

只验证引擎层面的事实约束，不约束关卡的设计风格与难度取舍。

- 冒烟测试：`event.ini`、`enemyTurn.ini`、`AreaPool.ini` 可解析且非空。
- 引用完整性：区域池的`enemy*`引用可解析到关卡section，`entry*`引用可解析到事件section，关卡内`addToEnemySupportLine()`引用的卡牌ID存在。
- 元数据完整性：每个关卡有`name`，每条行动都带`[icon=...,description=...]`；意图面板按整行读取一个icon，因此以行为单位检查。
- 部署描述用名：凡`addToEnemySupportLine(id)`，该行描述必须出现`id`在`card.ini`中的`name`（如`de_panzer4`必须写「四号坦克」），避免描述与部署单位对不上。
- 键格式：只允许`name`、`tN`、`everyNt`、`default`、`ADD`；永久前缀写成`ADD=`（等号）时不会被解释器识别，测试将其判为失效。
- 区域一致性：`AreaPool.ini`的section与`CardRestoration.cs`的`AreaOrder`严格为area1-area7。
- 孤立内容：未被任何区域引用的关卡以`[INFO]`列出，不计失败。

`tests/verify_enemy_cards.py`的`test5`只断言预设非空，不强制每关都有脚本化处决回合；没有处决回合的关卡以`[INFO]`列出。

## 攻击与死亡动画时序

测试脚本：`tests/verify_attack_death_timing.py`。

- 主动攻击、反击或伏击中首个实际启动的攻击Effect建立500ms展示窗口。
- 战斗死亡检查在该窗口结束后执行，再移除单位并播放smoke；非战斗死亡不增加延迟。

## 区域解锁进度

测试脚本：`tests/verify_area_unlock_progression.py`。

- `UnlockedArea`由`BattleStateManager`跨场景保存，初始仅`area1=1`。
- 战斗或事件完成后将当前区域设为0、下一区域设为1，区域进度只使用`UnlockedArea`。

## 剧情事件资源点

测试脚本：`tests/verify_event_material_points.py`。

- 冒烟测试：事件配置至少存在一个合法的 `materialPoints(n)` 效果。
- 基本验证：效果写入 `BattleStateManager.MaterialPoints` 并即时刷新世界地图 `pointNum`。
- 边界白盒测试：拒绝负数、非整数和超出 `int` 范围的参数，记录带时间和代码位置的错误；累计结果限制在 `int.MaxValue`。

## 世界地图区域

测试脚本：`tests/verify_world_map_areas.py`。

- 冒烟测试：`worldMap.tscn`、`AreaPool.ini`、`CardRestoration.cs` 均存在，且区域按钮、区域段、区域顺序、解锁状态可解析。
- 基本验证：按钮名、区域池段名、`AreaOrder`、`UnlockedArea` 四者严格为 `area1`～`area7`，且每个按钮都有同名区域池段。
- 边界白盒测试：初始仅 `area1` 解锁；每个区域至少有一个可抽取任务；按钮坐标完整、宽高为正且落在画布内；旧区域 `area8`～`area10` 无残留引用。
- 地理校验：按钮按由西到东排序为柏林、明斯克、斯摩棱斯克、库尔斯克、哈尔科夫、莫斯科、斯大林格勒；莫斯科位于斯大林格勒与哈尔科夫以北。

## 整局进度重置

测试脚本：`tests/verify_campaign_reset.py`。

- 冒烟测试：`BattleStateManager` 提供 `ResetCampaignProgress()`。
- 基本验证：重置清空持久化卡组 ID 与卡牌节点引用、复位 `IsDeckInitialized`、恢复仅 `AreaOrder[0]` 解锁、清空区域烈度与商店库存、清零物资点与上局战斗统计。
- 边界白盒测试：战败与通关两个整局结束入口都调用重置；`SettingsMenu` 返回主菜单不触发重置；`card.ini`/`event.ini`/`AreaPool.ini` 的内容缓存不被重置清掉。

## 区域战斗烈度

测试脚本：`tests/verify_area_intensity.py`。

- 冒烟测试：区域池、区域状态、任务面板场景、通关浮层与 `campaign_victory.dialogue` 均存在且可解析。
- 基本验证：`areaTimes` 被识别为元数据而不进入任务池；进入区域时按 `areaTimes` 初始化烈度；归零时才调用 `AdvanceArea()` 解锁下一区域。
- 边界白盒测试：缺失时回退默认值 3；非法值记录错误并回退；`EnsureAreaIntensity()` 不重置已进入过的区域；`IsFinalArea()` 以 `AreaOrder` 末项判定；通关浮层层级低于对白气泡。
- 接入验证：任务面板文本为「战斗烈度：当前值」；战斗与事件两条路径都消耗烈度且不再直接调用 `AdvanceArea()`；最后一个区域归零时进入通关流程并返回开始菜单。

## 阵亡统计

测试脚本：`tests/verify_friendly_death_count.py`。

阵亡/战果计数原先写在 `RemoveCard()` 内，而该函数是通用的卡牌移除函数——弃牌、指令卡结算、`ShowCardChoice` 清理选项卡、手牌溢出都会调用它，计数又没有 state 判断，导致非阵亡的移除被一并算作阵亡，并连带扣减 `CalculateMaterialPoints` 的物资点。现计数已移至 `ProcessDeadUnitAsync`。

- 冒烟测试：可从 `battlefield_.cs` 提取 `RemoveCard` 与 `ProcessDeadUnitAsync` 两个方法体。
- 基本验证：`RemoveCard` 不含任何阵亡/战果计数；`ProcessDeadUnitAsync` 含全部三个计数。
- 边界白盒测试：计数受 `deadUnit.isHq != HQ.hq` 守卫，总部不计入；友方分支保留显式的 `IsFriend.friend` 判断，不能写成裸 `else`（`IsFriend` 另有 `neutral` 与 `enemyNeutral`）。
- 回归验证：`CardDiscardAndRemove` 与 `ShowCardChoice` 仍会调用 `RemoveCard`，即该约束确有防范对象；`RemoveCard` 仍负责从 `cardInPlaces` 移除节点。

## 战斗结算面板

测试脚本：`tests/verify_settlement_panel.py`。

结算面板原先用代码绘制（`End.cs` 的 `ShowSettlement`），并且把物资点的评分系数又写了一遍——陆军 `*4`、空军 `*5`、总部 `/3`——而 `CalculateMaterialPoints()` 里早已改成 `*3`、`*4`、`/2`。结果是面板四行明细相加不等于底部显示的总额。现在系数集中在 `bin/BattleScore.cs`，面板搬到 `bin/settlement_panel.tscn`。

- 冒烟测试：场景文件存在且可解析出所需节点。
- 基本验证：`battleField.tscn` 以实例方式把面板挂在 `end` 节点下；`End.cs` 按名引用全部节点；两个面板默认隐藏。
- 回归验证：`End.cs` 与 `battlefield_.cs` 都不得出现评分系数的原始算术（`landKilled *`、`_battleEnemyLandKilled *`、`hqLost /` 等），必须调用 `BattleScore`。
- 边界白盒测试：三个系数以具名常量定义在 `BattleScore.cs`，且不在别处重复定义；手写场景不写 uid（沿用 `settings_menu.tscn` 等先例，避免与现有资源撞车）。
- 输入可达性：面板必须在 `_Ready` 中被移到全屏遮罩之后——Godot 的 GUI 拾取按树序、后加入者优先，不读 `z_index`，遮罩是 `MouseFilter.Stop` 且在 `_Ready` 中才 `AddChild`，面板排在它之前时按钮收不到点击。
- 对敌方总部的伤害：`LoseDefence` 是唯一累加点，`ReadTotalDefenceLost` 供结算读取；`CalculateMaterialPoints` 读取后写入 `LastBattleEnemyHqDamage`，整局重置时清零。该项存在的原因是击杀曾是唯一得分来源，「敌方不派兵、只加固总部」的关卡（加里宁）必然结算为 0。

`tests/verify_hq_defeat_flow.py` 中「失败面板文案」的断言已改为读 `bin/settlement_panel.tscn`——文案移入场景后，C# 里不再有 `Text = "战斗失败"` 这类字面量。
