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
- **一行多行动各自成行**：渲染时按顶层逗号拆开行动行（复用`SplitEffectString`），每个带`description`的段各出一行、各用各的图标。旧实现在整行上做`LastIndexOf('[')`，只认最后一个元数据块——布良斯克`t1`的「部署第1步兵团」就是这样被静默吞掉的，界面上看不出敌人还部署了单位。断言覆盖：渲染路径不得再出现`LastIndexOf`；对`enemyTurn.ini`全部行动行做行为验证，确认没有任何描述被丢掉、且单块行不会重复出行。
- 面板高度固定 300×400（约 5 行可见），而`ADD:`队列会随回合无限累积——`MamayevKurgan`与`berlin_final_battle`的峰值分别达到 13 行与 11 行，超出部分原本会画到面板底色之外、叠在战场上。行动列表现挂在`ScrollContainer`下（横向滚动禁用、列表`SizeFlagsHorizontal=ExpandFill`撑满宽度、列表`MouseFilter=Ignore`以保证滚轮能传到滚动容器），超出部分收进面板内纵向滚动。
- **纵向不显示滚动条**：纵向 `ScrollMode` 取 `ShowNever`，不是 `Disabled`。两者都能让滚动条消失，但 `Disabled` 会**连滚动一并禁掉**，高行数关卡的行会重新画到面板外——所以测试除了断言取值为 `ShowNever`，还额外断言纵向**不得**为 `Disabled`。`ShowNever` 保留滚轮滚动，同时收回滚动条占去的约 12px 宽度给 220px 的描述文本。

## 卡牌效果脚本静态校验

测试脚本：`tests/verify_card_scripts.py`。

专查「写错了也不报错、只是静默不生效」的三类问题：

- **选择器语法**：`setTargets` 的正则是 `\$\{([^}]*)\}`，**只认 `${...}`**。写成 `$(...)` 或裸 `$xxx` 时正则不匹配，`targets` 不被赋值，后续指令遍历空列表——整张卡毫无效果且不报错。`[第227号命令]`（`setTargets($allTargets.unit.friend.damaged)`）与`[血洒长空]`（`$allTargets.unit.friend.air`）都栽在这里，现均已补上花括号。断言覆盖三个 INI 文件：不得出现裸 `$xxx`、不得出现 `$(...)`，且确有 `${...}` 在使用（规则不是空转）。
- **选择器片段**：`GetTargetsFromSelector` 对不认识的片段走`ParseCardTypeFromName`，返回null时**静默忽略**，过滤条件凭空消失。断言 `${...}` 内每个片段都属关键字集合或卡牌类型名。
- **跳转标签**：`if(条件)标签&` 在`labels`中找不到`标签`时**不跳转**，条件形同虚设、效果体无条件执行。断言每处跳转都有对应标签。

解析按`battlefield_.cs`的`ParseAndExecuteEffect`/`GetTargetsFromSelector`复刻，`SplitEffectString`需同时跟踪**引号**、圆括号与方括号；去时点前缀时**只认引号外的第一个冒号**——否则`GetEffect("FriendlyTurnBegin: ...")`这类写法会被截错位置，误报标签缺失（校验脚本自身踩过这个坑）。

## 总部效果指令

测试脚本：`tests/verify_hq_instructions.py`。

HQ 的「血」是`defence`，`attack`恒为 0 且总部不会攻击，因此对 HQ 施加增减攻击力的指令没有任何可观察效果。步兵第845团原写作`TakingDamage: myHq|GetAttack(&lastDamage)`，卡面描述却是「受到伤害时友方总部恢复等量的防御力」——加的是攻击力，总部防御力纹丝不动，表现为「无法给总部加血」。现改为`Heal`（实现为`ChangeType.GetDefence`）。

