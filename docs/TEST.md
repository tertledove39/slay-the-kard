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

## 效果脚本时点前缀合法性

测试脚本：`tests/verify_timing_prefixes.py`。

`TriggerUnitEffects` 判定时点用的是 `if (prefix != triggerPoint) continue;`——**区分大小写的精确比较**。前缀写错时不报任何错，只是该段效果永远不执行。`[i1005]`「步兵第1005团」的亡记就是这么失效的：写成 `dead:`，而死亡触发点传的是 `Dead:`（`battlefield_.cs` 的死亡分支），`"dead" != "Dead"` 直接 continue，效果一次都没跑过。全量扫描发现同一 bug 还波及 `[雅克9]`（`dead:setResult(1)|DrawCard|`）。

注意 `Times` 枚举里的成员名是小写（`dead`、`played`…），但它**全项目零处使用**，不构成命名依据；权威写法是 `LOGIC.md`「时点系统」表里的 PascalCase。修法是改数据（2 处）而非把代码改成忽略大小写——后者会让 `DEAD`、`dEaD` 之类笔误静默生效，正是本项目一直在消灭的失效模式。

- 校验**两个来源**：① `effect = ...` 行的时点前缀（57 段）；② `GetEffect("...")` **内嵌串**里的时点前缀（11 段）。内嵌串会被赋给目标卡，之后由 `TriggerUnitEffects` 按同样规则解析，所以写错一样静默失效——漏掉这条路径会让测试留出盲区，实际上内嵌串里的 11 个前缀当时恰好全都正确，属于运气好。
- 冒烟测试：从代码中抽出 `TriggerUnitEffects("...")` 的触发点集合（26 个，且确含 `Dead`）；三个效果配置文件均存在；两个来源都能抽出带时点前缀的效果段。
- 基本验证：配置里每个纯字母时点前缀都必须**精确命中**触发点集合；大小写不符与名字未知分别报错，并打印 `文件:行号 [section] '写法' 应为 '正确写法'`。
- 前缀提取复刻 `TriggerUnitEffects` 的做法：先用 `SplitEffectString` 按顶层逗号切段（引号 / `()` / `[]` 内的逗号不生效），再取**第一个冒号之前**的部分，且**不剥离** `[]` 元数据。形如 `addToSupportLine(x)[icon=..,description=..]` 的段本就不是时点触发，取出的"前缀"不是纯字母标识符，脚本会跳过。
- 定点回归：`[i1005]` 与 `[雅克9]` 的 `effect` 必须以 `Dead:` 开头，且不得残留小写时点前缀。
- 边界检查：确认配置确实使用了多种时点前缀（15 种）且 `Dead` / `Deployed` 在受检范围内，规则不是空转。
- 断言有效性：把两处数据改回 `dead:` 后立即报出 5 个 FAIL，并准确指出 `card.ini:1341 [i1005]` 与 `card.ini:1543 [雅克9]`。

## 卡牌复制（Develop 用）

测试脚本：`tests/verify_card_copy.py`。

`Develop($选择器)` 的候选来自牌堆/手牌/场上的真实对象，不能改它们、也不能把原对象
同时塞进两个容器，所以三个分支统一先过 `Copy(cardBase_)`。`Copy` 必须做到两件事，
两件写错都**静默失效**：数值搬不全（按 id 重读会把运行时改过的值丢掉）、尺寸没钉死
（`Reparent` 到 `choiceLayer` 后 `GetGlobalRect()` 的命中框跟着变，卡就点不中了）。

- 冒烟测试：两个源文件存在；`Copy(cardBase_)` 可定位。
- 基本验证：`cost`/`attack`/`defence`/`effect` 四项都按 `copy.X = source.X` 搬运；
  `traits` 走 `AddTrait(source.traits)` 而非直接写字段（直接写会让 `hasAmbushActive`
  等运行时标志缺失，伏击静默失效，故专门加一条反向断言禁止 `copy.traits = source.traits`）。
- **回归（针对「开发出来的卡点不中」）**：`cardBase_` 暴露 `PinDesignSize()`，其内部钉
  `TopLeft` 锚点 + `DesignSize`；`Copy()` 里确实调了它；`InitializeDeckFromIni` 的两条
  分支（持久化重建 / 首次读 `deck.ini`）都调了它。
- 边界/配置项单一：`new Vector2(180, 240)` 在两个源文件里**只允许出现一次**，且必须在
  `cardBase_.cs`——钉死并断言这一点，防止尺寸又被抄成第二处配置。
- 显示同步：`Copy()` 末尾调了 `RefreshState()`（直接写字段绕开了 `SetCostValue` 等
  setter 的刷新副作用，三个 Label 需自己补）。
- 断言有效性：删掉 `copy.PinDesignSize();` 后立即报 FAIL。

## 选择界面的模态性（控制锁不得吃掉它的点击）

测试脚本：`tests/verify_choice_ui_modal.py`。

`_Input` 里原本把 `if (ReadControlState() == 1) return;` 写在选择界面判定**之前**。
平时没事——从手牌打出的指令卡（`[紧急投产]` 的 `Develop($deck)`）不在锁里。但时点
触发的 Develop 嵌在回合切换的锁内部：

```
OnNextTurnButtonPressed → ForbidControl → RunTurnTransitionAsync → FriendlyTurnBegin
```

`[步兵第190团]` 的 effect 正是 `FriendlyTurnBegin:Develop($deck)`。此时选择界面已经
弹出、正等玩家点卡，可 `allowControl == 1`，点击全被控制锁那行吃掉，
`HandleChoiceCardClick` 永远收不到事件——卡看得见、却一张都点不动，且不报任何错。

- 冒烟测试：源文件与卡表均存在；`_Input` 与 `_Process` 可定位。
- **回归（核心）**：在 `_Input` 体内，`if (isShowingChoiceUI` 的下标必须**小于**
  `if (ReadControlState() == 1) return;` 的下标。
- 单一实现：`_Input` 里 `HandleChoiceCardClick(` 只出现一次（不许为了绕过锁而在两处
  各写一个分支）。
- 前提校验（防空转断言）：`[i190]` 存在且 `name = 步兵第190团`，其 effect 以
  `FriendlyTurnBegin:` 开头并含 `Develop($deck)`；`OnNextTurnButtonPressed` 确实把
  `RunTurnTransitionAsync()` 包在 `ForbidControl()` 里、且要到它返回后才 `AllowControl()`。
- 对照组：`[紧急投产]` 的 effect 无时点前缀（从手牌打出），与「它一直正常」的实机
  反馈一致——证明这条断言区分的正是两者的差异，而不是巧合。


## 「刚入手的卡」指针

测试脚本：`tests/verify_card_obtained_pointer.py`。

项目里有**两套互相独立**的指针，由两条不同指令读取：

```
GetCardsBeingTreated   → Player.lastDrawnCards      （Player.GetLastDrawnCards()）
GetCardBeingAddToHand  → battlefield_.lastCardAddedToHand
```

只写其中一个，另一条就会拿到**上一次入手的卡**（或空列表）。全程不报错，只表现为
「后续指令作用到了别的卡上」——`[紧急投产]` 的
`Develop($deck)|GetCardsBeingTreated|setCost(0)` 就是这么减不到开发出的那张卡上的。

- 冒烟测试：源文件与卡表均存在；`RecordCardsObtained(List<cardBase_>, IsFriend)` 可定位。
- 基本验证：该函数**两套指针都写**——`lastCardAddedToHand = cards[cards.Count - 1]`，
  以及 friend / enemy 两条分支各自写对应 Player 的 `SetLastDrawnCards`；且有空列表守卫。
- **回归**：`RecordCardsObtained(` 全项目恰好 5 处（1 处定义 + 4 处调用）；`Develop` 的
  友方/敌方两个分支都调了它；「加入手牌」指令也改走同一入口。
- 单一实现：`^\s*lastCardAddedToHand\s*=` 全项目只匹配到 **1** 处；且
  「加入手牌」指令里原来的内联 `lastCardAddedToHand = card;` 已不存在。
- 前提校验（防空转断言）：`GetCardsBeingTreated` 确实读 `player1.GetLastDrawnCards()`；
  `GetCardBeingAddToHand` 确实读 `lastCardAddedToHand` 字段。
- 触发用例：`[紧急投产]` 的 effect 以 `Develop($deck)|GetCardsBeingTreated` 开头且含
  `setCost(0)`。
- 边界（有意保留）：`DrawCard` 仍写 `Player.lastDrawnCards`（抽牌路径未被改动）。

## 血量系统与事件界面

测试脚本：`tests/verify_hp_and_event_ui.py`（54 条）。

覆盖本批 7 项需求：事件选项的资源点门槛、会加卡选项的悬浮预览（同名去重计数）、
血量系统（开局 5 / 失败扣血不再立刻结束 / area7 清零 / 事件 `hp(n)` / worldMap 显示）、
新增 UI 一律在场景里、任务面板的返回按钮与「返回后重进不重抽」、商店 200 资源点买 1 点血。

- **界面相关的断言一律读 `.tscn`** —— 这本身就是对「UI 不写在代码里」这条需求的校验：
  哪天有人把 UI 挪回代码，`worldMap.tscn 里有 heartPic / hpNum`、`store.tscn 里有 BuyHp`
  这类断言就会红。另有一条反向断言：`EventCardPreview.cs` 里不得出现 `new Panel(`、
  `new Label(`、`new HBoxContainer(`。
- **扣血规则**：断言 `LoseHpOnBattleDefeat()` 里 `IsFinalArea` 清零、`IsBossBattle() ? -2 : -1`。
- **失败分支**：断言 `if (hpLeft > 0) _ = ReturnToWorldMapAfterDefeat(...)` 与
  `else _ = ReturnToStartMenuAfterDefeat();` 同时存在——两条路都要在。
- **战败不重复给奖励**：断言 `ReturnToWorldMapAfterDefeat` 体内不出现 `PostBattleReward`。
- **返回按钮的双向语义**：`DismissChooseMission()`（事件完成后）必须清空 `_drawnIds`，
  而 `CloseMissionPanel()`（点返回）**不得**清空——两条断言一正一反，防止把两者合并。
- **同名去重**：断言按 id 计数而不逐张罗列（`[snowstorm]` 有 20 张同名「埋伏」）。

## 调试控制台外观

测试脚本：`tests/verify_console_style.py`（26 条）。

需求：把战斗场景与世界地图两个控制台的边框去掉，改成浅黑色非圆角矩形。

**最容易漏的一点**：只换外层 `Panel` 的 StyleBox **去不掉边框**——Godot 默认主题给
`LineEdit` 的 `normal` / `focus` 样式自带圆角与描边，输入框那一圈框照样画得出来。
所以测试显式断言 `normal` / `focus` / `read_only` 三个状态都被覆盖。

