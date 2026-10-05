# 类功能文档

## 核心游戏类

### `battlefield_` : Control (bin/battlefield_.cs)

主战场场景类，承载所有游戏核心逻辑。

| 职责 | 说明 |
|------|------|
| 输入控制 | `ForbidControl()`/`AllowControl()` 锁定/解锁玩家操作 |
| 死亡检查 | `PauseDeathCheck()`/`ResumeDeathCheck()` 暂停/恢复死亡判定 |
| 卡牌列表 | `cardInPlaces` 场上所有卡；`choiceCards` 选择界面临时卡 |
| 阵线管理 | `supportLine`(支援)、`frontLine`(前线)、`enemySupprotLine`(敌方支援) 各5格 |
| 变量系统 | `memoryVariables` 自定义内存变量；`ReplaceVariables()` &变量替换 |
| 效果执行 | `ParseAndExecuteEffect()` 效果脚本解释器（~1176行） |
| 战斗系统 | `Attack()` 完整战斗流程；`Move()` 移动逻辑 |
| 敌方AI | `EnemyPerformActionsAsync()` 敌方行动AI；`EnemyTurnAsync()` 敌方回合 |
| 敌人意图 | `LoadEnemyActionQueue()` 读取预设；`GetNextTurnActions()` 汇总下回合行动；`RefreshEnemyIntentPanel()` 按顶层逗号拆开行动行、每个带描述的段各出一行（`AddEnemyIntentRow()`）；`ParseActionMetadata()` 解析`[icon=,description=]` |
| 选择UI | `ShowCardChoice()`/`HandleChoiceCardClick()` 卡牌选择界面 |
| 控制台 | `ToggleConsole()`/`CreateConsole()` 内建调试控制台 |

**内含内部类：** `Player`、`CardMaganer`

### `cardBase_` : Control (bin/cardBase_.cs)

卡牌UI节点，同时存储卡牌数据和运行时状态。

| 职责 | 说明 |
|------|------|
| 属性管理 | attack(攻)/defence(防)/cost(费)/effect(效果脚本) |
| 特性系统 | `HasTrait()`/`AddTrait()`/`RemoveTrait()` 特性位标记操作 |
| 状态跟踪 | smokeScreen/shock/mobilize/ambushActive 运行时状态 |
| 行动计数 | moveAble/attackAble 移动和攻击次数；`RefreshUnit()` 每回合重置 |
| 动画 | `MoveToPosition()` tween移动；`DiscardCard()` 弃牌动画 |
| 闪烁效果 | `FlashAttributeWithColor()` 属性变化闪烁；`FlashTraitIcon()` trait图标闪烁 |
| 图标面板 | `BuildAttributePanel()` 构建右侧attribute图标；`GetAllAttributes()` 收集所有图标 |
| 悬停高亮 | `SetHover()`/`ResetVisualsInstant()` 悬停边框+缩放 |
| 生命周期 | `Dead()` 死亡回收；`lifeTime` 存活回合计数 |
| 尺寸钉死 | `PinDesignSize()` / `DesignSize`(180x240)：把锚点钉成左上角并固定尺寸，使卡牌的可点区域不随父节点变化。任何要把卡牌放进别的父节点（尤其 `ShowCardChoice` 的 `choiceLayer` 这种 CanvasLayer）之前都必须先调 |

### `Cardbase` : Node2D (bin/Cardbase.cs)

视觉箭头渲染器。从卡牌位置到鼠标/目标位置绘制贝塞尔曲线箭头。

| 方法 | 说明 |
|------|------|
| `_Process(delta)` | 计算起终点 + 控制点，触发重绘 |
| `_Draw()` | 绘制贝塞尔曲线填充区域 + 箭头多边形 |
| `BezierCurve()` | 贝塞尔曲线点生成 |
| `DrawSimpleCurvesFill()` | 三角形带填充两条曲线之间的区域 |

### `place_` : Node2D (bin/place_.cs)

战场位置格点。管理卡牌与此格子的绑定关系。

| 方法 | 说明 |
|------|------|
| `GetMyCard()` | 返回当前绑定的卡牌 |
| `BondCard(cardBase_)` | 将卡牌绑定到此格子 |
| `UnbondCard()` | 解绑卡牌 |
| `GetPlaceGlobalPosition()` | 返回该格子的全局坐标 |