- 冒烟测试：`card.ini`与`enemyTurn.ini`可解析且非空。
- 基本验证：全库不得有任何效果对 HQ 使用`GetAttack`/`LoseAttack`；总部指令确实存在（56 处），且使用了`heal`/`damage`/`addDefence`/`SetDefence`等防御类写法。
- 定点回归：步兵第845团对总部使用`Heal(&lastDamage)`，卡面描述仍为「恢复等量的防御力」，两者一致。
- 触发链完整性：`lastDamage`的赋值（`battlefield_.cs`的`Attack()`）必须早于`TakingDamage`的触发，否则`&lastDamage`取不到数值；该时点只在`attackDamage > 0`时触发；`myhq`解析为玩家总部。
- 边界：规则不得过宽——对**友方单位**使用`GetAttack`仍被允许（`GetAllFriendUnits|GetAttack(...)`）。

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

## 起手换牌

测试脚本：`tests/verify_mulligan.py`。

开局抽 `battlefield_.OpeningHandSize`（5）张，平铺在屏幕前供玩家换牌，随后进入游戏。规则参考炉石/Kards：可换任意张（一张不换或全换都行），只有一次换牌机会。

- 冒烟测试：`bin/mulligan_screen.tscn`与`bin/MulliganScreen.cs`存在；开局抽牌数收敛为具名常量而非写死的`DrawCard(5)`；抽完起手牌后进入换牌界面。
- 平铺与点选：起手牌被reparent进覆盖层接管显示；`SlotPosition()`按序号计算落位，并补偿了「以中心为轴缩放」带来的偏移；点击是开关式且**没有张数上限**。
- X 标记：选中显示、取消隐藏；标记不拦截鼠标（点击由独立点击层处理）。assest下没有叉号素材，暂用`dead.png`占位。
- 换牌与补抽：`Player.MulliganAsync()`把选中的牌移出并加回牌库，**先整副洗牌再补抽**（炉石做法，理论上可能抽回刚换掉的牌）；新牌补进被换掉的牌原来的位置并飞入展示，随后停留`RevealHoldSeconds`让玩家看清。
- 一次收束：确认后禁用按钮；一张不选时不做任何换牌直接开局。
- 关键回归（卡牌所有权）：结束时必须把牌还给战场，否则会随覆盖层一起被销毁；被换掉的牌已进牌库、不在手牌里，需要单独还回；本界面加到卡牌上的子节点用`_overlays`显式记录后逐个释放——**不能按类型遍历卡牌子节点删除，卡牌自身的美术资源也是`TextureRect`**。
- 关键回归（手牌刷新）：`RefreshMyHand()`必须跳过已reparent的卡。补抽会触发该方法，若不跳过，屏幕上正在展示的起手牌会被拉回手牌区。该守卫与`battlefield_.cs`中既有的「跳过已临时Reparent到其他节点的卡」写法一致。
- 关键回归（显示顺序刷新）：`RefreshAllCardDisplayOrder()`由`_Process`每帧调用，其中的**手牌循环原先没有父子守卫**——`cardInPlaces`循环有、手牌循环没有。换牌期间手牌仍在`cardsInHand`里但父节点已变成覆盖层，于是每帧报`Child is not a child of this node`（`scene/main/node.cpp:487 move_child`）。已补上同样的守卫，并加了两条断言：手牌循环在`MoveChild`之前必须有`GetParent()`校验；`battlefield_.cs`中**每一处**`MoveChild`之前都必须先确认父子关系（撤掉守卫会立即报出违规行号）。
- 关键回归（卡牌可见性）：牌库中的卡靠**停在屏幕外**（`Player.DeckParkPosition`）隐藏，**不能靠`Visible = false`**——`battlefield_.cs`中没有任何路径会把它恢复，一旦设上就永久生效。换牌界面原先正是这么做的，导致被换掉、当时又没被重新抽到的牌日后抽到手上**只是一个空位**：能抽到、不报错、但看不见。现在换牌界面停到屏幕外语的牌库位置，`DrawCard()`另加`card.Visible = true`兜底。断言覆盖：`(-2000, 800)` 只在常量定义处出现一次、抽牌路径必须恢复可见、换牌界面不得出现`Visible = false`。
- 健壮性：换牌期间`ForbidControl()`锁住战场操作，并用`finally`解锁，流程出异常也不会把玩家卡死；点击层延迟启用，避免打开界面那一次点击被立刻吃掉。