- 冒烟：`bin/ConsoleStyle.cs` 存在。
- 边框与圆角：八个属性（BorderWidth × 4、CornerRadius × 4）逐个断言显式归零。
- 浅黑色：解析 `Background` 常量的 RGBA，断言 R=G=B（中性灰，不是带色的黑）
  且 `0 < alpha < 1`（带透明度才是「浅」黑，纯黑不透明不算）。
- **样式单一来源**：`battlefield_.cs` 与 `WorldMap.cs` 里**不得**再出现
  `new StyleBoxFlat` 或 `AddThemeStyleboxOverride`，且各自恰好调用一次
  `ConsoleStyle.Apply(_consolePanel, _consoleInput)`。原先两处各写了一遍同样的样式，
  改一处忘一处就会出现两个控制台长得不一样。

## 战斗动作与行动能力时序

测试脚本：`tests/verify_combat_action_timing.py`（27 条）。

覆盖四项「状态该结束时没结束」的问题：拖拽被截图工具打断后的兜底收尾、
行动能力按阵营在各自回合开头刷新、被撤退单位的战斗能力、多张单位卡弃置的错开节奏。

- **① 拖拽兜底**：断言 `CancelCurrentDrag()` 同时把 `caught` 还原成 `inHand`、
  `inplaceAndCaught` 还原成 `placed`，并调用 `RefreshMyHand()`。
  另有一条**顺序断言**：`_Input` 里那条「物理左键已松开」的兜底必须出现在
  `if (ReadControlState() == 1) return;` **之前**——放在后面的话，
  控制锁期间永远轮不到它，收尾照样挂住。
- **② 按阵营刷新**：正向断言 `RefreshCardsInField(IsFriend.friend)` 出现在
  `EnemyTurnAsync()` 与 `ApplyTurnStartTraits()` 之间，反向断言被替换掉的
  `RefreshAllCardInField()` 已经不存在（防止有人把两者都留着）。
- **③ 撤退禁战**：断言 `RetreatUnit()` 里 `DisableCombatAbility()` 恰好出现两次
  （友方回手牌、敌方弃置各一次），且敌方那条排在挂待弃置标记之前。
- **④ 弃置错开**：解析 `DiscardStaggerSeconds` 的值断言为 0.5；
  断言 `Task.WhenAll(tasks)` 存在、辅助函数自己不播动画、单张时不额外等待。

> 写这个脚本时踩到一个 Python 陷阱值得记下：`"x" in s is False` 是**链式比较**，
> 等价于 `("x" in s) and (s is False)`，恒为假。要写 `("x" in s) is False`。

## 敌方脚本、刷兵与关卡开局效果

测试脚本：`tests/verify_enemy_scripts_and_spawn.py`（27 条）。

这三件事的共同点是**失效时一声不吭**：写错了既没有报错也没有效果，
只能靠对局里「怎么没反应」发现，所以逐条静态钉住。

- **④ 弃置玩家的牌**：反向断言战役行动里**不得**再出现裸的 `DiscardRandomly`；
  新分支里**不得**出现 `sourceCard?.GetIsFriend()`（那正是旧写法按阵营派发的病根）。
  另有一条配置侧的**描述一致性**检查：`DiscardPlayerRandomly(n)` 的数字与
  `弃n张` 的说明必须相等（t6 曾经是「代码弃1张、说明写2张」）。
- **⑤ 刷兵吃表达式**：用正则断言旧的 `(\d+)` 写法不再出现、`int.Parse(` 不再出现，
  以及三条静默出路各有一条 `[SpawnByCost]` 日志。
- **⑪ battleStart**：断言它被从行动队列里**单独摘出**（否则会作为一条「敌方意图」
  显示在左侧面板上）、换关卡时一并清空、且 `StartBattleAsync()` 里
  `RunBattleStartEffectAsync` 排在 `StartOpeningHandAsync` 之前。
  另有一条 `&hp` 的「没有偷偷做换算」断言：取值附近不得出现 `/10` 之类的修饰。

## 守护豁免与事件叠层

测试脚本：`tests/verify_guardian_bypass_and_overlay.py`（25 条）。

- **⑩ 火炮/轰炸机无视守护**：断言豁免只认兵种、不看阵营
  （`IgnoresGuardian` 函数体内不得出现 `IsFriend`），且
  `IsTargetProtectedByGuardian` 里它排在 `HasSmokeScreenActive` **之前**——
  排在后面的话，目标带烟幕时函数会先返回，豁免被绕过。
  再断言守护判定函数被调用 3 次以上，确认玩家侧与 AI 侧走的是同一处。
- **⑥ 事件叠层**：断言事件暗幕是 `Ignore`（不拦鼠标）、
  `EnterEventOverlay` 走 `CloseMissionPanel` 而**不含** `DismissChooseMission`
  （保留批次），`ExitEventOverlay` 才丢弃；并解析 `_on_store_pressed` 的
  `canvasLayer.Layer` 断言它大于事件层的 2。
- **⑦ 标准弹药**：断言两段 `foreach` 各有自己的 `End&`、`subCost(1)` 恰好两次、
  `FriendlyCardDrawn` 已消失。另断言 `AnimateCostRoll` 的 `IsInsideTree()` 保护
  排在 `GetNode<Label>("cost")` **之前**——牌堆里的卡不在场景树上，
  这层保护放晚了照样空引用。

## 时点触发后的死亡检查

测试脚本：`tests/verify_trigger_death_check.py`（14 条）。

实机反馈：「场上有女狙击手，打出一张机动防御之后，有单位变成 0 血但是没死亡」。

`ExecuteCommandAndDiscard` 里的死亡检查排在时点**之前**，只覆盖了指令自身的效果；
而 `damage(n)` 是缓存型变更，时点跑完时目标防御已经是 0，只是没人再查一次。

- **修复点**：断言 `FriendlyCommandPlayed` 时点之后**还有一次** `CheckIfAnyUnitDiedAsync()`
  （用 `rindex` 取最后一次——第一次排在时点之前是正常的，两次都要在）。
- **观察点**：解析 `card.ini` 的 `[女狙击手]`，断言它确实挂在 `FriendlyCommandPlayed` 上、
  效果里带 `damage(` 且目标取自 `GetRandomEnemyTarget`（即「能打死人」且必须靠死亡检查收尾）。
- **邻居防回归**：`Move()` 的 `Moving` 时点、`AddCardToPlace()` 的 `FriendlyUnitEnteringField`
  时点，都断言「先 `ResumeDeathCheck()` 再查死亡」，防止重构时被删掉。
- **已知缺口留痕**：断言 `TriggerFriendlyCardDrawn` 仍是发后不理，且该缺口已写进
  `docs/BUGS.md` 第 49 条。这样一旦有人修好它，测试会提醒同步文档。

## 阵亡爆炸音效随机池

测试脚本：`tests/verify_explosion_sfx.py`（21 条）。

需求：爆炸音效改成在 `assest/爆炸3.wav`～`爆炸21.wav` 里随机播，**下划线开头的不要用**
（`_爆炸16` / `_爆炸17` / `_爆炸19` 是未采用的版本）。

实现复用了 `configs/music.ini` 已有的「槽位 + 逗号分隔 + 随机抽一条」机制，
音效放在独立的 `[sfx]` 段，与 BGM 的「播完再切」调度分开。

- **规则不许漂**：不写死 16 个文件名，而是**按需求规则现算**——扫 `assest/` 取
  `爆炸3`～`爆炸21`、排除下划线开头的，再断言配置里的清单与现算结果**完全相等**。
  这样「删了文件忘了改配置」「加了新音没配进去」「不小心把 `_爆炸16` 列进来」三种情况都会红。
- **两层排除**：既断言清单里没有 `_` 前缀项，也断言那些 `_` 文件确实存在于磁盘上
  （证明这条规则不是在空跑）。
- **复用而非重写**：断言 `[music]` 与 `[sfx]` 走同一个 `LoadSection`，
  防止有人给音效另写一份 ini 解析。
- **失败要退而不哑**：断言 `PickSfx` 在槽位缺失/加载失败时返回 `null`，
  且 `PlayDeadSound` 只在非 null 时才覆盖 `Stream`——否则一次配置写错会让爆炸彻底没声音。
- **顺带一类错**：遍历全项目 `.tscn` 的 `AudioStream` 外部引用，断言目标文件都存在。
  `battleField.tscn` 曾引用不存在的 `res://assest/机枪.wav`（实际只有 `机枪_低.wav` /
  `机枪_高.wav`），Godot 只在控制台报一行 `Resource file not found`，游戏照跑，
  很容易被忽略。

## attackEffect 多效果与 flying / bombing / airstrike 特效

测试脚本：`tests/verify_attack_effects.py`（116 条）。

需求：① `attackEffect` 里能填多个效果；② 新增 `flying`（卡牌飘起来→左右摆→落回，配
飞机飞过_单位 音效）；③ 新增 `bombing`（用 航弹.png 做类 bullet 的效果，攻击力多少扔多少发）；
④ 新增 `airstrike`（投弹要夹在飞掠**中间**：起飞 → 投弹 → 降落）。

**设计上的关键取舍**（测试把这些取舍钉住了，改坏会红）：

- **只给坐标不够用**。`flying` 要动的是**那张卡本身**（拿不到节点就没法做动画），
  `bombing` 的弹数 = **攻击力**（特效自己不知道攻击力多少）。所以 `Effect.Play` 增加了
  `source` 与 `count` 两个入参，三个已有实现跟着改签名——**而不是给这两个新效果各开一个特例**。
- **`bombing` 不写第二个类**：与 `bullet` 共用 `BulletEffect` 脚本，差别只在场景里
  Export 出去的「弹体场景」与「弹数」。断言「bombing 场景挂的是 BulletEffect 脚本」
  就是这条约束。
- **弹体池按场景路径分池**：子弹与航弹混在一个队列里会串味（取到航弹却按子弹的贴图/朝向播）。
  断言旧的 `AcquireBullet` / `ReleaseBullet` 已不存在，且释放时按记录的路径还回原池。
- **`flying` 必须还原干净**：`finally` 里把位置/缩放/层级/`isUnderCardEffect` 全部还原——
  中途出错也不能让一张卡永远浮在空中压在别人身上。
- **刷 ZIndex 的那套会打架**：`RefreshAllCardDisplayOrder` 对场上卡一律 `ZIndex = 10`，
  所以漂浮期间要么被跳过、要么抬起来的层级下一帧就被打回去。断言那条 `continue`
  同时看 `isDiscarding || isUnderCardEffect`。
- **「夹在中间」拼不出来，只能继承**。要的是「升起 → 投弹 → 落回」，而
  `attackEffect = flying,bombing` 只能做到两段各自开跑（总时长取较长者）。
  所以 `AirStrikeEffect : FlyingEffect`，**只覆写一个钩子**（`DuringRiseAsync`）=
  投弹。测试同时钉住反面：`AirStrikeEffect` 里**不得出现**
  `private async Task RiseAsync(` / `LandAsync(` / `SwayAsync(`——
  出现任何一个就说明运动代码被抄了第二份。
  （断言要按**完整方法签名**查：父类那个钩子叫 `DuringRiseAsync`，
  只查 `RiseAsync` 会把「覆写钩子」误判成「抄了一份升起实现」。）