### `Effect` : Control (core_logic/Effect.cs)

所有视觉效果的抽象基类。`Play`接收可选的全局`Vector2`位置列表和以秒为单位的可选播放时间；省略参数时由具体效果采用默认值。`EffectRegistry`将卡牌配置短名称映射到效果场景。

### `Bullet` : Effect (bin/Bullet.cs)

单发飞弹动画，从一个全局位置飞向另一个全局位置。`bullet.tscn`（机枪子弹）与 `bomb.tscn`（航弹）共用本脚本。

| 成员 | 说明 |
|------|------|
| `Play(positions, time)` | 异步动画：使用前两个位置和可选时长播放飞弹 |
| `[Export] Tween.EaseType FlightEase` | 飞行缓动。默认 `InOut`（起步慢、中间快、收尾减速）= 子弹原来的样子；`bomb.tscn` 设成 `In`（一直加速、不减速），`In` 的最快点正好是 tween 结束那一刻，而 `OnMoveFinished()` 就挂在那一刻，所以表现是「速度最大时直接消失」 |

### `BulletEffect` : Effect (core_logic/BulletEffect.cs)

「从 A 点向 B 点打出一串弹体」的效果控制器，一个脚本、两个场景：`bullet`（固定 10 发）与 `bombing`（`ProjectileCount = 0` 表示用调用方给的弹数，即攻击力）。每发弹体飞完全程的秒数由 `ProjectileFlightSeconds` 决定（子弹 0.3、航弹 1.5），调用方通过 `Play(time)` 传了则以调用方为准。

### `FlyingEffect` : Effect (core_logic/FlyingEffect.cs)

飞掠效果：让**触发它的那张卡**升起 -> 原地悬停 -> 落回，把卡的 Position / Scale / Rotation / ZIndex 全接管，播完在 `finally` 里逐项还原。

**本特效不负责转向**：卡在整段动画里保持进入前的角度。早先版本会让卡边升边「指向被攻击的目标」（`AimRotationOffset`），已整段删除。

三段顺序在 `Play` 里显式可见，其中**悬停**是钩子：

| 成员 | 说明 |
|------|------|
| `Play(...)` | 升起（含并行的事）`RiseAsync` -> 悬停 `StayAsync` -> 落回 `LandAsync` |
| `protected virtual Task DuringRiseAsync(card, positions, count)` | 与**起飞同时**开跑的事，默认什么都不做。父类把它和升起 `WhenAll` 等在一起，所以它比升起长也没关系——起飞那段会一直等它 |
| `protected virtual async Task StayAsync(card, positions, baseRotation, stayDuration, count)` | 悬停阶段。`SwayDegrees` 为 0（默认）时是**原地静止悬停**（只等够时间，不建 tween）；调大了才在卡自己的原始角度上左右摆。子类覆写它即可在这段时间里捎带做别的事 |

### `AirStrikeEffect` : FlyingEffect (core_logic/AirStrikeEffect.cs)

空袭特效：轰炸机攻击时**刚开始起飞，航弹就一起扔出去**，投完了原地悬停一下再落回桌面。投弹夹在飞掠中间，所以拼接写法（`attackEffect = flying,bombing`）做不到，必须由一个特效自己掌握节奏。

只覆写 `DuringRiseAsync` = 投弹。投弹本身不在这里实现，而是把 `StrikeEffectName`（默认 `bombing`）当**子特效**播一遍，弹数、飞行时长、错开间隔仍然配在 `effects/bombing_effect.tscn` 里。升起/悬停/降落/还原/飞掠音效全部沿用父类——「卡飘起来」这套运动只有一份实现。

> 投弹最初挂在 `StayAsync`（等起飞演完才投），实机反馈「炮弹发射得太晚」——起飞那一段有整整一秒。现在挂到 `DuringRiseAsync`，卡刚一离地航弹就已经在飞了。

### `SmokeEffect` : Effect (core_logic/SmokeEffect.cs)

单位被消灭时在其中心播放的烟雾动画。使用`assest/Smoke_006.png`的4×4图集，每帧256×256，按行依次播放16帧，默认总时长0.4秒；前10%时间淡入，从30%进度开始淡出。

### `BattleEffectPool` : Node (core_logic/BattleEffectPool.cs)

