# 选择界面函数说明与最佳实践

## 目的
这个提示文件专门说明当前战场选择界面的相关函数作用，以及我在后续维护时应该遵循的最佳实践。

---

## 核心函数说明

### `ChooseSomeCard.Show(parent, pickCount, title)`
- 作用：统一显示“从玩家现有卡组选择指定数量卡牌”的界面，供事件换卡、商店购买和战后奖励替换复用。
- 返回：选中的卡牌ID列表；取消或卡组为空时返回空列表。
- 层级：实例挂到 `SceneTree.Root` 下的独立 `CanvasLayer`，层级为 `int.MaxValue`，不受调用者自身CanvasLayer影响。
- 遮罩：`choose_some_card.tscn` 的 `OverlayMask` 是根节点第一个子节点，使用浅黑色并拦截输入；标题、卡牌、点击区和按钮均显示在其上方。
- 选择数量：必须选择满 `pickCount` 才能确认。
- 布局：卡牌以五列显示，使用 0.9 倍大卡和较大的横纵间距；悬浮时缩放至 0.96 倍，移开后恢复。
- 输入：卡牌点击区在创建后通过 `Callable.From` 延迟执行 C# 属性赋值来启用，避免打开商店替换界面的原始点击被继承为选卡点击。
- 选中反馈：复用手牌的 `cardBase_.SetHover` 样式，使用卡牌自身的金色边框、放大、置顶和上移效果；移除额外选中外框。
- 当前调用方：`Store`、`EventScene`、`PostBattleReward`。
- 边界：战场 `Choose` / `Develop` 操作临时候选卡实例，包含动画、对象池与加入手牌流程，不属于现有卡组选卡替换，不应调用本组件。

### `RefreshAllCardDisplayOrder()`
- 作用：重新整理战场和手牌中的卡牌显示层级。
- 重点：
  - 普通场上卡片默认 `ZIndex = 10`
  - 选择界面期间，`choiceCards` 保持更高 `ZIndex = 40`
  - 当前正在拖拽或选择的卡 `cardNowChoose` 视需要在最前面显示
- 最佳实践：
  - 避免在此函数中对选择层卡片做过多逻辑判断，保持其职责单一为“显示顺序刷新”。
  - 对 `cardInPlaces` 做空检查，确保排序逻辑不会因 `null` 抛异常。

### `CreateChoiceOverlay()`
- 作用：初始化选择界面使用的 `CanvasLayer`、遮罩（`ColorRect`）和容器（`HBoxContainer`）。
- 重点：
  - `choiceLayer` 用于让选择卡始终在最上层显示
  - `choiceDim` 目前不再遮盖画面，只保留透明阻挡区域
  - `choiceContainer` 仅用作布局辅助，不捕获鼠标输入
- 最佳实践：
  - 如果希望选择界面更简洁，必要时直接用 `choiceLayer` 托管可交互卡牌即可，不必依赖 `choiceContainer` 触发点击。
  - `MouseFilterEnum.Ignore` 可以避免容器抢占点击事件。

### `ShowCardChoice(List<cardBase_> cards, bool animateExit = true)`
- 作用：显示可选卡牌，让玩家点击选择一张。
- 关键步骤：
  1. 将传入卡牌放到 `choiceLayer` 下
  2. 设置它们初始位置为屏幕左侧外部
  3. 逐张飞入到屏幕中央可见区域
  4. 等待 `selectedChoiceCard` 被点击
  5. 处理选中卡退出或直接回手牌
  6. 清理其余临时卡片
- `animateExit` 参数区别：
  - `true`：用于 `Choose(...)`，选中卡片飞出动画后再结算
  - `false`：用于 `Develop(...)`，选中卡片直接回手牌，不执行飞出动画
- 最佳实践：
  - 不要让选择卡留在 `cardInPlaces`，否则会影响场上点击检测与排序
  - `choiceCards` 清空和 `choiceLayer` 子节点清理必须在选择结束后执行
  - 只用 `selectedChoiceCard.ResetVisualsInstant()` 在回手之前重置状态，避免手牌显示残留异常

### `HandleChoiceCardClick(Vector2 mousePosition)`
- 作用：在选择界面里根据点击位置匹配当前展示的选择卡。
- 关键点：
  - 直接检测 `choiceCards` 而非 `cardInPlaces`
  - 使用 `GetGlobalRect().HasPoint(mousePosition)` 进行命中判断
- 最佳实践：
  - 不要把本函数的逻辑和正常战场点击混在一起，保持它只处理选择阶段点击
  - 若需要将来支持触碰反馈，可以在这里扩展 hover 逻辑，但不要改变 `selectedChoiceCard` 赋值方式

### `CheckCardClick(Vector2 mousePosition)`
- 作用：返回当前鼠标所在的第一张场上卡牌。
- 重点：
  - 只遍历 `cardInPlaces`
  - 这是普通战场点击判定，不应包含选择界面里的临时卡牌
- 最佳实践：
  - 如果之后要支持选择界面里的卡片点击，新增独立函数而不是复用此函数
  - 保证 `cardInPlaces` 与 `choiceCards` 在选择阶段的边界清晰