- **投弹是子特效，不是第二套弹道**。`airstrike` 通过
  `EffectRegistry.Create(StrikeEffectName)` 播一遍已注册的 `bombing`，弹数
  （`count` = 攻击力）、飞行时长、错开间隔都还留在 `bombing` 场景里。

### 实机反馈：投弹时听到机枪「哒哒」声

那不是投弹音效，是**每次攻击都放**的通用开火声（`battleSound`）。飞机掠过时再叠一层
机枪声就串味了。

修法是按**攻击者的 `attackEffect`**决定放不放：
`EffectRegistry.NoFiringSoundNames`（`flying` / `airstrike`）+ `ReplacesFiringSound()`，
调用点在 `battlefield_.cs` 的 `Attack()` 里、`PlayBattleSound(1)` 那一行。
（表名后来从 `SelfVoicedNames` 改成 `NoFiringSoundNames`，原因见「第九轮调整」。）

判定**不写到卡上、也不按 `CardTypes.Bomber` 判**——那两条都会让同一个特效配在不同卡上
行为不一致。测试除了断言这条判断存在，还会**交叉核对**表里每个名字对应的场景确实挂了
`AudioStreamPlayer`：否则这张表会变成一句假话（写了名字却根本不发声，等于白静音一场）。

### 两个「钉错了地方」的断言，已经改掉

原先的写法是「场景里必须写全每个 `[Export]` 的值，且每个值上方都有一行中文 `;` 注释」。
这是**钉不住的**——在 Godot 编辑器里保存一次，注释被整段抹掉，取值等于 C# 默认值的属性
也被省略（实测 `flying_effect.tscn` 的 9 项只剩 2 项）。这种断言只会制造「每次调完手感就红」
的假红灯，最后没人看。

改成钉三件真的不会变的事：

1. 每个 `[Export]`（**含继承链上的**）上方都有中文 `/// <summary>`——C# 里那份留得住，
   也正是编辑器里悬停能看到的；
2. 场景里写的属性名必须真实存在（Godot 对拼错的属性名**静默忽略**，最难查）；
3. 场景里的 `;` 注释只打印提示、**不作失败**。

顺带一条测试自身的原则：**手感数值（摆幅、周期、升降时长）不钉具体数字**。
那些是主人在编辑器里调的，钉了就会「调一次红一次」。

参数（升起高度、放大倍率、摆幅、时速、音效槽位）全部 Export 在 `effects/flying_effect.tscn` 上，
调手感不必改代码——测试断言这些字段都是 `[Export]`。

### 第二轮调整（flying 的旋转 / bombing 的命中音效，两者都已撤销）

- ~~**flying 的「摆动」改成「先指向目标再摆」**~~（**已撤销**）：第一版是在 X 轴上左右
  **平移**，第二版改成「先指向被攻击的目标，再在那个角度上左右摆」。后来主人要求
  **不转向**，这一整套（`AimRotationOffset`、升起的转向 tween、绕瞄准角摆动）已删除。
  见下面的「第四轮调整」。
- **落回要连角度一起还原**，否则打完之后卡会斜着停在场上。`finally` 的还原清单
  已包含 `Rotation`（与位置/缩放/层级/标记并列）。这条即使在「全程不转角度」之后也留着——
  它是摆动被中途打断时的兜底。
- ~~**bombing 的爆炸音效按「每发命中各响一声」做**~~（**已全部撤销**）：
  曾给 `BulletEffect` 加过 `ImpactSfxSlot` / `ImpactSfxVolume` / `ImpactVoiceCount`
  三个 Export 与一套多声部播放器。主人实测后要求**投弹不出声**，第一版只是把槽位留空
  （「靠配置关掉」——随时会被谁填回去），第二版**把整套机制整段删除**：
  `BulletEffect` 里现在没有任何 `AudioStreamPlayer`，弹体在结构上就发不出声音。
  测试相应改成断言**代码里不存在音效路径**，而不是断言某个槽位为空；
  搜这类「已经删掉的东西」前要先剥掉注释（`code_only()`），
  否则说明它被删掉的那段文档本身会把名字搜出来。

### 第三轮调整（flying 的时序与速度）

三条要求：① 升到最高点时方向**已经**调整完毕；② 升起/降落/偏转都太快，摆动约 **4 秒一个周期**；
③ 摆幅缩到 **±5°**。

- **① 的根因是时序，不是速度**：原先「升起 → 转向 → 摆动 → 落回」是四段串行，所以升完才开始转，
  自然会出现「悬在空中还在慢慢转」的中间状态。当时改成升起/放大/转向同一个 tween 并行。
  （**转向本身后来被整段移除**，见「第四轮调整」；「同一个 tween 共用 duration」这个做法保留。）
- **② 的根因是「按总时长的百分比切段」**：总时长只有 0.9 秒，升起占 30% 就是 0.27 秒，
  快得看不清。而「摆动 4 秒一个周期」这种要求用百分比根本表达不出来，所以改成
  **每段各自填秒数**（`RiseDuration` / `SwaySecondsPerCycle` / `LandDuration`）。
  测试反向断言百分比常量（`RiseFraction` 等）已移除——它们再回来就意味着又切回去了。
- **③** 是场景里的值（`SwayDegrees = 5.0`），代码里只是读它，测试断言场景里确实是 5.0。
  （这条断言后来删了：主人的手感值一直在调，钉具体数字只会「调一次红一次」。）

### 第四轮调整（flying 不转向，改成「升起 → 悬停 → 落回」）

要求：**起飞后悬停，但不转向**；再确认「完全不转，静止悬停」。

- **`AimRotationOffset()` 整段删除**，连「指向被攻击的目标」这个能力一起去掉。
  升起那段的转向 tween 也没了（`RiseAndAimAsync` → `RiseAsync`），**升起只做位置 + 缩放**。
- 中间那段从「摆动」正名为**悬停**（`SwayAroundAimAsync` → `SwayAsync`），
  摆的基准从 `aimRotation` 换成 **`baseRotation`（卡自己的原始角度）**。
- **`SwayDegrees` 默认改成 0** = 完全不转。这种情况下 `StayAsync` 走「等够时间」的分支，
  **不建一串「原地不动」的 tween**；悬停时长 = `SwaySecondsPerCycle × SwayCycles`
  （所以那两个键的名字在「不摆」时读起来会是「悬停时长」，已在 C# 注释里写明，
  但**不改名**——主人现有的配置键不动）。
- 两个场景里的 `SwayDegrees = 2.0` 也去掉了（等于默认值，编辑器保存时本来也会被省略）。

测试的相应改法：反向断言 `AimRotationOffset` / `RiseAndAimAsync` / `SwayAroundAimAsync`
**都不存在**；`RiseAsync` 里**不含 `rotation`**（这就是「不转向」）；摆动绕的是 `baseRotation`；
`SwayDegrees` 默认值必须是 `0f`。

### 第五轮调整（三条实机反馈）

**① 起飞的单位挡在手牌上面。** 查出来两件事：

- 飞掠抬起的卡在源里是 `TopZIndex = 15`（低于手牌的 20），但**场景里曾经写死过
  `TopZIndex = 200`**——场景值优先于 C# 默认值，那段时间它当然压住手牌。
  现在默认值改成 **12**（场上 10 与手牌 20 之间），并在 `NOTICE.md` 记下这个坑。
- **`_discardZCounter = 50`** 是**确实**会压住手牌的：阵亡/弃置动画的层级从 50 起
  一路自增，50 > 20。现在改成 `[DiscardZBase(11), DiscardZMax(19)]` 区间取号并**封顶**
  （宁可几张同层，也不许爬到手牌之上），每批弃置前 `ResetDiscardZCounter()`。

新增 `tests/verify_card_layering.py`（18 条）专门钉这条链子：抬起卡与弃置动画的层级
都必须落在 10 与 20 之间；递增必须封顶；两条弃置路径走同一个取号函数。
`NOTICE.md` 里补了完整的层级表和那条唯一不变式。

**② 炮弹发射得太晚。** airstrike 的投弹原来挂在 `StayAsync`——要等起飞那整整一秒演完
才开始投。`FlyingEffect` 新增 `DuringRiseAsync` 钩子（与起飞**并行**，父类用 `WhenAll`
把它和升起等在一起），`AirStrikeEffect` 把投弹挂上去：卡刚一离地，航弹已经在飞了。
用 `WhenAll` 而不是 fire-and-forget 是刻意的——否则会出现「卡已落回桌面、航弹还在半路」，
而且特效节点被回收时会把没播完的弹一起删掉。

**③ 阵亡到爆炸之间加 1 秒延迟。** `ProcessDeadUnitAsync` 里拆出
`PlayDeathPresentationAsync`：卡在场上**停留 1 秒**（`DeathPresentationDelaySeconds`）
-> 消失 -> 冒烟 + 爆炸声。两个关键点：

- **调用方不 await 它**（`_ = PlayDeathPresentationAsync(...)`）。阵亡检查的时序
  （`ResumeDeathCheck` / `AllowControl`）不该被一段纯表现拖住；多个单位同时阵亡时
  也各算各的，不会一个等一个。
- **停留前必须先把状态打成 `destroyed`**。`IsDeadPlacedUnit` 要求 `state == placed`，
  所以不会被下一轮死亡检查重复统计；`TriggerUnitEffects` 也只挑 `state == placed`
  的单位（`battlefield_.cs:1937`），所以死亡时点不会点到这具「还没消失的尸体」。
  `verify_unit_dead_trigger.py` 的测试 2 相应重写成钉这条（原来是钉「触发在 RemoveCard
  之后」——`RemoveCard` 挪进表现方法后，那条断言会变成一个永远成立的空断言）。

### 第六轮调整（航弹放大 + 落地后仍压手牌）

**① 航弹太小。** `bin/bomb.tscn` 的 Sprite2D 缩放 `0.6 → 1.2`，净尺寸从 `3 × 0.6 = 1.8`
变成 `3 × 1.2 = 3.6`，翻一倍。测试不钉"就是这个数"，而是钉**净尺寸 = 根 scale × 贴图 scale
且不小于 3.6**——两个缩放都写在场景里，主人自己调大小不必改代码。
（`project.godot` 的 `default_texture_filter=0` 是 Nearest，放大会看到像素块而不是模糊。
`航弹.png` 只有 10×14，想更清晰得换更大的素材。）

**② 动画播完、飞机落下之后，卡仍然压在手牌上面。** 上一轮只修了"动画期间"，
这一轮找到了收尾那一半：

- 攻击是从**拖拽释放**发起的（`Attack(cardNowChoose, ...)`），那会儿卡被抬到 `ZIndex = 100`。
- `FlyingEffect` 开头存 `baseZIndex = source.ZIndex` → 存到的是 **100**；`finally` 里
  `source.ZIndex = baseZIndex` 又还原成 100。手牌才 20，于是永久压住。