战斗场景级视觉效果对象池。进入战斗时预加载并预渲染效果资源，预留4个子弹效果根、40颗子弹和8个烟雾效果；并发超出容量时允许临时扩容，播放结束后只保留池容量内的对象。

### `Player` 类 (battlefield_.cs ~line 4904)

玩家管理类。管理手牌、卡组、指挥点。

| 职责 | 说明 |
|------|------|
| 手牌管理 | `GetCardsInHand()`/`AddCardToHand()`/`RemoveFromHand()` |
| 卡组管理 | `DrawCard()`/`AddCardToDeck()`/`ShuffleDeck()` |
| 起手换牌 | `MulliganAsync(returned)` — 把选中的手牌洗回牌库再补抽等量张，返回新抽到的牌 |
| 指挥点 | `ReadPoint()`/`UsePoint()`/`AddPoint()`/`AddPointMax()`/`AddPointMaxNatural()`；`AddPoint` 可把当前点数推高到当前上限之上（天花板 `pointMaxMaxMax`=24），回合开始时由 `AddPointMaxNatural()` 刷满 |
| 手牌布局 | `RefreshMyHand()` — 弧形排列、悬停浮起推旁、旋转倾斜缩放动画 |
| 卡组初始化 | `InitializeDeckFromIni()` 从 deck.ini 或持久化ID加载 |

### `CardMaganer` 类 (battlefield_.cs ~line 5551)

卡牌数据管理器。卡牌数据库。

| 方法 | 说明 |
|------|------|
| `GetCard(string id)` | 按ID获取CardData |
| `GetRandomCard()` | 随机获取一张非HQ卡 |
| `GetAllCards()` | 返回所有卡牌数据 |
| `LoadHq(int isFriend)` | 加载"莫斯科"(友方)或"柏林"(敌方)总部卡 |
| `SetCardDictionary(dict)` | 从外部设置卡牌字典 |

## 资源/工具类

### `GameDialogue` : Node (core_logic/GameDialogue.cs)

全局autoload对白门面。缓存Dialogue Manager资源、串行化播放请求，并提供等待式`PlayAsync()`和非等待式`Play()`；开始菜单禁止播放。

### `MusicManager` : Node (core_logic/MusicManager.cs)

全局autoload背景音乐管理器。读取`configs/music.ini`中的槽位配置，维护单一常驻`AudioStreamPlayer`，确保StartMenu、WorldMap和battlefield_在场景切换时不会因节点销毁而中断音乐。

| 成员 | 说明 |
|------|------|
| `PlaySlot(slot)` | 请求槽位。**不立即换曲**：有曲目在播时只记入`pendingSlot`排队，等曲末再切 |
| `PlayBattleSlot(enemyPreset)` | 战斗BGM入口：优先`battleBGM_<预设名>`槽位，未配置回退`BattleSlot` |
| `HasSlot(slot)` | 槽位是否配置了至少一首曲目 |
| `StopMusic()` / `SetVolumeDb(db)` | 停止播放（并清空排队）/ 设置音量 |
| `BattleBgmPrefix` / `BattleSlot` | 常量`"battleBGM_"`与`"battle"`，避免调用方写死字符串 |

内部成员：`StartSlot()`是唯一真正起播的地方；`OnTrackFinished()`由`AudioStreamPlayer.Finished`驱动，曲末切到排队槽位、无排队则从当前槽位续播下一首；`DisableBuiltinLoop()`关掉三种格式的内建循环，否则曲目永不结束、`Finished`不触发。

槽位表为`Dictionary<string, string[]>`并按忽略大小写比较；同槽位重复请求撤销排队，换槽位抽到同一首也不重播。详见`docs/MUSIC.md`。

### `GameDialogueBalloon` : CanvasLayer (core_ui/GameDialogueBalloon.cs)

项目Galgame对白气泡控制器。组合Dialogue Manager官方C#气泡，根据行标签切换`happy`、`sad`、`angry`、`normal`立绘，缺失素材回退normal。

### `ResourceManager` : Node (bin/ResourceManager.cs)

单例资源缓存节点。

| 职责 | 说明 |
|------|------|
| 纹理缓存 | `GetTexture(path)` — 懒加载+缓存Texture2D |
| 场景缓存 | `GetScene(path)` — 懒加载+缓存PackedScene |
| 字体缓存 | `GetFont(path)` — 懒加载+缓存FontFile |
| 空卡池 | `AcquireEmptyCard()`/`ReleaseEmptyCard(card)` — 对象池复用cardBase_ |