## 任务抽取规则

测试脚本：`tests/verify_mission_draw.py`。

抽取规则集中在`bin/MissionDrawer.cs`（`WorldMap`原先的`PickRandomEntries`已移入并替换，避免同一功能两处实现）。

- 冒烟测试：`MissionDrawer.cs`存在；`AreaPool.ini`可解析且至少一个区域配了`boss`；`WorldMap`调用`MissionDrawer.Draw(candidates, pool.ReadBoss(), intensity)`。
- 规则一（boss 按烈度分流）：`Draw`接收 boss 与烈度；烈度为 1 时只返回 boss 一条；烈度不为 1 时 boss 被排除出战斗池；`AreaPool.ini`的`boss`键被识别为区域元数据而不进入可抽取条目；`Area`提供`ReadBoss()`，未配置时为空串。
- 规则二（事件脱敏）：定义常量`EventMaskLabel = "<事件>"`；事件条目一律使用该文案；不再把`event.ini`的`title`直接当按钮文字；事件配置缺失时仍记录错误但不影响脱敏。
- 规则三（2战斗+1事件）：`BattleCount = 2`、`EventCount = 1`为具名常量；事件按常量上限截取；兜底补位只从剩余战斗取。
- 数据层验证：用真实的`bin/AreaPool.ini`把`Draw`的行为复算一遍，每个区域 × 烈度 3/2/1 各跑 200 次，断言事件数恒不超过 1、烈度不为 1 时抽不到 boss、烈度为 1 时固定只有 boss。
- 降级规则：按钮数**不超过池中实际可用条目数**（不会凭空造条目）；战斗充足（≥2 场）时恒定凑满 3 个按钮。这两条写成与具体配置无关的形式，避免内容调整后断言过时。
- 当前实际产出：`area1`在烈度 1 下固定只有`Kalinin`，烈度 2/3 下恒为 3 个按钮且恰好 1 个事件；`area1`/`area2`为 3 个按钮；**`area3`~`area6` 只有事件、没有战斗，因补位只用战斗而降级为 1 个按钮**；`area7`为 2 个按钮（1 战斗 + 1 事件）。给这些区域补上战斗后按钮数会自动回到 3。

> 数据层验证是在 Python 中复算同一算法，能证明真实配置下的产出符合规则，但不能替代对 C# 实现的运行验证。

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

## 拖拽期间右键导致卡牌卡在场上的回归

测试脚本：`tests/verify_drag_right_click.py`。

`battlefield_.cs` 的 `_Input` 把 `InputEventMouseButton` 拆成「按下」「抬起」两个兄弟分支，两条原本都没校验 `ButtonIndex`，右键因此会完整走一遍左键流程：右键**按下**时 `CheckCardClick` 返回正被拖起的卡，其状态是 `caught`，不匹配 `inHand` / `placed` 任何分支，于是落到 `else` 把 `currentInputState` 冲成 `nil`（原 `P_InHandUnit` 丢失）；右键**抬起**时 `switch (InputState.nil)` 无匹配分支，落位/归位逻辑整段跳过；此后松开左键依然走 `nil`，卡牌永久停在 `caught`——而 `RefreshMyHand()` 对 `isDragging` 的卡 `continue` 跳过（"拖动状态下，不让自动布局移动该卡"），不会把它拉回手牌位，于是**卡在场上**。附带伤害是 `cardNowChoose` 始终非 null，`_Process` 因此对全部手牌关闭悬停（`UpdateHover(-9999,-9999)`）。场上单位拖拽（`P_InPlaceUnit` / `inplaceAndCaught`）走同一段代码，同样会卡住。

右键在本项目中无任何既定功能（全项目零处 `MouseButton.Right`），故修法是给两条分支都加左键限定，拖拽期间彻底忽略右键——不新增取消逻辑、不动状态机。