- **它不会自己好**：层级只在 `RefreshAllCardDisplayOrder()` 里被改回去，而那个函数只在
  `_displayOrderDirty` 为真时跑，特效结束时没人标脏。只有玩家碰巧悬停别的卡才会顺手修好
  ——所以现象看起来像"有时候好有时候不好"。

修法是 `finally` 里还原完补一句 `field._displayOrderDirty = true;`：
**特效只负责说"该重算了"，不自己猜一个层级**。层级数字的唯一权威是战场的
`RefreshAllCardDisplayOrder()`；在特效里写死一个 10 等于把同一份约定配到第二处。
这条已经写进 `docs/NOTICE.md` 的层级约定里，`verify_card_layering.py` 加了两条守着
（必须标脏、必须不自己写死层级）。

### 第七轮调整（航弹加速 / 盘旋减半 / 死卡不可选）

**① 航弹飞行逐渐加速、不减速，到最快时消失；盘旋时间减半。**
`bin/Bullet.cs` 加了 `[Export] Tween.EaseType FlightEase`，默认 `InOut`——
**子弹（机枪）保持原来的收尾减速，行为一点没变**；只有 `bin/bomb.tscn` 设 `FlightEase = 0`（`In`）。
`In` 的最快点正好是 tween 结束那一刻，而 `OnMoveFinished()` 就挂在那一刻，
所以「速度最大时直接消失」不需要额外代码，它本来就是那个时机。

> `0` 到底是不是 `In` **用 Godot 自己实测过**（临时 GDScript 读 `Tween.EASE_IN` 与
> 场景实例的 `FlightEase`），因为 `.tscn` 里的属性名/枚举值写错是**静默生效**的——
> 这种地方不能靠记忆。

`effects/air_strike_effect.tscn` 的 `SwaySecondsPerCycle` `1.0 → 0.5`（盘旋减半）。

**② 阵亡但暂留在场上的卡能被选为攻击/指令目标。**
四个漏洞，修法是**一个判据 `CanBeSelected()`，四处引用**：

| 位置 | 原来为什么漏 |
|---|---|
| `IsValidTarget()` | 不检查状态。它是目标高亮 / 目标计数 / 指令落点校验的**共同入口** |
| 攻击落点分支 | **根本不走 `IsValidTarget`**（那是给指令用的目标类型筛选），只比阵营 |
| `CheckCardClick()` | 遍历 `cardInPlaces` 无状态过滤；点中尸卡会把 `cardNowChoose` 换成它 |
| `HighlightValidTargets()` | 只遍历 `placed`，尸卡不变灰——周围全灰它保持原色，看着像「这个能打」 |

新增 `tests/verify_dead_unit_targeting.py`（15 条）。写这个测试时踩到的坑值得记一笔：
`case InputState.P_InPlaceUnit:` 在文件里**有两个**（按下的 switch 与释放的 switch），
按 `case` 标号切会切到错的那个；`CheckCardClick` 里第二行就有一句
`if(cardInPlaces== null) return null;`，按 `return null;` 切会当场截断。
**切片要按内容定位，或者干脆取固定长度窗口。**

### 第八轮调整（新增 `strafe` 扫射）

要求：新增一种攻击动画——把「起飞投弹」换成「起飞后发射子弹」，其他不变；毛驴改用它。

**一行 C# 都没改。** `AirStrikeEffect.StrikeEffectName` 本来就是「起飞时打什么」的开关
（当初把它做成 `[Export]` 就是为了这一天），所以新动画 = **新场景 + 改 `card.ini` 一个值**：

- 新建 `effects/strafe_effect.tscn`，挂 `AirStrikeEffect` 脚本，`StrikeEffectName = "bullet"`；
- `EffectRegistry.ScenePaths` 加 `["strafe"]`；
- 毛驴（`cards/card.ini`，战斗机，攻击 2）`attackEffect = bullet` → `strafe`。

> 音效那一项当时按「与 `airstrike` 同规格」处理（静音），下一轮就按主人的要求改回来了，
> 见「第九轮调整」。

测试里那两条是重点：

- **「两个场景只差子特效名与节点名」**：把两个 `.tscn` 的非注释行做差集，除了
  `StrikeEffectName` 与 `[node name=...]`，不该有别的差异。这条断言就是「其他不变」的机器版。
- `strafe` 场景挂的必须是 `AirStrikeEffect.cs`——防止有人又写第二个类。

> `StrikeEffectName` 与 `FlightEase` 一样，在 `.tscn` 里写错是**静默生效**的，
> 所以两轮的 Export 值都用临时 GDScript 读出来实测过（`bombing` / `bullet` 各自正确）。

### 第九轮调整（`strafe` 开火要听得到机枪声）

要求：`strafe` 开火时也播放机枪音效。

看起来只是「从表里删一行」，但那行删掉之后**表的旧名字就变成假话了**：
`SelfVoicedNames` = 「自带音效的特效名」，而 `strafe` **自带飞掠声**、却不在表里。
所以改名成 **`NoFiringSoundNames`**（按**结果**命名，不按原因），并重写注释把判据写清楚：

> 判断依据是「**打出来的东西和机枪声搭不搭**」，不是「有没有自己的音效」。
> `flying`/`airstrike` 扔炸弹 → 机枪声串味 → 不放；`strafe` 打枪 → 那声机枪正是它要的 → 放。

测试那边加了一条**反向断言**：`strafe` **必须不在**静音名单里。这条是本次需求的核心，
以后有人「顺手统一一下」把它加进去会立刻红。同时加了一条「旧名 `SelfVoicedNames`
已彻底移除」——留着就是两个说法打架。

### 第十轮调整（卡牌语音：打出卡时喊一声）

需求：新增了 8 个音效（严冬 / 战略重心 / 红色旗帜 / 嘿 / 阿嘿 / 朱可夫 / 拉伸 / 预备役），
让对应的卡打出时播放。其中 5 个的名字与卡名对不上，由主人给出了映射。

**做法：给特效名加了一层「参数」语法。** 卡写 `playEffect = sfx(严冬)`，链条是

    StartEffect（用括号感知的 SplitEffectString 拆名）
      → EffectRegistry.ParseName 拆出名字 `sfx` 与参数「严冬」
      → Create 取 effects/sound_effect.tscn
      → AddChild → Configure("严冬") → PrepareForUse
      → SoundEffect.Play → MusicManager.PickSfx("严冬") → configs/music.ini 的 [sfx] 段

三个刻意的取舍：

1. **没有做成一音效一场景**。语音会越加越多，一音效一场景的话每加一句都要新建 `.tscn`
   再改注册表；现在加一句只要 `[sfx]` 写一行、卡上写一行。
2. **拆分交给已有的 `SplitEffectString`**（括号与引号感知），没有新写一个拆分器——
   朴素的 `Split(',')` 会把参数里的逗号也当分隔符。
3. **`Configure` 排在 `AddChild` 之后**。先让节点进树（`_Ready` 跑完、子节点就绪），
   参数里才可能安全地 `GetNode`；反过来会拿到 `null`。测试钉住了这个顺序。

`SoundEffect.Play` 必须 **等音效放完再返回**：调用方 `RunEffect` 的 `finally` 会立刻回收
特效节点，不等的话播放器连着声音一起被删掉，只听得见开头一小截。测试钉住这句
（依赖素材是**非循环**的——8 个新 wav 的 `edit/loop_mode` 都是 0，已核对）。

新增 `tests/verify_card_voice.py`（94 条），做两条**交叉核对**：

- 卡里写的槽位必须在 `[sfx]` 段里存在（拼错一个名字就静默没声）；
- 槽位指向的音频必须**已被 Godot 导入**（有 `.import`）。

第二条是实机跑出来的：`战略重心.wav` 少了 `.import`，另外 7 个都有，于是
`ResourceLoader` 报 `No loader found for resource`、返回 null——配置全对却没声。
用一次 `--headless --editor --quit` 扫描就补上了。

本轮还踩到一个自己造成的坑：**用 `utf-8-sig` 写回 `cards/card.ini` 给它加了 BOM**，
而该文件本来没有 BOM，用 `encoding="utf-8"` 读它的两个测试（`configparser`）
当场 `MissingSectionHeaderError`。已去 BOM；这条写进了 `NOTICE.md`。

### 第十一轮调整（指令卡默认音「咚」）

需求：新增了「咚」音效，让**所有没标注打出效果的指令卡**播它。

94 张指令卡里只有 8 张自带语音，**剩下 86 张**要默认音。做成**默认值**而不是在 86 张卡上
各写一行 `playEffect = sfx(咚)`：

- 同一串值抄 86 遍正是规范 (E)「同一数据应只在一处配置」要挡的；
- 以后每加一张指令卡都要记得补那行，**漏了就静默没声**，没人会发现。

落在 `battlefield_.cs` 的 `DefaultCommandPlayEffect = "sfx(咚)"` + `PlayCardEffect` 里的一句兜底：

    string effect = card.playEffect;
    if (string.IsNullOrWhiteSpace(effect) && card.cardType == CardTypes.Command)
        effect = DefaultCommandPlayEffect;

- **8 张自带语音的以卡为准**（卡上写了就不兜底）；
- **只有指令卡**兜底，单位卡不受影响（`PlayCardEffect` 也服务单位部署，见 `AddCardToPlace`）；
- 音效文件仍然只配在 `[sfx]` 段一处：`咚=res://assest/咚.wav`。

测试补 7 条，其中一条是交叉核对：**常量里写的槽位名必须在 `[sfx]` 段里存在**——
拼错的话整批 86 张指令卡一起静默没声。

### 第十二轮调整（步兵进场音，按身材分三档）

需求：新增 3 个 `AU_Infantry_*` 素材，步兵**部署**与**加入战场**时都播；
按 `attack + defence` 分档——≤4 小、5~8 中、≥9 大。三处不确定由主人定：**以卡为准**、
**双方都播**、**取进场那一刻的当前值**。

**一个关键发现让改动变得很小**：这两条进场路径**已经汇聚到同一个函数** `PlayCardEffect`——
从手牌拖上场走 `Move()`（`isDeployedFromHand`，同时触发 `Deployed`），
效果刷进场走 `AddCardToPlace()`（同时触发 `BeingAddedToField`）。
所以挂在 `PlayCardEffect` 上就一处覆盖两条路，而且**在场上挪位置的单位两条都不满足、不会重播**。

判定写成 `InfantryDeployEffect(card)`：`cardType != Infantry || isHq == HQ.hq` 直接返回 null
（**总部的 `cardType` 也是 `Infantry`**，不排掉的话开局摆总部也会响一声）。

测试补 17 条，其中两条值得记：

- **把规则当规格来验**：从源码里读出 `InfantryVoiceSmallMax` / `InfantryVoiceMediumMax`，
  在 Python 里重新算一遍 `1→小 / 4→小 / 5→中 / 8→中 / 9→大 / 18→大`。
  这比断言「源码里有 `<= 4`」结实——改常量而忘了改规则会红。