### `SceneLoader` 静态类 (bin/SceneLoader.cs)

异步场景切换。

| 方法 | 说明 |
|------|------|
| `ChangeSceneAsync(currentScene, targetPath)` | 异步切换场景，带Loading覆盖层 |
| `BeginPreload(path)` | 后台预加载PackedScene |

### `BattleStateManager` 静态类 (bin/CardRestoration.cs)

跨场景持久化状态管理。

| 职责 | 说明 |
|------|------|
| 卡组持久化 | `DeckCardIds` 跨战役保留卡组ID列表 |
| 战役状态 | `SelectedEnemy`/`SelectedArea`/`IsCampaignMode`/`UnlockedArea` |
| 卡牌缓存 | `CacheAllCards()`/`GetCachedCard()`/`GetAllCachedCards()` |
| 配置缓存 | `CacheAllCards()`/`GetCachedCard()`、`CacheAllEvents()`/`GetEvent()`、`CacheAreaPools()`/`GetCachedAreaPools()`；返回地图时复用缓存，避免重复解析 INI |
| 区域管理 | `AdvanceArea()`/`UnlockAllAreas()`；`ReadAreaIntensity()`/`EnsureAreaIntensity()`/`ConsumeAreaIntensity()`/`IsFinalArea()` 管理区域战斗烈度 |
| 卡组查看 | `ShowDeckViewer()`/`BuildDisplayDeck()` |
| 商店数据 | `StoreCardQueue`/`StoreCurrentSlots`/`InitializeStoreSlots()`/`RefreshStoreSlots()` |

### `IniFile` / `IniSection` / `IniValue` (bin/iniHandler.cs)

通用INI文件解析和写入库。支持有序节、键值对、类型转换。

### `MeterLabel` : Control (bin/MeterLabel.cs)

电表式数字滚动显示组件。每个数位独立裁剪窗口+垂直滚条+Tween动画。

### `SettingsManager` : static (bin/SettingsManager.cs)

设置项静态存储。读取 `setting.ini` 的 `bool` section，提供 `Items`、`GetBool()` 和 `SetBool()`。

## 场景类

| 类 | 文件 | 说明 |
|----|------|------|
| `WorldMap` : Control | bin/WorldMap.cs | 世界地图主界面。7个区域按钮随进度解锁，按钮按墨卡托投影落在对应历史城市。含调试控制台；卡牌、事件与区域池配置首次解析后跨场景复用。内置`Area`类（区域池、`areaTimes`烈度、`boss`预设名）。任务抽取委托给`MissionDrawer`。事件期间由 `EnterEventOverlay()` / `ExitEventOverlay()` 接管：只收起任务面板（保留本次抽到的一批）、锁住区域按钮，好让商店与卡组仍可点开。 |
| `MissionDrawer` : static | bin/MissionDrawer.cs | 任务抽取规则的唯一实现，纯计算不依赖场景节点。按当前烈度处理`boss`（不为1则不参与抽取，为1则只提供它），其余组成`2战斗+1事件`，不足时只用剩余战斗补位。见`docs/CONFIG.md`的`AreaPool.ini`一节。 |
| `ChooseMission` : Control | bin/ChooseMission.cs | 任务选择面板。3个任务按钮（战斗或事件）。 |
| `EventScene` : CanvasLayer | bin/EventScene.cs | 剧情事件界面。配图+描述+选项，支持获得战役资源点和卡牌替换效果。 |
| `EventMaterialPoints` : static | bin/EventMaterialPoints.cs | 解析事件 `materialPoints(n)` 效果，并以 `int.MaxValue` 为上限执行安全加法。 |
| `PostBattleReward` : CanvasLayer | bin/PostBattleReward.cs | 战后奖励系统。3组卡牌选择→卡组替换。稀有度限制。 |
| `DisplayCard` : Control | bin/DisplayCard.cs | 卡组查看器。滚轮翻页。 |
| `End` : CanvasLayer | End.cs（项目根目录） | 战斗结束浮层。负责暗幕与国徽显示，并驱动 `bin/settlement_panel.tscn` 的结算/失败面板；面板布局在场景里，本类只填数值与等待确认。 |
| `CampaignVictory` : CanvasLayer | bin/CampaignVictory.cs | 战役通关浮层。最后一个区域烈度归零时由战斗或事件路径调用，叠加暗幕（Layer=90，低于对白气泡）播放 `campaign_victory` 对白，结束后返回开始菜单。 |
| `Store` : Control | Store.cs | 商店界面。CanvasLayer叠加于WorldMap上方。7张卡按稀有度加权随机生成，价格按稀有度生成（`CardRestoration.StorePriceTable`：普通 15±5、稀有 30±8、史诗 60±9、传奇 90±12，下限 1），随机打折；待购卡复用手牌式金色边框、放大、置顶和上移反馈。点击购买→扣物资点→ChooseSomeCard选1张替换。refresh消耗5物资点重新生成7张卡。 |
| `ChooseSomeCard` : Control | ChooseSomeCard.cs | 统一卡组选卡替换UI。静态Show(parent, pickCount, title)返回选中卡ID；通过SceneTree.Root上的最高层CanvasLayer覆盖当前界面，场景内最底层为浅黑遮罩；使用五列大卡网格、悬浮缩放与高对比选中框，并延迟启用卡牌输入以隔离打开界面的点击。被PostBattleReward/EventScene/Store复用。 |
| `MulliganScreen` : Control | bin/MulliganScreen.cs | 起手换牌界面。静态`ShowAsync(field, player)`走完整个流程：把真实手牌reparent进覆盖层平铺在屏幕前，点击层切换选中并显示X标记，确认后调用`Player.MulliganAsync()`洗回补抽，新牌在原位展示后随全部手牌归位。可换任意张（含一张不换或全换），只有一次换牌机会。 |
| `StartMenu` : Control | bin/StartMenu.cs | 游戏启动界面。显示全屏底图和继续、开始、设置、鸣谢入口；按钮带悬浮缩放。 |
| `SettingsMenu` : Control | bin/SettingsMenu.cs | 设置场景。按 `SettingsManager.Items` 动态生成布尔设置开关。 |