- 冒烟测试：`bin/battlefield_.cs` 存在且含 `_Input` 入口。
- 基本验证：以拖拽分支内部的语句为锚点（按下分支用 `var card = CheckCardClick(mousePosition);`，抬起分支用 `// 没有卡牌被拖动，不处理`）向前回溯，各自最近的 `mouseButton.Pressed` 判断必须带 `ButtonIndex == MouseButton.Left`。**不用字符串计数**——选择界面守卫（`isShowingChoiceUI`）与拖拽按下分支的守卫文本完全相同，计数会把 2 误判成 1。
- 回归验证：不得残留裸 `if (mouseButton.Pressed)` 与 `if (mouseButton.Pressed==false)`。
- 边界白盒测试：全项目扫描每个 `InputEventMouseButton` 处理点，凡测试 `.Pressed` 的行都必须提到 `ButtonIndex`；只处理滚轮的 `DisplayCard.cs` 豁免，且断言它确实同时含 `WheelUp` 与 `WheelDown`（豁免名单不是空转）。
- 前提校验：全项目零处 `MouseButton.Right`——这是「忽略右键不会破坏任何功能」的依据。
- 成因链校验：断言 `CardState.caught`、`cardNowChoose` 与 `RefreshMyHand` 中的 `isDragging → continue` 仍然存在，证明本测试守的是真实风险。
- 断言有效性：把两处守卫还原成 `if (mouseButton.Pressed)` / `if (mouseButton.Pressed==false)` 后，脚本立即报出 5 个 FAIL（两处锚点断言 + 全项目扫描 + 两条回归断言）。

## Next 按钮在敌方回合闪烁的回归

测试脚本：`tests/verify_control_lock_nesting.py`。

`ForbidControl()` / `AllowControl()` 原是一对**扁平标志**（`allowControl` 只是个 `int`，没有计数），但调用点是**嵌套**的：`Attack()` 结尾无条件调用 `AllowControl()`，而敌方回合里每一次 `Attack` 都嵌套在 `EnemyTurnAsync` 与 `OnNextTurnButtonPressed` 的禁止之下。最内层的解锁直接推翻外层的禁止，于是每执行完一次敌方行动按钮就被置回 Enabled、下一次行动又立刻禁用——**敌方行动几次就闪几次**。闪烁期间按钮看起来可点，功能上被 `turnTransitionRunning` 挡住，属纯视觉问题。

修法：改为**嵌套感知**的控制锁，按 `controlLockDepth` 计数，只有最外层 `AllowControl()` 才真正解锁。`Attack()` 结尾那句 `AllowControl()` 必须保留（我方回合单独攻击要靠它解锁），由计数而非删除来保护。所有既有调用点一行未改。

- 冒烟测试：两个函数存在；`controlLockDepth` 字段已引入。
- 基本验证：`ForbidControl` 自增计数；`AllowControl` 递减，且计数仍大于 0 时提前返回；嵌套判断必须位于解锁语句之前（顺序颠倒会先解锁再判断）。
- 行为模拟（核心）：用 Python 复刻计数器语义，按敌方回合的真实嵌套顺序（2 层 Forbid + 3 组「Forbid/Allow」攻击对 + 2 层收尾 Allow）走一遍，断言按钮在整个敌方回合期间**一次都没有**变成 Enabled，且回合真正结束时才恢复可点击。
- 对照实验：用旧语义跑同一序列，断言它**确实会闪烁 4 次**——证明该断言不是空转。
- 边界白盒测试：我方回合单独一次攻击（深度 1→0）照常解锁，原有效果未变；计数为 0 时未配对的 `AllowControl()` 行为与从前一致；多次未配对调用不会让计数变负或卡死；深度 2 时内层一次解除后仍保持禁用。
- 调用点审查：`Attack()` 只在开头 `ForbidControl` 一次，并以 `AllowControl()` 作为最后一条语句收尾。
- 断言有效性：把实现还原成扁平标志后立即报出 3 个 FAIL。