- 三条音频的存在与 `.import`、三个槽位名与代码常量对得上（拼错就整批步兵静默没声）。

顺带核对了一遍真实数据：46 张非总部步兵分成 **15 小 / 21 中 / 10 大**。

切片又踩了一次坑：`Move` 后面不是 `MoveUnit`，按「下一个方法名」切直接 `ValueError`。
固定长度窗口才是稳的（这条在第十轮已经记过一次）。

### 第十三轮调整（坦克/火炮进场音，飞机移动也响）

需求：新增 `Tank_*_Move_Fx` 素材，规则与步兵一致；**坦克与火炮**播它；
**飞机入场与移动**都播 `飞机飞过_效果`。

**没有新写一套判定，而是把上一轮的 `InfantryDeployEffect` 泛化成 `DeployMoveEffect`**：
档位规则本来就一样，差别只在**素材家族**。于是槽位名改成 `{前缀}_{档位}` 拼出来：

    CardTypes.Infantry                   → infantry_{small|medium|large}
    CardTypes.Tank or CardTypes.Artillery → tank_{small|medium|large}
    CardTypes.Plane or CardTypes.Bomber   → plane_flyby（不分档）

这样加一个兵种只要加一个前缀常量 + `[sfx]` 三条，判定逻辑一行都不用改。
上一轮的 6 个「一档一个槽位常量」也随之收成了 3 个（两个前缀 + 飞机那条）——
测试里有一条专门断言旧常量已消失，防止有人又抄回去。

**「移动」是新的时机**：入场那一声在两轮的汇聚点 `PlayCardEffect` 里，
而移动走的是 `Move()` 的 `else`（非部署）分支，那里新加 `PlayPlaneMoveEffect(card)`。
两条互斥，不会连响两声——测试断言部署分支里**没有**这个调用。

移动那一声**不看卡上的 `playEffect`**：那个语义是「打出时」，在场上挪位置不算打出。

测试补 30 条（⑥ 扩写 + 新增 ⑦）。顺带核对真实数据：
29 张坦克/火炮分 **7 小 / 11 中 / 11 大**，13 张飞机/轰炸机走不分档那一条。

**又一次靠「交叉核对」拦住静默失败**：`Tank_*.wav` 三个文件**都没有 `.import`**，
`ResourceLoader` 会直接返回 null、配置全对也没声——和上一轮 `战略重心.wav` 同一个坑。
跑一次 `--headless --editor --quit` 补上即可（测试里每个槽位都断言 `.import` 存在）。

### 第十四轮调整（喀秋莎专属音；单位「移动」时也响）

需求：喀秋莎两个专属音——`AU_Rocket_Art_Katyusha_fire_02` 是**攻击**音、
`AU_Rocket_Art_Katyusha_IntoPos_01`（主人写字误作 `_02`）是**移动**音。

**先纠正一个前提**：主人说「似乎步兵入场音效在步兵移动时也会播放，所以不需要改 ini 写法」。
**核实结果是并不会**——`PlayCardEffect` 只有四个调用点（`AddCardToPlace` 刷进场、
`Move()` 的 `isDeployedFromHand` 部署分支、两条指令卡路径），移动走的是 `Move()` 的 else 分支，
那一支此前**什么都不放**。所以「移动也出声」需要真的去接。

**但主人的意图（不改 ini 写法）被采纳了**：把移动那一声做成「**卡上写了 `playEffect` 就用它**」。
于是喀秋莎只要写一行

    playEffect = sfx(katyusha_into_pos)

**进场和移动就都响它**——一行覆盖两个时机，确实不用新增任何 ini 语法。
`PlayPlaneMoveEffect` 随之改名为 `PlayMoveEffect`（它现在不只服务飞机），规则变成三条：

1. 卡上有 `playEffect` → 用它；
2. 没写 + 飞机/轰炸机 → `plane_flyby`；
3. 其余不播。

攻击音则是零代码：`attackEffect` 挂上去即可（多特效本来就支持）。
**主人随后自己把 `attackEffect` 从 `bullet,sfx(...)` 改成了 `sfx(...)`** ——
即喀秋莎开火只响炮声、不出子弹视觉。测试相应改成只断言「带了这条音效」，不锁死整串值。

**机枪声**：主人选「不要」——火箭炮配机枪「哒哒」是串味。做法是把 **`sfx` 也加进
`NoFiringSoundNames`**：**攻击特效里带了自定义音效（`sfx(...)`）就不再叠通用开火声**。
影响面只有喀秋莎自己（此前没有别的卡在 `attackEffect` 里用 `sfx`）。
注意 `strafe` 仍然**不在**名单里——它打的就是子弹，那声机枪正是它要的。

### 第十五轮调整（抽卡音）

需求：新增 `Draw_One_A`~`E` 五条素材，抽卡时播放。

**随机不用写代码**：`[sfx]` 段本来就是「逗号分隔、每次随机抽一条」，所以 `draw` 槽位里
五个文件一列就有了变化感。实机跑过 12 次抽签，抽到了 4 个不同文件，确认生效。

**难点在挂载**：`Player` 里有**四条各自独立的抽牌实现**——

    DrawCard()            （主路径：回合抽牌 / 起手 / 换牌 / drawCard 指令都走它）
    DrawCardsWithName()   （自己的循环）
    DrawCardsWithType()   （自己的循环）
    DrawUnitCards()       （自己的循环）

四条都以 `deck.Remove*` 开头，下半截看着一样、其实有微妙差异（爆牌那条一个走
`CardDiscardAndRemove`、三个走 `DiscardCard + RemoveCard`）。**没有合并它们**——
合并是行为改动，不属于「加一声抽卡音」的范围；只在 `battlefield_` 加了一个公共入口
`PlayDrawSound()`，四处各调一次。

测试里那条结构检查值得记：**每一处 `deck.Remove*` 之后 160 字符内都必须有
`PlayDrawSound()`**。以后谁新加第五种抽法却忘了挂音，这条会红——不然那种抽法就是静默没声。

### 第十六轮修正（喀秋莎的机枪声为什么没被静音）

主人问「为什么喀秋莎还是播放了 bullet 音效」。是**上一轮埋的 bug**，见 `BUGS.md` #56：

第十四轮给 `NoFiringSoundNames` 加了 `"sfx"`，但 `ReplacesFiringSound` 的判定是
`NoFiringSoundNames.Contains(raw.Trim())` —— 名单里是**特效名** `sfx`，
而卡上的分段是 **`sfx(katyusha_fire)`**（带参数），整串永远匹配不上。加进去那天就没生效过。

修复是判定里先用 `ParseName` 拆掉参数再查名单。

**更值得记的是为什么测试没拦住**：那条断言是查**源码文本**的
（`'"sfx"' in 名单那一段`），只能证明「名单里有这个字符串」，**证明不了判定会命中**。
现在换成**行为测试**：把名单从源码里读出来，在 Python 里按同样规则重放六种输入——

    bullet                      -> 保留机枪声
    bullet,sfx(katyusha_fire)   -> 静音   ← 漏的就是这条
    sfx(katyusha_fire)          -> 静音
    flying / airstrike          -> 静音
    strafe                      -> 保留机枪声

并加一条「旧的拿整串去查的写法已消失」。**这类「A 是不是 B 的子串」的判定，
文本断言等于没测。**

### 场景里 export 的值要带中文注释

`.tscn` 的导出值在 Inspector 里只显示英文字段名，看不出含义，所以约定在**上一行**写一行
`;` 中文注释。分号注释必须单独成行（写在属性行尾会被当成值的一部分）。

测试把这条约定钉死了，而且**不写死字段名单**——它先解析脚本里 `[Export]` 的声明，
再逐项核对场景：

1. 每一项都得在场景里**显式赋值**（缺了就在 Inspector 里看不见，也谈不上注释）；
2. 每个赋值行的**正上方**必须是一行带中文的 `;` 注释。

覆盖 `effects/flying_effect.tscn`、`effects/bullet_effect.tscn`、`effects/bombing_effect.tscn`
（后两者共用 `BulletEffect` 脚本）。这样以后新增 Export 忘了写注释或忘了写值，测试会直接点名。

> 反向验证过一次：故意删掉 `; 摆动几个来回` 后测试立刻报 `没有中文注释: SwayCycles` 并失败；
> 补回来后恢复全绿——确认这条守卫不是空跑。

### 第十七轮（坦克炮弹：TankAttack + 分档开火音 + 抵达后的命中音）

需求（主人的原话，分两次给）：

> 新增了 tank_projetile.png 和一系列坦克开火音效
> 1) 新增一个类似于航弹的开火动画 2) 播放速度更快 3) 图片用刚给的 4) 适当大一点
> 5) attackEffect 可以填 TankAttack，或者坦克类单位不写 attackEffect 默认用这个

> 抱歉 修改一下指令 **impact 是抛射体动画到达目标位置之后播放的** 我又新增了一系列火炮音效
> **火炮单独用自己的**

**第一版理解错了**：把 `impact` 当成了「轻型的开火音」（因为坦克只有 medium/heavy 两套
开火素材），于是凭空拼出一个 `tank_cannon_small` 槽位去指 impact 的三条。实际是**时机**问题，
不是档位问题——`impact` 是**弹体落地那一声**。两轮的实现放在一起看，差别只在「这一声什么时候响」。

**它响在哪，决定了槽位怎么传。** `BulletEffect.PlayAndReleaseBullet` 里那句
`await bullet.Play(positions, time);` **正是在弹体飞抵目标那一刻返回的**，
所以命中音挂它后面就行，不必往 `Bullet` 里塞回调。

但**槽位从哪来**成了问题：口径取决于**攻击者**（火炮按身材三档、坦克不分档），
而特效自己不知道是谁打的。场景 Export 只能写死一个，那就得给每个口径各做一个场景。
于是走了**特效名的参数**：`attackEffect = TankAttack(artillery_large_impact)`，
由 `battlefield_.TankAttackEffectFor()` 算好拼进去，`BulletEffect.Configure` 拆出来。

**这套机制历史上被整段删过一次**（航弹连发时每发各炸一声会糊成一片，见第十四轮）。
它现在回来了，因为**问题从来不是这个能力本身、而是默认值**：航弹不该响，坦克炮弹该响。
所以测试钉的不是「代码里没有音效」，而是 **`Configure` 里「不传参数就直接 return」那一句**
——`bullet` / `bombing` 一个播放器都不建，结构上仍然无声。

**判档抽成了 `SizeTier(card)`**：进场音、开火音、命中音三处共用同一个判据，边界只写一遍。
这次改动让原来散在两处的 `size <= DeploySoundSmallMax ? ... : ...` 收成了一处（规范 E）。

**28 张坦克/火炮卡的 `attackEffect = bullet` 已清空**，改吃默认——新卡不写也不会漏。
喀秋莎是**刻意的例外**：它的 `bullet,sfx(katyusha_fire)` 是火箭炮的组合，保留原样。
测试因此**按 `fullmatch("bullet")` 判**而不是 `startswith`，否则喀秋莎会被误伤。