## 数据类型/枚举 (cardBase_.cs 末尾)

| 类型 | 说明 |
|------|------|
| `CardData` : Resource | 卡牌数据模型。Id/Name/Attack/Defense/Cost/Effect/CardType/Rarity/Traits/IconPath/TargetType |
| `HQ` enum | normalCard / hq / command |
| `CardTypes` enum | Plane / Bomber / Tank / Infantry / Artillery / Command |
| `UnitTraits` enum (Flags) | None / Blitz(闪击) / Determination(奋战) / HeavyArmor(重甲) / SmokeScreen(烟幕) / Guardian(守护) / Shock(冲击) / Ambush(伏击) / Immunity(免疫) / Mobilize(动员) / SharedHatred(同仇) / Suppressed(压制) / Garrison(驻守) |
| `ActionForbiddingTraits` const | 「禁止主动行动」类特性的**唯一掩码**（`Garrison \| Suppressed`）。AddTrait / RemoveTrait / IsActionForbidden 三处共用；新增同类特性只需并进它 |
| `IsActionForbidden()` | 本单位主动行动是否被特性禁止；`battlefield_.cs` 的 Fight / FightRandomEnemy 靠它挡住「强制参战」绕过 |
| `Rarity` enum | Common / Rare / Epic / Legendary / Unobtainable |
| `Stage` enum | Prepare / Draw / Battle / End / EnemyPrepare / EnemyDraw / EnemyBattle / EnemyEnd |
| `CardState` enum | inHand / caught / placed / played / attack / beAttacked / destroyed / inplaceAndCaught / commandCardCaught |
| `Times` enum | 21个触发时点（Deployed / Attacking / BeingAttacked / Dead / BePicked 等） |
| `IsFriend` enum | friend / enemy / neutral / enemyNeutral |
| `ChangeType` enum | GetAttack / LoseAttack / GetDefence / LoseDefence / SetDefence / DiscardCard |
| `TargetType` enum | aPlace / aUnit / aFriendlyUnit / anEnemyUnit / myHq / enemyHq / anyTarget / friendlyTarget / enemyTarget / NOTarget |
| `EffectAttribute` class | 效果的attribute元数据：IconName / Description / IsTrait / TraitName / IconTint |
| `IconCache` static class | 图标缓存，预加载/获取trait图标纹理 |
| `Change` struct | ChangeType + Value，用于缓冲的属性变更 |