### `AddToBattleField(cardBase_ card)`
- 作用：把卡牌节点加入当前 `battlefield_` 的子节点，并添加到战场卡列表。
- 最佳实践：
  - 只用于真正进入战场或手牌显示的卡，而不是临时选择卡
  - 添加前应避免重复加入，必要时先检查父节点状态

### `RemoveCard(cardBase_ card)`
- 作用：从战场列表移除卡牌并触发销毁/回收逻辑。
- 重点：
  - 如果卡牌是敌方总部，执行屏幕暗化效果
  - 最终调用 `card.Dead()` 让卡片回收或销毁
- 最佳实践：
  - 对于临时选择卡，不要调用 `RemoveCard`。`RemoveCard` 适合真实卡牌死亡或弃置路径。
  - 选择卡的未选中回收应直接从父节点剥离并回收到对象池，而不是走战场死亡流程。

### `ParseAndExecuteEffect(string effectString, cardBase_ sourceCard, List<cardBase_> targetCards = null, cardBase_ targetCard = null)`
- 作用：解析卡牌效果字符串并执行每一步具体技能。
- 关键点：
  - `Choose(...)` 和 `Develop(...)` 解析后都可能调用 `ShowCardChoice`
  - `sourceCard` 负责传递所属阵营和触发效果语境
- 最佳实践：
  - 复杂指令解析尽量保持语句可读，不要在单条 `if` 里叠加太多逻辑
  - 当效果包含选择与回收时，优先把视觉选择、效果触发、回收三块拆开处理

### `Player.AddCardToHand(cardBase_ card)`
- 作用：把卡加入玩家手牌列表，并刷新手牌布局。
- 关键点：
  - 手牌满时转为弃牌流程
  - 添加时如果卡不在 `battlefield.ReadCardInPlaces()`，会补充到战场节点中
- 最佳实践：
  - 手牌加入前保持卡片状态清晰：`setState(CardState.inHand)`
  - 若是选择界面选出的卡片，必须先用 `ResetVisualsInstant()` 清除选择 UI 的状态

### `cardBase_.ResetVisualsInstant()`
- 作用：立即停止悬停动画，重置卡片大小、旋转、亮度、高亮和 `ZIndex`。
- 最佳实践：
  - 任何从选择界面回到手牌的卡片，都应先调用此方法，避免残留 animate/hover 状态
  - 对 `choiceCards` 以外的普通卡牌无需频繁调用，避免视觉抖动

### `cardBase_.MoveToPosition(Vector2 destination, float duration = 0.5f)`
- 作用：异步 Tween 移动卡片到目标位置。
- 最佳实践：
  - 向手牌布局移动时可设置较短时长（如 `0.12f`）来增加响应感
  - 对于选择界面进入与退出动画，使用 `await` 等待完成保证视觉连贯

---

## 选择流程区分说明

### `Choose(...)` 应该保持的行为
- 显示一个临时选择界面
- 选择后执行被选卡的效果
- 选中卡在屏幕上快速飞出，保持原先“抉择”视觉感觉
- 不直接进入手牌

### `Develop(...)` 现在应保持的行为
- 仍然显示选择界面
- 选中后直接加入手牌
- 不做“飞出到左侧”的退出动画
- 选中卡进入手牌前重置视觉状态

---

## 自己的最佳实践提示

- 任何视觉交互逻辑与战场数据逻辑要分离。游戏状态走 `cardInPlaces` / `cardsInHand`，视觉层放到 `choiceLayer`。
- 从持久化卡组选择待替换卡牌时必须调用 `ChooseSomeCard.Show`，禁止在事件、商店或奖励类中重复创建卡组网格。
- 选择界面里不要复用普通点击判定函数；如果需要，新增专用命中检测。`

- 临时 UI 卡片不应永久留在战场列表里。结算完成后它们必须从父节点剥离并清理。

- 只让 `ShowCardChoice` 维护自己的 `choiceCards`。结束后 `choiceCards.Clear()` 是必须操作。

- 如果要改动新的选择效果，先问自己：这个效果属于“视觉层”还是“战斗数据层”？不要把两者混淆。

- 日后调试时，优先从 `ShowCardChoice` 的 `selectedChoiceCard` 生命周期入手：
  - 是否在选择阶段被赋值
  - 是否在结束时被正确回收
  - 是否在手牌加入前调用了 `ResetVisualsInstant()`

- 对于新功能，先写画面流程图：
  - 进入选择界面
  - 点击选项
  - 选中卡片状态变化
  - 后续效果执行
  - 卡片离开选择界面 / 进入手牌

---

## 代码维护建议

1. 改变 `ShowCardChoice` 逻辑时，先保持 `Choose` 与 `Develop` 行为的区分。
2. 做视觉动画改动时，不要改动战场数据列表相关逻辑。
3. 任何回收逻辑，优先使用 `ResourceManager.Instance.ReleaseEmptyCard(...)` 而非 `RemoveCard(...)`，只要它是临时选择卡。
4. 保留 `choiceLayer` 的单独节点结构，避免后续出现层级错乱。
5. 如果增加更多选择类型，考虑把 `ShowCardChoice` 拆成更小函数：`PrepareChoiceCards()`, `PlayChoiceEntryAnimation()`, `WaitForSelection()`, `CleanupChoiceCards()`。