新增 `tests/verify_tank_cannon.py`（90 项），三档都覆盖：

- **冒烟**：两个新场景存在、`effect` 注册表登记了 `TankAttack`、贴图带 `.import`；
- **基本**：炮弹 0.6s 比航弹 1.5s 快（需求②）、固定 1 发、默认特效只在没写时才生效；
- **边界（白盒）**：把常量读出来在 Python 里**重放一遍分档规则**，
  1/1、2/2、3/3（都在 ≤4 或 ≤8 里）、5/5（刚过 8）逐个验；
  **每张坦克/火炮卡按它自己的 attack/defence 算出的两个槽位，都必须真的在 `[sfx]` 段里、
  文件存在、`.wav.import` 也在**——拼错一个就整批卡静默没声。

> 踩过的坑：`card.ini` 是 CRLF，而 `^attackEffect\s*=\s*(.*)$` 里 `\s` **连换行一起匹配**，
> 于是空值那一行会把**下一行的 `键 =`** 当自己的值（`attackEffect` 读成 `"description ="`）。
> 判断「这行是不是空的」必须用 `[ \t]*`。这个坑同时出现在新写的解析和一条改写的断言里。

> 实机等价验证用临时 GDScript 跑过（跑完即删）：`Configure("")` 建出 **0** 个 `AudioStreamPlayer`、
> `Configure("artillery_large_impact")` 建出 **4** 个且 bus 都是 `&"SFX"`；
> 9 个新槽位逐个 `PickSfx` 都取到了 `AudioStream`。

### 第十八轮（飞机部署/移动音 + 按键音）

**需求一**：主人给了两组素材——`AU_depl_Fighter_small_01~02`（部署）与
`AU_Flyby_Fighter_small_v2_01~02`（移动）——要求「所有战斗机的移动和部署音效改成这两个，
同类型的随机播放」；随后补一句「**轰炸机也用这两个**」。

素材本来就是按**时机**分的（depl / flyby），所以拆的是「部署」与「移动」这两个时刻，
而不是兵种。第一版按兵种命名成 `fighter_*`，加上轰炸机之后这个名字就不准了，
于是改为按**时机**命名，与素材文件名一一对应：

    plane_deploy  ← AU_depl_Fighter_small_*
    plane_flyby   ← AU_Flyby_Fighter_small_v2_*   （复用已有的键，语义本来就叫「飞过」）

**「同类型随机播放」不用写代码**：`[sfx]` 段本来就是「逗号分隔、每次随机抽一条」。
实机验证跑了 60 次抽取，两个槽位都各抽到 2 个不同文件。

「哪些兵种算飞机、这一刻放哪条」抽成了 `PlaneFallbackEffect(card, forDeploy)`——
部署走 `DeployMoveEffect`、移动走 `PlayMoveEffect`，两个调用点共用同一张表。
13 张飞机卡**没有一张写了 `playEffect`**，所以改兜底就全覆盖了；测试也钉住了这一条
（有卡自己写了 `playEffect` 就会绕过整张表，改兜底等于对它无效）。

**需求二**：新增素材 `General_button2`，世界地图界面所有按钮 + 战斗界面「下一回合」按钮
都用它当按键音。

**分工是这次的关键**：「哪些按钮要响」是 **UI** 的事，「怎么响」是**音频**的事，所以分两层：

- `bin/UiClickSound.cs`——薄门面。`AttachAll(root)` 递归找 `BaseButton` 把界面里
  所有按钮一次挂完（世界地图的 7 个区域按钮是运行时按名字前缀找出来的，
  写死名字的话以后加个按钮就静默没声）；`Attach(button)` 挂单个。
  带 `HasMeta` 记号防重复挂载——挂两次就是两声。
- `MusicManager.PlaySfx(slot)`——**放一次就完**。声部挂在 **autoload** 上而不是调用方场景里：
  按键音最典型的用法就是「按下去 → 立刻切场景」（点区域按钮就进战斗），
  挂场景里节点会跟着 `QueueFree`，声音刚起个头就被掐掉。

> 写完发现 `MusicManager.cs` 涨到 **306 行、破了 300 行红线**（规范 B），于是把声部池
> 拆成 `core_logic/SfxPlayer.cs`（宿主 + 一个「槽位 → 音效」的委托，就是 `PickSfx`）。
> `MusicManager` 退回 **275 行**，只剩一句 `PlaySfx(slot) => sfxPlayer?.Play(slot);`。
> 拆分后重跑实机验证：`MusicManager` 下 5 个播放器（1 个 BGM + 4 个声部），
> 按钮音照常响。测试里顺手钉了一条 `MusicManager.cs < 300 行`——不然下次一加功能又涨回去。

实机（headless 起真 `worldMap.tscn`）验证：场景里 **9 个按钮**，挂完后
**带记号的也是 9 个**；真按一下，`MusicManager` 上在播的声部从 1 变 2；
直接调 `PlaySfx` 再到 3；传一个不存在的槽位则仍是 3（不崩、不误播）。

`tests/verify_button_animations.py` 从 12 项扩到 25 项：新增按键音的挂载点、
防重复记号、声部池、SFX 总线，以及**槽位 → 文件 → `.import`** 的交叉核对
（少了任何一环都是静默没声，不查就只能靠耳朵发现）。

> 顺带记录一个现象：在合成测试里 `worldMap._Ready()` 会启动**线程化预加载**
> （`SceneLoader` 用 `ResourceLoader.LoadThreadedRequest`），随后日志里出现
> `Parse Error: Failed [bin/cardbase.tscn:611]` 与 `store.tscn:36`。
> 这两个场景**单独加载完全正常**（实测四个场景逐个 `load()` 全部 OK），
> 报错行都是 `script = ExtResource(...)`。与本轮改动无关，也没有定位到根因，
> 先记现象、不动代码。详见 `BUGS.md` #57（未定位）。

### 第十九轮（坦克炮弹：炮口/落点烟 + 发光 + 大幅提速）

需求（主人原话）：

> 坦克的动画有问题 请拿烟雾修改一下 在坦克炮发射和落点的位置都新增一个小型烟雾
> 并让坦克炮像子弹一样发光 再让坦克炮弹飞行速度大幅加快

**发光不是「调亮一点」，是 HDR**：战场里 `bin/battleField.tscn` 开着
`glow_enabled = true` / `glow_hdr_threshold = 0.9`。`bin/bullet.tscn` 的根节点写着
`modulate = Color(18.59, 18.89, 0, 1)` —— 把贴图色推到远大于 1 越过阈值，才有 bloom。
坦克炮弹缺的就是这两行，于是**逐字照抄**（不是「调成差不多的黄色」：
抄的是机制，值也一模一样，以后一眼能看出两者是同一套）。

> 顺带查清了一件事：`bullet.tscn` 同时写了 `modulate` 与 `self_modulate`，
> 但 `self_modulate` 只管**节点自身**的绘制，而根节点是个不画东西的 `Control`，
> 所以真正起作用的是 `modulate`。照抄时两条都留着，是为了两个场景能对得上。

**两根烟挂在哪，是这次唯一的设计点**。「炮弹抵达那一刻」只有 `BulletEffect` 知道
（`await bullet.Play(...)` 正是在那一刻返回的）——与上一轮的命中音同一个道理：

    炮口烟：Play 开头、**生成弹体之前**  → 不 await，与第一发同时跑
    落点烟：await Task.WhenAll(tasks) 之后 → **await 它**

落点烟必须 `await`：不 await 的话 `RunEffect` 的 finally 会立刻回收整个特效，
而烟是这个特效的子节点，会被一起掐掉半截。

两个烟都是 `BulletEffect` 的 Export、**默认留空 = 不冒**，只有 `tank_attack_effect.tscn`
填了值 —— 与命中音同一套「默认关闭、按需开」，所以 `bullet` / `bombing` 两端依旧干净。

**规范 A 的一次实际应用**：`AirStrikeEffect` 里**已经有**一段「取子特效 → 播 → 回收」
（Create → AddChild → PrepareForUse → Play → Release）。所以没有新写第二份，把它提到
`Effect` 基类当 `PlayChildEffectAsync(name, positions, count)`，攻击机与炮弹共用。
`AirStrikeEffect` 那边从 28 行缩成一行转发。四条原本钉在 `AirStrikeEffect` 内部的断言
改成钉**基类**（更强的说法：全项目只有一份实现），另加一条「AirStrikeEffect 里已经没有
第二份 `EffectRegistry.Create` / `Release`」。

**「小型」是 Export，不是第二个类**：`SmokeEffect` 加 `[Export] float SizeScale = 1f`
（默认 1 = 阵亡烟大小不变），新场景 `effects/smoke_small_effect.tscn` 填 0.3。
乘在 `(1 + frame * 0.2)` **之上**，所以放大过程本身的速度不受影响。

**速度**：`ProjectileFlightSeconds` 0.6 → **0.25**（子弹是 0.3 —— 现在炮弹比子弹还快）。

验证（临时 C# 场景，跑完即删；`Play` 的入参是 `IReadOnlyList<Vector2>`，
GDScript 传给 `call()` 会编组失败，所以这次用 C# 写临时测试）：

    smoke_small 注册表命中 = True
    smoke_small.SizeScale = 0.3 / smoke.SizeScale = 1          <- 既有烟没被改到
    tank_shell modulate = (18.592834, 18.892157, 0, 1)         <- 与子弹逐字相同
    tank_shell 的 Sprite2D modulate = (1, 1, 1, 1)             <- 炮弹保持不透明
    MuzzleEffect = 'smoke_small' / ImpactEffect = 'smoke_small' / FlightSeconds = 0.25
    跑完全程，见到烟的落点: (100,200) (500,300)                <- 正好两个，位置全对
    await 返回那一刻的子节点: 4 个 AudioStreamPlayer（命中音声部，上一轮的）

> 最后那行一开始看着像泄漏（期望 0 个子节点）。查下来是**命中音的 4 个声部**——
> 它们是 `Configure` 建的、随特效一起销毁，本来就该在。炮弹与两个烟都已回收。

`tests/verify_attack_effects.py` 179 项：新增第 ⑦ 节钉住上面每一条，并把两个烟雾场景
也纳入「每个 Export 都要有中文 `///` 说明 + 场景里的属性名必须真实存在」那条守卫 ——
为此给 `ENGINE_PROPS` 补了 `texture` / `hframes` / `vframes` / `scale` / `position` /
`rotation` / `modulate`：那张白名单是**全文件**扫的，而场景里除了挂脚本的根节点
还有子节点（`SmokeSprite` 就写了这几个），它们是子节点自己的引擎属性。

### 第二十轮（按键音铺开到各界面 + 事件界面进不去的修复）

主人的六条：

