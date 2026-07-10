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

### `Bullet` : Control (bin/Bullet.cs)

飞弹动画。从攻击卡飞向目标卡。

| 方法 | 说明 |
|------|------|
| `Fly(from, to, duration)` | 异步动画：从攻击者飞向目标，带随机偏移 |

### `Player` 类 (battlefield_.cs ~line 4904)

玩家管理类。管理手牌、卡组、指挥点。

| 职责 | 说明 |
|------|------|
| 手牌管理 | `GetCardsInHand()`/`AddCardToHand()`/`RemoveFromHand()` |
| 卡组管理 | `DrawCard()`/`AddCardToDeck()`/`ShuffleDeck()` |
| 指挥点 | `ReadPoint()`/`UsePoint()`/`AddPoint()`/`AddPointMax()`/`AddPointMaxNatural()`；当前点数始终不高于当前上限 |
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
| 战役状态 | `SelectedEnemy`/`SelectedArea`/`IsCampaignMode`/`CompletedAreas` |
| 卡牌缓存 | `CacheAllCards()`/`GetCachedCard()`/`GetAllCachedCards()` |
| 事件缓存 | `CacheAllEvents()`/`GetEvent()` |
| 区域管理 | `IsAreaUnlocked()`/`MarkAreaCompleted()` |
| 卡组查看 | `ShowDeckViewer()`/`BuildDisplayDeck()` |
| 商店数据 | `StoreCardQueue`/`StoreCurrentSlots`/`InitializeStoreSlots()`/`RefreshStoreSlots()` |

### `IniFile` / `IniSection` / `IniValue` (bin/iniHandler.cs)

通用INI文件解析和写入库。支持有序节、键值对、类型转换。

### `MeterLabel` : Control (bin/MeterLabel.cs)

电表式数字滚动显示组件。每个数位独立裁剪窗口+垂直滚条+Tween动画。

## 场景类

| 类 | 文件 | 说明 |
|----|------|------|
| `WorldMap` : Control | bin/WorldMap.cs | 世界地图主界面。10个区域按钮随进度解锁。含调试控制台。 |
| `ChooseMission` : Control | bin/ChooseMission.cs | 任务选择面板。3个任务按钮（战斗或事件）。 |
| `EventScene` : CanvasLayer | bin/EventScene.cs | 剧情事件界面。配图+描述+选项，支持卡牌替换效果。 |
| `PostBattleReward` : CanvasLayer | bin/PostBattleReward.cs | 战后奖励系统。3组卡牌选择→卡组替换。稀有度限制。 |
| `DisplayCard` : Control | bin/DisplayCard.cs | 卡组查看器。滚轮翻页。 |
| `End` : CanvasLayer | bin/End.cs | 屏幕暗化效果。单例。战斗胜利时显示国徽+暗化。 |
| `Store` : Control | Store.cs | 商店界面。CanvasLayer叠加于WorldMap上方。7张卡按稀有度加权随机生成，价格按稀有度生成，随机打折。点击购买→扣物资点→ChooseSomeCard选1张替换。refresh消耗5物资点重新生成7张卡。 |
| `ChooseSomeCard` : Control | ChooseSomeCard.cs | 统一卡组选卡替换UI。静态Show(parent, pickCount, title)返回选中卡ID；通过SceneTree.Root上的最高层CanvasLayer覆盖当前界面，场景内最底层为浅黑遮罩；使用五列大卡网格、悬浮缩放与高对比选中框，并延迟启用卡牌输入以隔离打开界面的点击。被PostBattleReward/EventScene/Store复用。 |

## 数据类型/枚举 (cardBase_.cs 末尾)

| 类型 | 说明 |
|------|------|
| `CardData` : Resource | 卡牌数据模型。Id/Name/Attack/Defense/Cost/Effect/CardType/Rarity/Traits/IconPath/TargetType |
| `HQ` enum | normalCard / hq / command |
| `CardTypes` enum | Plane / Bomber / Tank / Infantry / Artillery / Command |
| `UnitTraits` enum (Flags) | None / Blitz(闪击) / Determination(奋战) / HeavyArmor(重甲) / SmokeScreen(烟幕) / Guardian(守护) / Shock(冲击) / Ambush(伏击) / Immunity(免疫) / Mobilize(动员) / SharedHatred(同仇) |
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