> 1) 主界面按钮也有音效 2) 战斗选择界面选择战斗也应该有音效 3) **现在无法进入事件界面了**
> 4) 商店界面内部应该有音效 5) 卡组的返回按钮应该有音效 6) 战斗内的查看卡组和返回按钮应该有音效

#### 先修第 3 条：这是阻断性问题

**先复现，再改**。临时 C# 场景起真 `worldMap.tscn`，点 `area1` 开面板，
用反射读出 `ChooseMission._entries` 挑出事件那一个、反射调 `OnChoose(index)`
（走真路径，不是自己造一个假场景）：

    修复前  第 0 帧 任务面板还在=False  EventScene=<没有>
            事件界面**从头到尾没出现在树里**
    修复后  第 0 帧 任务面板还在=False  EventScene 在树里=True
            事件界面父节点 = Control2（世界地图根节点）  0.6 秒后仍在 = True

根因：`EventScene.Show` 把叠层挂到 **parent**（=任务选择面板）上，下一句
`EnterEventOverlay()` 就把那个面板 `QueueFree` 了 —— 同一次调用里先挂上去、再让宿主消失。
连带两个症状：`ExitEventOverlay()` 写在 `await Run(...)` 之后、永远执行不到，
`_eventOverlayActive` 卡在 `true`，**地图此后点不动**（这就是「无法进入」的另一半）；
末尾 `CampaignVictory.ShowAndReturnToMenu(parent)` 传的是已死节点，
会被存活判定挡掉、**通关界面整段静默跳过**。

**先证明不是本轮改出来的**：把上一轮加的 `UiClickSound.AttachAll(this)` 整段停用再跑，
现象一模一样 → 根因在 `7c5d076`，与本轮无关。这条很值得做，
因为主人说的是「**现在**无法进入了」，很容易被当成自己刚改坏的。

#### 再把按键音铺开

`bin/UiClickSound.cs` 早就留好了 `AttachAll(root)`（递归）与 `Attach(button)`（单个），
所以这一轮**每个界面只加一行**：主菜单、任务选择面板、商店、卡组查看器各一句
`AttachAll(this)`。战斗里两个按钮是 `_Ready` 之后才有的（「下一回合」在场景里、
「卡组」是代码 `new` 的），所以在**创建处** `Attach`。

商店的「点卡片购买」不是 `Button` 而是 `_Input` + 矩形命中，`AttachAll` 扫不到，
单独在命中判定之后补一声 `Play()`。

`DisplayCard` 是**世界地图与战斗共用的**卡组查看器，所以第 5、6 条说的「返回」
其实是同一个按钮，挂一次两边都覆盖。

#### 测试

`tests/verify_button_animations.py` 25 → **44** 项。除了逐个断言挂载点，还加了一张
**「场景 与 挂载点」的对照表**：对每个界面同时断言「场景里确实有按钮」与「脚本里确实调了
`AttachAll`」——只查其中一个都是空跑（前者保证断言不是对着空气，后者才是真要求）。
加了新界面忘了挂，这条会直接红。

**未能在合成测试里验证的一处**：`store.tscn` 只要 `add_child` 就让进程静默死亡
（`load` + `instantiate` 都正常，只是不进树）。**已确认与本次改动无关** ——
把 `Store.cs` 的两处改动整段撤掉再跑，一样死。真机商店是能用的，所以更像是合成场景
本身的问题，记在 `BUGS.md` #57 里，**商店那三个按钮需要主人实机确认**。

### 第二十一轮（研发卡语音：三档 + 两张终极卡）

主人给了 5 个素材，对应关系是：

    MACHINE_Air_Compressor_100L_5_5HP_Stop_stereo.wav  初级研发（美国/苏联/皇家）
    TOOL_Wrench_Long_RR1_stereo.wav                    2 级研发（扩展 ×3）
    TOOL_Wrench_Long_RR2_stereo.wav                    3 级研发（高级 ×3）
    AU_StalinsOrgan_03.wav                             斯大林管风琴
    AU_Order_ManhattanProj_02.wav                      曼哈顿计划

**一行 C# 都没改**。这正是规范 A 想看到的：卡牌语音那一套（`playEffect = sfx(槽位名)` +
`[sfx]` 段配文件）早就有了，加新语音本来就是「配置两处」的事。9 张研发卡 + 4 张终极卡
（含两个 cost 0 衍生卡）插一行 `playEffect`，`[sfx]` 段加 5 行，收工。

**素材是按档位给的，不是按国家给的** —— 所以三家共用一条槽位，而不是一家一条。这一点
如果按「美/苏/英 各一条」去写，配置会变成 9 条槽位指向 3 个文件，是同一个数据配了三处。

**档位怎么机器校验**：卡名里看不出档位（三家叫法还不一样：军事研发 / 扩展 / 高级），
唯一统一的判据是卡面 `price`：3 = 初级、6 = 2 级、9 = 3 级。测试按这张表**反查**
每一张研发卡，填错档会直接红：

    [PASS] 美国军事研发（cost 3）→ research_1，与档位表一致
    [PASS] 扩展军事研发（cost 6）→ research_2，与档位表一致
    [PASS] 高级苏联研发（cost 9）→ research_3，与档位表一致
    … 共 9 张

**两张终极卡为什么要连衍生卡一起配**：`makeKatyusha` / `makeManhattan` 是 cost 0 的
「加入手牌」用卡（`AddToHand(斯大林管风琴,3)` / `Choose(makePenicillin,makeManhattan)`），
显示名与本体**逐字相同**。只给本体配的话，同一张卡从两条路进手牌，打出来会响两声不一样的。

**动的文件的写法**：`cards/card.ini` 插在 `targetType` 之后、`traits` 之前，
`playEffect  = ` 两个空格对齐（与既有 10 张逐字一致）；**保持无 BOM + CRLF**
（NOTICE.md 里记过：用 `utf-8-sig` 写回会加 BOM，一次弄红两个测试）。
插完实测：无 BOM、CRLF 2753 = LF 2753、186 个段落、23 张卡有 `playEffect`（10 旧 + 13 新）。

验证：

- 实机逐个 `PickSfx`：5 个槽位全部取到对应 `AudioStream`
- `tests/verify_card_voice.py` 302 → **359** 项（新增研发档位反查 + 5 个槽位的文件与 `.import` 核对）
- 全量回归：52 脚本全绿，8 个既有失败脚本与基线一致，无新增失败

### 第二十二轮（毛驴/伊尔2M 的攻击提升成飞机兵种默认）

需求：把毛驴的 `strafe` 与伊尔2M 的 `airstrike` 从「这两张卡各自的配置」提升成
**战斗机 / 轰炸机的默认攻击动画**。

做法跟坦克那一轮同一个套路（`ResolveAttackEffect` 的兜底从「只有坦克/火炮」抽成
按兵种查的 `DefaultAttackEffect`），13 张飞机卡的 `attackEffect` 全部清空、吃默认：

    战斗机 Plane   -> strafe     （起飞即扫射 10 发 → 悬停 → 降落）
    轰炸机 Bomber  -> airstrike  （起飞即投弹，弹数 = 攻击力 → 悬停 → 降落）
    坦克/火炮      -> TankAttack(命中音槽位)    <- 没被碰到
    步兵/指令/总部 -> null（不播特效）

顺带把毛驴/伊尔2M 卡上那两行也删了 —— 它们现在**就是**默认值，留着是同一数据配两处。

**这次最容易弄坏的地方是开火声的联动**（上一轮实机调过两轮，方向还是反的）：

    Plane  -> strafe      不在静音名单里 -> **保留那声机枪「哒哒」**
    Bomber -> airstrike   在静音名单里   -> 只有飞掠声

所以第 ④b 节把这两条**一起**钉住了，而不是只断言默认动画是什么。

**实机验证**（临时 C# 场景 + 反射调私有静态 `DefaultAttackEffect`，跑完即删）：

    Plane      -> 特效 strafe       | 放通用机枪声
    Bomber     -> 特效 airstrike    | 静音（特效自带音）
    Infantry   -> 特效 <null>       | （不播特效）
    Command    -> 特效 <null>       | （不播特效）

用反射是因为 `DefaultAttackEffect` 是私有静态、而 `Attack()` 那条路要起整个战场；
拿一个只设了 `cardType` 的空 `cardBase_` 直接调，是这里最轻的真实验证。

**踩到的坑**：改 `card.ini` 的那段脚本用 `re.subn(r'^attackEffect[ \t]*=[ \t]*[^\r\n]*', ...)`
—— 第一版写的是 `.*`，而 `.` **会连行尾的 `\r` 一起吃掉**，替换后那几行只剩 LF。
写完的 `assert out.count('\r\n') == out.count('\n')` 挡住了，文件没被写坏，改成
`[^\r\n]*` 才对。（同一个坑在第十九轮读 `card.ini` 的正则里也踩过一次。）

**另外发现**：主人自己往 `card.ini` 加了 21 张指令卡的 `playEffect`（第227号命令、乌拉、
间谍组织、黑夜女巫……），都在，没被这轮的脚本碰到。其中 **`间谍组织`（cost 2）配的是
`sfx(research_2)`** —— 它不是研发家族（美/苏/皇家的 初/扩/高级），看着像填错了，
已报给主人、没有擅自改。

测试也跟着放宽了一处：「接线的卡数与预期一致」原本是**相等**判定，主人随时加语音就会
每次弄红；改成**包含**（点名的必须接对 + 接的槽位必须存在）。另外研发档位反查原来用
「槽位以 `research_` 开头」挑卡，会把 `间谍组织` 误判成研发卡，改成**卡 id 白名单**。

### 第二十三轮（存档 / 读档 + 两个界面的暂停菜单）

需求（老板分两段给的）：

> 战斗界面按 ESC 或点左上角设置 → 菜单：A 调音量 B 认输（总部血量归零）C 保存并退出。
> 点 C 保存当前战斗的 id（如 berlin）；进游戏点 Continue 有档就直接进对应的战斗。
> **存档还要同时保存 worldMap 的状态。**
> worldMap 同样位置也有设置按钮 → A 调音量 B 放弃（失去存档回主界面）C 保存并退出。
> 点开始的时候，如果存在可读的存档，先问是否覆盖；是 → 删档，否 → 退回主菜单。

#### 两个先说清楚的取舍

1. **读档 = 用同一个敌人重开这场战斗**，不还原战斗内的棋盘（手牌/场上单位/指挥点）。
   老板原话是「保存当前的战斗的 id」，就按字面做——还原棋盘是另一个量级的工作。
2. **不存「当前抽到的三个任务」**。它是 `WorldMap` 的瞬时交互状态（`_drawnArea`/`_drawnIds`，
   关面板或离开地图就作废）。真正的进度——烈度、解锁、血量、物资、卡组、商店——**全存**。

#### 结构上的三处「只写一份」

- **存档**：`SaveManager` 一处读写，两个界面都调它，不各写一份序列化。
- **二次确认**：`UiConfirm.AskAsync`。第一版我在 `PauseMenu` 与 `StartMenu` 各写了一份
  `TaskCompletionSource`，写完自己发现是两份实现，抽出来只留一处。
- **音量滑条行**：`SettingRow`。原先 `SettingsMenu.AddSlider` 是 private static，
  暂停菜单要用就只能抄——抽出来两边共用。

「认输」也刻意**不另写**一套失败流程：把玩家总部防御打到 0 + `await CheckIfAnyUnitDiedAsync()`，
剩下的交给既有的 `RemoveCard(myHq)` 链路（扣血规则 / 结算面板 / 血尽重置）。
复制一套的话，以后改血量规则就会漏掉认输这条路。

#### 实机验证：一次真实的存 → 清 → 读往返

临时 C# 场景（跑完即删）造了一个有辨识度的局面，存盘、`ResetCampaignProgress()` 抹干净、再读回来：

    删档后 HasSave = False；存档后 HasSave = True
    清空后：敌人=berlin 血量=5 物资=0 卡组=0 烈度area3=-1 货架=-1      ← 确实清干净了
    读档返回 = True
      敌人/区域/战役模式 = berlin / area3 / True
      血量/物资         = 4 / 123
      卡组              = t70,su85,is2
      解锁              = a1:1 a2:1 a4:0
      烈度              = a1:-1（0 不该被存） a2:1 a3:3
      商店队列          = kv11934,de_tiger
      货架              = A152/42/折True/售False、b29/77/折False/售True
      读档后该进        = res://bin/battleField.tscn（战斗中存的）
      世界地图存的      → res://bin/worldMap.tscn

暂停菜单也一并验了：`Show` 返回菜单、`IsOpen` 正确、**重复 Show 被拒**（不会叠第二个）、
4 条音量行、按钮文案 = 认输 / 保存并退出 / 返回游戏、`Close` 后 `IsOpen=false`、`onClosed` 被调到。

> 第一次跑时「按钮数 = 1」看着像 bug —— 其实是**测试写法不对**：我直接 `AddChild(menu)`，
> 没走 `Show()`，于是 `_action1`/`_action2` 都是 null。改成走真入口就对了。
> 另一处是真 bug：`pause_menu.tscn` 的层级是 `Panel/Margin/Rows/...`，
> 而我代码里取的是 `Panel/VolumeRows` —— 写错取节点路径是**运行时才炸**的空引用，只能靠实跑发现。

`tests/verify_save_load.py`（60 项）把上面这些连同「三处只写一份」一起钉住；
`tests/verify_volume_settings.py` 的那条滑条断言改为指向共享的 `SettingRow`。

### 第二十四轮（`convert(id)` 转换指令 + 翻面动画）

需求：新增 `convert(id)` key，目标被转换成 id 指代的单位；过程播放动画
① 轻微浮起（类似战斗机起飞）② 翻面（露出卡背）③ 再次翻面、回来时已经是新单位；
老板新加了两张卡背，需要露卡背时就用它们。

#### 两个技术选择

**翻面不用 shader**。老板说「可以考虑上网找个 shader」，但 2D 卡牌绕竖轴翻转的标准做法
就是把 `Scale.X` 走 1 → 0 → 1——`X = 0` 那一刻正好是「侧对屏幕」，在那时换掉正面内容就是翻面。
shader 版能看到透视，代价是引 GLSL、还得赌它不会因为编译失败而**静默什么都不发生**。
所以选了压扁方案，翻面期间只动 X、Y 保持飞掠抬起后的值。

**动画继承 `FlyingEffect`、只覆写悬停段**。浮起/落回/还原 Scale·Rotation·ZIndex/
`isUnderCardEffect`/`_displayOrderDirty` 全部沿用父类——与 `AirStrikeEffect` 覆写
`DuringRiseAsync` 是同一个套路。测试专门断言「没有把 Rise/Land/Sway 抄一份过来」。

#### 卡背

两张卡背是**按阵营**分的新素材（`assest/苏联卡背.png` 1024²、`assest/德国卡背.png` 512²），
比卡（180×240）大得多，所以不能直接换 `cardbase` 那个 Sprite2D 的贴图（会撑成 1024px）。
做法是在 `cardbase.tscn` 里加一层**铺满整张卡的 `TextureRect`**（`convertBack`，默认隐藏）：

- 只加**一层盖子**、而不是逐个隐藏卡面节点——卡面上有 icon / name / attack / defence /
  cost / description / country / unitType 八九个，一个个藏既啰嗦又容易漏（漏一个就从卡背透出来）。
- `StretchMode = KeepAspectCovered` + `ExpandMode = IgnoreSize`：铺满、不变形、不溢出。
- 按**这张卡属于哪一方**选图（我方苏联、敌方德国），每次都重新选——卡是从对象池复用的，
  上一轮可能属于另一方。

#### 实机验证（临时 C# 场景，跑完即删）

    cardbase.tscn 有 convertBack = True
      读回来的 StretchMode = KeepAspectCovered  一致=True     <- 枚举值是我从文档猜的，实测确认
      读回来的 ExpandMode  = IgnoreSize         一致=True
      尺寸 = (180, 240)   默认 Visible = False
    转换前：id=t70 攻/防=2/2 名=T-70 卡背显示=False
      第一次见到卡背：贴图=res://assest/苏联卡背.png 缩放X=0   <- 正好在压扁到 0 那一帧盖上
    [Convert] t70 → is2
    过程中露出过卡背 = True，持续 96 帧
    转换后：id=is2 攻/防=8/8 名=IS-2 费=10 卡背显示=False
      Scale 还原=(1,1)   isUnderCardEffect 已复位=False
    敌方卡背 = 德国卡背 / 我方卡背 = 苏联卡背
    id 不存在 → 只报警，id 仍是 t70，卡背已收

> 「枚举值是我猜的」这条值得单列：`NOTICE.md` 记过「`.tscn` 里枚举值写错是**静默生效**的」，
> 所以 `stretch_mode = 6` / `expand_mode = 1` 不能靠眼力，必须读回来比对。实测两个都对。

#### 指令侧

`convert(id)` 照 `Heal(n)` 的同一套写法加在 `ParseAndExecuteEffect` 里（取括号里的参数、
对 `targets` 批量作用），`timesList.ini` 的 `[keys]` 与 `ConsoleCommands` 同步登记。

**指令必须 await 动画**：换卡发生在**第二次翻面那一刻**（只有动画知道那个瞬间），
不 await 的话 `convert(x)|GetAttack(2)` 会把 +2 加在**旧卡**上、然后被转换整个盖掉——
不报任何错，只是数值凭空少一截。所以这里用了 `EffectRegistry.PlayOnceAsync`（**等**的版本），
而不是攻击特效那个 fire-and-forget 的 `StartEffect`。

> 顺带把「取→挂→传参→播→还」收成了 `EffectRegistry.PlayOnceAsync` 一处实现：
> 攻击机的子特效、炮弹的发射/落点烟、`convert` 指令三个调用方共用，
> `Effect.PlayChildEffectAsync` 退化成一层转发。这是规范 A 那句
> 「先找已有功能、适合改造就改造」的又一次落地。

#### 踩到的坑

- 测试里那条「没有引入着色器」一开始是红的——因为**它撞上了我自己写的注释**
  （`///` 里写着「翻面不用 shader」）。剥掉注释再查才对，与 `verify_attack_effects`
  的 `code_only` 是同一个坑。
- 上一轮把 `PlayChildEffectAsync` 改成转调之后，`verify_attack_effects.py` 里
  按函数体切片的断言直接 `ValueError` 崩了（**没有输出**，不是失败）。已改指 `PlayOnceAsync`。

`tests/verify_convert.py`（42 项）把上面这些连同「换的是同一张卡」「行动次数不刷新」
「总部拒绝转换」「id 不存在只报警」一起钉住；`effects/convert_effect.tscn` 也纳入了
`verify_attack_effects.py` 的 Export 中文说明守卫。

### 第二十五轮（卡背拉伸填满 + 翻面时收起效果浮标）

老板两条反馈：

> 1) 请注意我给的卡背应该被拉伸到符合卡牌大小的尺寸
> 2) 翻面的时候应该不显示效果浮标

**① 拉伸**：上一轮用的是 `StretchMode = KeepAspectCovered`（保持比例、裁掉多余）。
卡背是**方图**（苏联 1024²、德国 512²）、卡牌是 **3:4**，保持比例就只铺一部分、上下留白。
改成 `StretchMode = Scale`（`stretch_mode = 0`）+ 原有的 `IgnoreSize`：拉伸填满 180×240。

**② 效果浮标**：「浮标」= `cardBase_._attrPanel`（卡右侧 `(155,30)` 的属性图标面板）。
它**不是被盖不住**，是**画在卡背上面**——`_attrPanel` 是**运行时 `AddChild`** 的，
而 Godot 同 ZIndex 下按**子节点顺序**绘制、后加的排后面，所以它天生压在场景里的
`convertBack` 之上。修法是两件事一起做：

- `MoveChild(back, GetChildCount() - 1)`：把卡背挪到最后一个子节点，从根上解决
  「以后再有运行时 AddChild 的东西盖上来」；
- 显式隐藏 `_attrPanel` 与它的悬停提示（老板要的是「不显示」，不只是「被盖住」——
  浮标还在卡右侧多探出一点点，光靠盖也不干净）。

**刻意没做的事**：不给 `convertBack` 加大的 `ZIndex`。Godot 的 `z_index` 是**相对父节点**的，
给子节点加 40 等于把卡背抬到「卡自己的层级 + 40」，会越过手牌的 20 ——
正是 `NOTICE.md` 层级表明令避免的那类 bug。挪顺序能达到同样的遮挡效果，层级却留在卡自己那一层。

实机验证（临时 C# 场景，跑完即删）：

    StretchMode = Scale（期望 Scale）  一致=True        <- 枚举值又是读回来确认的
    ExpandMode  = IgnoreSize
    效果浮标建出来了 = True（卡有特性）
    露卡背时：卡背 Visible=True；浮标 Visible=False
      子节点顺序：卡背 index=16，浮标 index=14（卡背 > 浮标，即画在上面）
      卡背在最后 = True
      卡背 z_index=0（跟着卡走，不比手牌的 20 高）
      卡背贴图 = res://assest/苏联卡背.png，尺寸 = 180x240
    收起后：卡背 Visible=False；浮标 Visible=True

`tests/verify_convert.py` 42 → 48 项，新增「必须 Scale 不是 KeepAspectCovered」
「卡背要挪到最后一个子节点」「浮标跟着显隐」「卡背不许加 z_index」四条。

> 顺带修了 `verify_card_effects.py` 的一条：它原来断言**指令卡里不许出现 `attackEffect` 这个键**，
> 而老板新加的测试卡 `[convertTest]` 带了一行**空的** `attackEffect =`（从别的卡复制模板带来的）。
> 空值是无操作（`ResolveAttackEffect` 拿到的还是空串），把它当失败属于假红灯。
> 改成判「有没有**值**」。
