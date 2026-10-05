# 代码逻辑问题汇总

> 仅收集记录，**未做修改**。请在确认后再修改。

## 🔴 高优先级

### 1. 双倍 IncrementLifeTime() (battlefield_.cs ~line 2916, ~2931)

`OnNextTurnButtonPressed()` 中 `IncrementLifeTime()` 被调用了两次，单位每回合存活计数+2而非+1。

```csharp
// 第一次 (line 2916-2919)
foreach(var card in ...) { card.IncrementLifeTime(); }

// 第二次 (line 2931-2934) - 完全相同的循环
foreach(var card in ...) { card.IncrementLifeTime(); }
```

### 2. RemoveCard 未解绑 place_ (battlefield_.cs line 3057)

`RemoveCard()` 从 `cardInPlaces` 移除后调用了 `card.Dead()`（`Dead()` 内部会解绑），但 `RemoveCard` 本身没有显式调 `place_.UnbondCard()`。虽然 `Dead()` 已解绑，但如果 `Dead()` 逻辑变更，可能留下悬空引用。

### 3. SetMyPlace(null) 空引用崩溃 (cardBase_.cs line 1439-1446)

```csharp
public void SetMyPlace(place_ place)
{
    if(myPlace != null) myPlace.UnbondCard();
    myPlace = place;
    myPlace.BondCard(this); // NRE if place is null
}
```

## 🟡 中优先级

### 4. PlayCardWithoutCost 双倍执行卡效果 (battlefield_.cs line 4576, 4608)

`PlayCardWithoutCost()` 在无 `targetType` 参数时执行一次效果，有 `targetType` 时又用随机目标再执行一次。

### 5. async void FlashAttributeWithColor (cardBase_.cs line 1608)

`async void` 无法被 await，未捕获的异常将静默终止。应改为 `async Task`。该方法在 `GetDefence`/`LoseDefence`/`GetAttack` 等中被 fire-and-forget 调用。

### 6. AddCardToHand(CardData) async without await (battlefield_.cs line 5061)

方法标记为 `async Task` 但内部没有 `await`，编译器警告 CS1998。应去除 `async` 或改为同步方法。

### 7. 多个 fire-and-forget 死亡检查 (battlefield_.cs ~line 4554, 2971, 3004)

`CheckIfAnyUnitDiedAsync()` 在多处被 fire-and-forget 调用（`_ =` 或直接调用），可能导致属性变更（如动员buff）未在死亡判定前生效。

### 8. AttackInf 空桩方法 (cardBase_.cs line 1688)

```csharp
async public Task AttackInf(cardBase_ target) { }
```
完全空体，步兵攻击动画未实现。

## 🟢 低优先级

### 9. DiscardCard 停留时间错误 (cardBase_.cs line 1579)

注释说"停留3秒"但 `await Task.Delay(1000)` 只有1秒。

### 10. FlashAttributeWithColor 注释与代码不一致 (cardBase_.cs line 1626)

注释说"等于初始值：黑色"，代码用 `Colors.White`。

### 11. 方法名拼写错误 (cardBase_.cs line 1675, 1683)

`MouceEntered()` / `MouceExited()` 应为 `MouseEntered` / `MouseExited`。

### 12. 调试函数残留 (battlefield_.cs ~line 3148-3165)

`TestButtonPressed()` 和 `OnButtonTest2Pressed()` 仍是生产代码。

### 13. 回合阶段顺序异常 (battlefield_.cs line 2902-2943)

`EnemyTurnBegin` 在 `TurnBegin` 之前触发，语义上不自然。

### 14. OnRefreshUnitType 缺少 null 检查 (cardBase_.cs line 1396-1398)

`GetNode<Sprite2D>("unitType")` 无 null 保护。

### 15. 文件命名不一致 (CardRestoration.cs)

文件名为 `CardRestoration.cs`，但包含的类是 `BattleStateManager`。

### 16. DiscardCardsWithName 正则局限 (battlefield_.cs line 3793)

正则 `@"\((.*),(\d+)\)"` 在卡名含逗号时会失败。

---

## ✅ 已修复

### 17. SplitEffectByComma 不追踪括号导致 icon 属性丢失 (cardBase_.cs line 983)

`SplitEffectByComma` 只追踪 `[]` 括号深度，不追踪 `()` 括号，导致 `DrawACard(t34,1)` 等指令中括号内的逗号被错误当作段分割符。使 `[icon=dead,...]` 等元数据在分割后被丢弃，attribute 图标无法显示。

**修复**: 增加 `()` 括号深度追踪和引号追踪，只有 `bracketDepth==0 && parenDepth==0 && !inQuotes` 时才将逗号视为分割符。

### 18. 性能优化：每帧刷新显示顺序 (battlefield_.cs)

`RefreshAllCardDisplayOrder()` 在 `_Process()` 中每帧无条件调用，每帧分配3个List + 对每张卡调`MoveChild` + O(n²)的`IndexOf`。

**修复**: 加脏标记`_displayOrderDirty`，只在卡牌增删/手牌变化时设true，`_Process`检测到才刷新。手牌遍历从`foreach+IndexOf`改为`for(int i...)`。

### 19. 性能优化：箭头渲染器每帧QueueRedraw (Cardbase.cs)

`Cardbase._Process` 每帧无条件`QueueRedraw`+鼠标坐标转换，即使箭头不可见。

**修复**: 开头加`if (!Visible) return;`。

### 20. 性能优化：RefreshState磁盘IO (cardBase_.cs)

`RefreshState()` 每次属性变更都调`FileAccess.FileExists(IconPath)`做磁盘IO。

**修复**: 改为`!string.IsNullOrEmpty(IconPath)`内存检查。

### 21. 性能优化：BuildAttributePanel重复调用 (cardBase_.cs)

`BuildAttributePanel()` 调`GetAllAttributes()`两次，第二次完全多余。

**修复**: 删除第二次调用，复用`newAttrs`。

### 22. 性能优化：MoveToPosition不杀旧Tween (cardBase_.cs)

`MoveToPosition()` 每次创建新Tween不Kill旧的，导致悬停切换时Tween竞争+泄漏。

**修复**: 加`moveTween`字段，创建前先Kill旧的。

### 23. 性能优化：热路径GD.Print (cardBase_.cs)

`CheckIfCanAttack`、`FlashAttributeWithColor`、`ParseEffectAttribute`、`IconCache.GetIcon`中的`GD.Print`在频繁调用路径中产生字符串拼接开销。

**修复**: 删除这些调试日志。

### 26. 性能优化批次A：战斗异步串行化

死亡检查改为门闩合并请求和固定点循环，等待change list、Dead效果及连锁死亡完成；回合切换增加防重入，并等待Trait结算、死亡检查和抽牌。

### 27. 性能优化批次B：绘制、Tween与UI热路径

箭头由逐帧约100个三角形绘制改为缓存数组和单个箭身多边形；卡牌与商店移动Tween可取消；商店价格颜色按状态变化更新；属性Tooltip改用局部GUI输入并缓存命中图标。

### 28. 箭身多边形三角剖分失败

批次B将两条动态贝塞尔边界合并成凹多边形，随鼠标角度变化时可能自交，导致Godot持续报告`Invalid polygon data`。最终修复为宽`DrawPolyline`绘制箭身，彻底取消动态箭身多边形三角剖分；仅固定三角箭头头部保留`DrawColoredPolygon`。

宽Polyline与三角箭头仅边界接触时会因抗锯齿产生视觉接缝。拖尾终点现向箭头内部延伸半个拖尾宽度，使两者形成稳定重叠。

以上箭头绘制优化及后续修补已按要求全部撤销，`Cardbase.cs`恢复为提交`7a5258e`中的原始绘制函数。批次A和其他批次B优化不受影响。

### 24. LoadCardDataCache 缺失 IconPath 导致卡图不显示 (WorldMap.cs line 91)

commit `ebc702f`（CardParser 重构）在替换 `GetRarity`/`GetTypes` 等本地方法为 `CardParser.xxx` 调用时，误删了 `cd.IconPath = configFile[section.Key]["icon"].GetString();` 这一行。

**影响**: `WorldMap.LoadCardDataCache()` 加载到 `BattleStateManager` 缓存的 `CardData` 均无 `IconPath`（默认空字符串）。战役模式下 `battlefield_._Ready()` 从缓存取卡牌数据，`SetCardInformation` 将空 `IconPath` 传给 `cardBase_`，`RefreshState()` 中 `FileAccess.FileExists("")` 返回 false，不加载任何纹理，卡图为空白。冷启动直接运行战场场景时走 `battlefield_.cs:479` 的备用加载路径（有 `IconPath` 赋值），故该路径不受影响。

**修复**: 在 `WorldMap.cs` `LoadCardDataCache()` 中补回 `cd.IconPath = configFile[section.Key]["icon"].GetString();`。

### 25. ShowSettlement 结算面板位置错误+缺少确认按钮 (End.cs)

`ShowSettlement` 使用了 `SetAnchorsPreset(Control.LayoutPreset.Center)` 后又设置了相对坐标 `Position = (viewSize.X/2 - 200, 120)`，Center 预设将锚固点设为 (0.5,0.5)，最终 X = viewSize.X - 200，面板跑到了屏幕右下角。

此外，原流程在 `RemoveCard` 中同步调用 `ShowSettlement` 后立即 fire-and-forget `ReturnToWorldMapAfterVictory`（直接跳到奖励选择），缺少结算→用户确认→奖励的等待环节。

**修复**: (1) 去掉 `SetAnchorsPreset`，直接用 `(viewSize - panelSize) / 2` 居中定位；(2) 添加确认按钮，`ShowSettlement` 改为 `async Task` 等待按钮按下；(3) 将 `ShowSettlement` 调用移入 `ReturnToWorldMapAfterVictory` 最前面，确保确认后才进入奖励选择。

---

## 新卡批次（撤退/开发/费用/抉择一系）实机反馈的问题

> 2026-10 新增 18 张卡 + 2 张辅助卡后，实机测试反馈 7 个问题。
> 其中 4 个已修（见下方「已修」），其余为尚未定位/待确认项，记录在此供接手者直接开工。

### 已修（仅存档，不必再动）

| 现象 | 根因 | 修复 | 提交 |
|---|---|---|---|
| 破釜沉舟抽不了牌 | 无参指令按项目约定须裸写（`Retreat`/`HealAllTargets` 都是），而 `GetHandMax()` 带括号会让 `ins`（`instruction.ToLowerInvariant()`，不去括号）变成 `"gethandmax()"`，与 `ins == "gethandmax"` 永不相等 | card.ini 改裸写；代码同时接受两种写法 | `c0d9777` |
| 伏击接应需限定友方 | — | 选择器改为 `${allTargets.unit.friend.Ambush}` | `c0d9777` |
| 开发后牌堆错乱 / 手牌出现空卡牌 | `Develop($选择器)` 把牌堆里的真实卡实例直接加进手牌，既未从牌堆摘除（同一对象同时在两处），也未恢复 `Visible`（牌堆卡靠停在屏幕外隐藏） | 新增 `CloneCardWithCurrentValues()`，复制一份并搬运当前数值进手牌，原卡留在牌堆 | `13efaca` |

### 29. 全面总攻打出后无任何效果 (card.ini [全面总攻] / battlefield_.cs Choose 分支)

`[全面总攻]` 的 effect 是 `Choose(总攻_强攻,总攻_固守)`，期望弹出二选一并执行选中效果，实机无任何反应。

**已排除的原因**（均读过实现确认无误）：
- `foreach` 会把 `targets` 切成当前元素：`battlefield_.cs` 的 foreach 分支里 `targets = new List<cardBase_> { savedTargets[0] };`，循环推进时同理。故辅助卡里的 `&target.attack` 取的是当前单位，写法本身正确。
- `GetCard()` 按 **section ID** 查（`_items[card.Id]`），`Choose(总攻_强攻, 总攻_固守)` 的两个参数与两个辅助卡的 section ID 一致。
- 两张辅助卡的 `effect` 字段与 `cardType` 已写入 card.ini。

**尚未查证**：
- `Choose` 分支内部是 `await ShowCardChoice(new List<cardBase_>{...}, true)` 然后 `await ParseAndExecuteEffect(selectedCard.effect, selectedCard, null)` —— **targets 传的是 `null`**。需确认 `ParseAndExecuteEffect` 对 null targets 的处理，以及 `ShowCardChoice` 第二个参数 `true` 的语义。
- `Choose` 是唯一一处把 `ShowCardChoice` 的返回值直接拿去执行效果的路径，缺少可对照的既有用例。

**建议定位方式**：在 `Choose` 分支的 `if (cardA != null && cardB != null)` 与 `if (selectedCard != null)` 两处各加一行 `GD.Print`，复现后即可看出是「卡没取到」「选择界面没弹」还是「弹了但效果没执行」。

### 30. 无法正确重设费用 (card.ini [机动调配] / [紧急征调])

`setCost(0)` 不生效。**注意：反馈已澄清，出问题的不是「紧急征调」**，嫌疑在 `[机动调配]`。

`[机动调配]` 的 effect：
```
setTargets(${allCardInHand})|getCount(${allCardInHand})|GetRandomNumber(0,&result-1)|GetTargetByIndex(&result)|setCost(0)
```

**已查证**：
- `setCost(n)` 走 `target.AddChange(ChangeType.SetCost, n)`，由 `cardBase_.cs:490` 的 **`ExecChangeList()`（单数！）** 结算，其中 `case ChangeType.SetCost: SetCostValue(change.Value);`。
- `ExecChangeList()` 全项目只有两个调用点：`battlefield_.cs:3031`（遍历 `ReadCardInPlaces()` 逐张执行，手牌卡也在 `cardInPlaces` 中，故理论上覆盖得到）与 `battlefield_.cs:5460`。

**尚未查证**（按嫌疑排序）：
1. `GetRandomNumber(0,&result-1)` 的第二个参数是否支持表达式。该指令分支在 `battlefield_.cs:4713`，需确认它用 `EvaluateExpression` 还是 `int.Parse`。
2. `getCount(${allCardInHand})` 是否正确统计手牌数（决定 `&result-1` 是否合法）。
3. `GetTargetByIndex(&result)` 是否接受变量形式的下标。
4. 结算时机：`setCost` 是缓存型变更，需等 `ExecChangeList()` 被调用才生效。若手牌卡在那之前就被查看，会看到未改的费用。

### 31. 文档错误：LOGIC.md 提到的 ExecChangeLists() 并不存在

`docs/LOGIC.md`「效果指令一览」下的结算时机表写「缓存到 `ChangeList`，由 `ExecChangeLists()` 结算」，但**全项目没有 `ExecChangeLists`**，实际函数是 `cardBase_.cs:490` 的 **`ExecChangeList()`（单数）**。

**影响**：照着文档去搜 `ExecChangeLists` 会一无所获，排查费用/治疗/伤害类问题时白费时间。本次排查 #4 时就先被误导了一次。

**修复**：把 LOGIC.md 中该处函数名改为 `ExecChangeList()`。

### 32. 撤退回手的单位若没有 Blitz，重新部署后永久无法移动/攻击（待确认是否 bug）

`RetreatUnit()`（`battlefield_.cs`）在单位回手牌时调用 `unit.DisableCombatAbility()`，把 `moveAble` 与 `attackAble` 一起置 0。而唯一能恢复它们的 `cardBase_.RefreshUnit()`（`cardBase_.cs:105`）在部署路径上**只对有 `Blitz` 特性的单位调用**（`battlefield_.cs:324` 与 `:2103` 两处都是 `if(card.HasTrait(UnitTraits.Blitz)) card.RefreshUnit();`）。

**待确认**：这可能是**刻意的**——「新部署的单位本回合不能攻击」本就是基础规则，`attackAble=0` 恰好符合；`RefreshUnit()` 只给 Blitz 用也说得通。**尚未读卡牌实例化时这两个标志的初值**，无法判断「非闪击单位撤退回手再打出后是否应该能立即行动」。

**若判定为 bug**：注意修法不能简单地在部署时无条件 `RefreshUnit()`——那等于给所有单位闪击。

### 33. 本批次其余未验证项（非已报问题，属隐患）

| 项 | 说明 |
|---|---|
| `[破釜沉舟]` 的弃牌/抽牌时序 | `setTargets(${allCardInHand})\|foreach\|DiscardWithTarget\|End&\|GetHandMax\|drawCard`。`DiscardWithTarget` 会播放异步弃牌动画，紧随其后的 `drawCard` 可能与之竞态 |
| `[紧急征调]` 的目标语义 | 效果是 `setTarget\|setCost(0)`，`targetType = aFriendlyUnit`（场上单位）。但「费用」是**手牌打出时的成本**，给已在场上的单位设费用是否有可观察效果，存疑。需求方曾澄清「选的是场上的一个友方单位」，但未确认这是否就是想要的效果 |
| `Develop($选择器)` 的 `$` 形式 | 全项目 9 处真实用法都是 `Develop(具体卡名,...)`，**`$` 形式此前从未被使用过**。本次为「步兵第190团」「紧急投产」首次启用，仅按代码分支（参数 `Substring(1)`）推断其语法 |
| 未被选中的候选卡泄漏 | `Develop` 收尾处「释放未选择的卡牌回到池中」那段被注释掉了。命名分支的候选是新建实例，未选中的不会回收。属既有问题，非本次引入 |

### 34. 开发选择界面弹出的卡点不中（Copy 未钉死尺寸）

需求方反馈：`Develop` 复制出来的卡「点选不了」。

**根因**：`cardbase.tscn` 的根 Control 用的是**锚点布局**，不是固定尺寸：

```
[node name="Control" type="Control"]
anchor_right  = 0.112
anchor_bottom = 0.26700002
offset_right  = 0.7999878
offset_bottom = -0.3000183
```

即 `Size = (0.112 × 父宽 + 0.8, 0.267 × 父高 − 0.3)`——**尺寸是从父节点算出来的**。

`ShowCardChoice()` 会把候选卡 `Reparent(choiceLayer)`，而 `choiceLayer` 是个
`CanvasLayer`（不是 Control），卡牌的 anchorable rect 随之换成视口矩形，尺寸被重算。
而**判定「点到哪张卡」用的正是 `GetGlobalRect()`**：

```csharp
if (card.GetGlobalRect().HasPoint(mousePosition)) { selectedChoiceCard = card; return; }
```

尺寸一变，命中框就与眼睛看到的位置对不上，点击落空。

**为什么牌堆卡没事**：`InitializeDeckFromIni()` 早就把这件事修过了，只是没抽成公共入口——

```csharp
var card = template.Duplicate() as cardBase_;
card.SetAnchorsPreset(Godot.Control.LayoutPreset.TopLeft);
card.Size = new Godot.Vector2(180, 240);
```

锚点全归零 + 尺寸写死，卡牌的 rect 就与父节点无关。**`Copy()` 出来的卡没有这两行**，
所以一 `Reparent` 到 `choiceLayer` 就出问题。（同一个函数里首次读 `deck.ini` 的分支
用 `Instantiate()` 也没钉，属同一处遗漏，一并补上。）

**修复**：

1. `cardBase_` 新增 `DesignSize`（180x240，全项目唯一一处配置）与
   `PinDesignSize()`（`SetAnchorsPreset(TopLeft)` + `Size = DesignSize`）。
2. `InitializeDeckFromIni()` 的两条分支改调 `PinDesignSize()`，消除重复配置。
3. `Copy()` 在 `SetCardInformation` **之前**调 `copy.PinDesignSize()`。

**顺带修掉的第二处差异**：`Copy()` 直接写 `copy.cost/attack/defence` 绕开了
`SetCostValue`/`SetDefence` 的刷新副作用，三个数值 Label 还停在 `SetCardInformation`
写进去的初始值上——显示的费用与实际 `ReadCost()` 对不上。末尾补一句 `copy.RefreshState()`
（项目自己的公开刷新入口）即可。

**回归**：`tests/verify_card_copy.py`（16 条），其中断言 `new Vector2(180, 240)` 在两个
源文件里只允许出现一次。

### 35. 时点触发的 Develop 选择界面点不动（控制锁吃掉了点击）

需求方反馈：`[紧急投产]`（手牌打出的指令卡）的开发一切正常，但 `[步兵第190团]`
**无法正确点击**；不报任何错，候选卡也**正常显示**（日志确认：3 张、`visible=True`、
`size=(180,240)`、坐标 `(420,330)/(670,330)/(920,330)`）。

**两张卡的唯一区别是触发时点**：

| 卡 | effect | 触发路径 |
|---|---|---|
| `[紧急投产]` | `Develop($deck)\|GetCardsBeingTreated\|setCost(0)` | 从手牌打出，**不在任何控制锁里** |
| `[步兵第190团]` | `FriendlyTurnBegin:Develop($deck)` | **嵌在回合切换的控制锁里** |

`OnNextTurnButtonPressed()`（`battlefield_.cs`）：

```csharp
ForbidControl();
try { await RunTurnTransitionAsync(); }   // ← FriendlyTurnBegin 在这里面触发 Develop
finally { AllowControl(); }                 // ← 选择界面还开着时，锁尚未解除
```

而 `_Input` 的原顺序是**控制锁判定在选择界面判定之前**：

```csharp
if (ReadControlState() == 1) return;                        // ← 一锁就 return
...
if (isShowingChoiceUI) { HandleChoiceCardClick(...); return; }   // ← 永远到不了
```

于是每一次点击都被第 1098 行吃掉，`ShowCardChoice` 里
`while (selectedChoiceCard == null) await Task.Delay(50);` 永久等待——
**卡看得见、点不动、界面卡死，全程无任何报错。**

**修复**：把选择界面的处理**提到控制锁判定之前**。选择界面是模态的，它的点击
不应该受战场操作锁管辖；战场那侧被锁住反而是对的（回合切换还没走完）。
修完两块都不受影响：选择界面照常可点，战场输入照旧被锁住。

**回归**：`tests/verify_choice_ui_modal.py`（13 条）。核心断言是
「`if (isShowingChoiceUI` 的下标必须小于 `if (ReadControlState() == 1) return;` 的下标」，
并附带断言触发场景确实存在（`[i190]` 的 effect 以 `FriendlyTurnBegin:` 开头、
`OnNextTurnButtonPressed` 确实把 `RunTurnTransitionAsync` 包在 `ForbidControl` 里），
避免这条断言变成空转。

> 注：本条与 #34 是**两个独立问题**。#34（`Copy()` 未钉死尺寸）是「与正规建卡流程
> 不一致」的真实差异，也已修复；但它并不是「不显示 / 点不动」的原因——#34 修完后
> 症状依旧，才据此继续查到时点与控制锁。

### 36. `GetCardsBeingTreated` 指不到开发出来的卡，导致「紧急投产」减不了费

需求方反馈：`[紧急投产]`（`Develop($deck)|GetCardsBeingTreated|setCost(0)`）的开发出的卡
没有变成 0 费。

**根因**：项目里有**两套互相独立**的「刚入手的卡」指针，由两条不同指令读取：

| 指针 | 读取它的指令 |
|---|---|
| `Player.lastDrawnCards` | `GetCardsBeingTreated`（经 `Player.GetLastDrawnCards()`） |
| `battlefield_.lastCardAddedToHand` | `GetCardBeingAddToHand` |

写入点原本散在三处：`DrawCard`、`DrawACard` 一系、以及「加入手牌」指令
（`battlefield_.cs` 的 `AddToHand` 分支，它**两个都写**）。

**`Develop` 让卡进了手牌，却两个都没写。** 于是紧跟其后的 `GetCardsBeingTreated`
拿到的是**上一次抽到的卡**（回合开始时通常刚抽过牌），`setCost(0)` 就减到了别人身上；
若 `lastDrawnCards` 恰好是空列表，则 `targets` 为空，`foreach` 一次都不进，
表现为「整张卡毫无反应」。两种表现都不报任何错。

**修复**（按《需求实现规范》(A)「优先寻找已有功能，避免同一功能多处实现」）：
把「加入手牌」指令里那段记账抽成单一入口

```csharp
private void RecordCardsObtained(List<cardBase_> cards, IsFriend side)
{
    if (cards == null || cards.Count == 0) return;
    lastCardAddedToHand = cards[cards.Count - 1];
    if (side == IsFriend.friend) player1.SetLastDrawnCards(cards);
    else if (side == IsFriend.enemy) player2.SetLastDrawnCards(cards);
}
```

调用方两处：`Develop(...)` 的两个分支（本次修复）、「加入手牌」指令（原本的内联写法
改为调用它）。`lastCardAddedToHand` 的赋值点由此收敛为**全项目唯一一处**。

**边界（有意不动的部分）**：`DrawCard` / `DrawACard` 一系仍只写 `Player.lastDrawnCards`
（抽牌语义），未纳入本函数。因此纯抽牌之后 `GetCardBeingAddToHand` 仍指向上一次
`AddToHand` 的结果——目前无卡使用该组合，记为已知边界而非缺陷。

**结算时机**（顺带核实，不是本 bug 的原因）：`setCost` 是缓存型变更
（`target.AddChange(ChangeType.SetCost, n)`），由 `cardBase_.ExecChangeList()` 落地；
`ParseAndExecuteEffect` 末尾就有 `await ExecuteChangeLists();`，故会在本次效果结算完时生效。
所以「指针错了」就是全部病因。

**回归**：`tests/verify_card_obtained_pointer.py`（17 条），其中断言
`lastCardAddedToHand` 的赋值点全项目只有一处。

---

## 2026-10 实机反馈批次（拖拽/召唤/撤退/敌方脚本/守护/开局）

### 37. 拖动卡牌时截图，卡永久卡在场上

**现象**：拖着一张卡的时候用截图工具截屏，这张卡再也拖不动，显示优先级也乱了。

**根因**：拖拽是「按下进入、松开退出」的一对事件。截图工具会把鼠标抢走，
让「松开左键」永远送不到游戏里；窗口失焦也一样。`cardNowChoose` 与
`state == caught` 就此挂住，而 `RefreshMyHand()` 对拖拽中的卡是 `continue` 跳过的，
于是它再也不会归位。

**修复**：新增 `CancelCurrentDrag()`，两条外部兜底——
`_Notification(NotificationApplicationFocusOut)`，以及 `_Input` 里「收到鼠标移动但
物理左键已不在按下状态」。后者**必须放在 `ReadControlState()` 之前**，
否则解锁前永远轮不到它。

**回归**：`tests/verify_combat_action_timing.py` ①。

### 38. 亡计刷出来的 IS-2 下个回合不能行动

**现象**：近卫步兵272团的亡计拉出的 IS-2，撑到友方回合还是动不了。

**根因**：全场刷新只有 `RefreshAllCardInField()` 一处，且只在 `EnemyTurnAsync()` 开头调用。
`cardBase_._Ready()` 给的是 `attackAble = 0 / moveAble = 0`，只有闪击会在入场时补刷。
回合顺序是「敌方回合开头刷一遍 → 敌方行动中亡计刷出 IS-2 → 友方回合开始」，
中间再没有刷新，于是它整回合都是 0/0。

**修复**：拆成按阵营刷新 `RefreshCardsInField(IsFriend side)`——敌方在敌方回合开头、
友方在友方回合开头各刷一次（友方那次排在 `FriendlyTurnBegin` 时点之前）。
从手牌部署的召唤失调不受影响：部署发生在本次刷新之后。

**顺带**：`BUGS.md` 第 32 条「撤退回手的单位没闪击就永久不能动」是同一个根因，一并解决。

**回归**：`tests/verify_combat_action_timing.py` ②。

### 39. 被撤退的敌方单位在删除前还能攻击

**现象**：撤退敌方支援阵线的一个单位后马上结束回合，它已经在播撤退/移除动画了，
却照样打了一下。

**根因**：`RetreatUnit()` 只对「友方回手牌」那条分支调了 `DisableCombatAbility()`；
敌方弃置分支只挂了个 `AddChange(ChangeType.DiscardCard,1)` 标记。这张卡要等死亡检查
才真正 `RemoveCard`，在那之前它仍绑在 `place` 上、`state` 还是 `placed`，
敌方 AI 照样能把它选去攻击。

**修复**：敌方分支同样先 `DisableCombatAbility()`；顺带把裸的 `Task.Delay(100)`
提成具名常量 `RetreatSettleDelayMs`。批量弃置也改成「起飞前统一关掉全部战斗能力」。

**回归**：`tests/verify_combat_action_timing.py` ③。

### 40. `DiscardRandomly` 写在敌人意图里完全无效

**现象**：`[Orsha]` 的 t3/t6/t9/t12/t15 写着「随机弃置友方N张卡」，实际什么都不发生。

**根因**：该指令按「效果来源卡的阵营」派发。写在 `enemyTurn.ini` 里的战役行动，
来源卡是敌方总部，于是它去弃**敌方自己**的手牌——那是空的。既不报错也没有表现。

**修复**：新增 `DiscardPlayerRandomly(n)`，固定弃置 `player1` 的手牌，与来源阵营无关。
原来的 `DiscardRandomly` 保持「来源方自己弃自己」的语义不动（`card.ini` 里
「抽3张弃1张」那张卡还要用它）。`enemyTurn.ini` 的 5 条行动改用新指令。

**顺带**：t6 的说明写的是「弃2张」而代码是 1 张，按《战役约定》第 2 条
（说明必须严格反映当前状态）把说明改回 1 张。若本意是 2 张，改 `DiscardPlayerRandomly(2)` 即可。

**回归**：`tests/verify_enemy_scripts_and_spawn.py` ④。

### 41. `改装` 指向单位后刷不出新单位

**现象**：`[改装]` 指向 Su-85 之后没有刷出任何单位。

**根因**：`addANewUnitToBattlefieldWithCostAndType` 的正则写死成
`\(([^,]+),\s*(\d+)\)`，第二个参数只认**字面量数字**。而 `[改装]` 传的是 `&result`，
正则整体不匹配 → 整条指令静默失效。（注：`ReplaceVariables` 在派发前已经把 `&result`
换成了数字，但正则对「设计上要收表达式」这件事没有留余地，任何非纯数字实参都会被吞掉。）

**修复**：改为捕获表达式并交给 `EvaluateExpression` 求值，同时给三条静默出路各加日志
（参数解析失败 / 卡池无可用卡 / 支援阵线满员）。第三条是这条指令唯一会「什么都不做」的
正当理由，但从前看不出来，和「效果写错了」分不开。

**回归**：`tests/verify_enemy_scripts_and_spawn.py` ⑤。

### 42. 火炮与轰炸机攻击不了被守护的目标

**修复**：`cardBase_.IgnoresGuardian(CardTypes)` 把 `Artillery` 与 `Bomber` 排除在守护之外。
判定只认攻击方兵种、不看阵营，且放在 `IsTargetProtectedByGuardian` 的**烟幕检查之前**
（排在后面的话，目标带烟幕时函数会先返回，豁免被绕过）。玩家侧 `Attack()` 与
敌方 AI 选目标都调这同一个函数，规则天然一致。

**回归**：`tests/verify_guardian_bypass_and_overlay.py` ⑩。

### 43. 事件期间看不到商店与卡组

**修复**：进事件时只收起任务选择面板（`EnterEventOverlay` 走 `CloseMissionPanel`，
**保留**本次抽到的一批），事件结算后才丢弃（`ExitEventOverlay`）。
事件暗幕的 `MouseFilter` 由 `Stop` 改为 `Ignore`，世界地图上的商店/卡组按钮重新可点
（商店在 CanvasLayer 10、卡组查看器在同层后加，天然盖在事件之上）。
挡区域按钮的活改由 `WorldMap._eventOverlayActive` 在 `OnAreaPressed` 里管——
暗幕不拦鼠标之后，不挡这一下，点到底下的区域会在事件背后又叠一个任务面板。

**回归**：`tests/verify_guardian_bypass_and_overlay.py` ⑥。

### 44. 多张单位卡弃置动画过慢

**修复**：卡与卡之间错开 `DiscardStaggerSeconds`（0.5 秒）起飞，动画互相重叠，
`Task.WhenAll` 统一等待。单张行为不变（不为单张平白加 0.5 秒）。
详见 `docs/LOGIC.md`「单位卡的弃置动画节奏」。

**回归**：`tests/verify_combat_action_timing.py` ④。

### 45. `[标准弹药]` 只减了手牌的费用

**修复**：改为打出时对 `${allCardInHand.friend}` 与 `${deck.friend}` 各 `subCost(1)`，
并去掉原先「每抽 1 张再 -1」的持续效果。

**顺带（必须一起修，否则牌堆那一段会逐张抛空引用）**：
`cardBase_.AnimateCostRoll()` 里用了 `GetTree().CreateTimer(...)`，而牌堆里的卡是
「已实例化但不在场景树上」的对象，`GetTree()` 返回 null。加了 `if (!IsInsideTree()) return;`
（放在 `GetNode<Label>("cost")` 之前）。纯视觉的滚动动画对牌堆卡跳过，费用本身照改。

**回归**：`tests/verify_guardian_bypass_and_overlay.py` ⑦。

### 46. 新增 `battleStart=` 关卡开局效果

「游戏开始时，若 xxx 则 xxx」的通用架构，详见 `docs/LOGIC.md`。
柏林关用它实现「每有 1 条命，友方总部额外获得 10 点防御力」。

**回归**：`tests/verify_enemy_scripts_and_spawn.py` ⑪。

### 47.（待确认，未修）友方总部的效果浮标不显示

用户报告「友方总部的效果浮标似乎没有正确显示」，但表示**先不解决、晚点详细测试**。

已查清的事实：

- 约定见 `docs/战役约定.txt` 第 3 条：永久/成长效果用 `enemyHq|GetEffect("<effect>")`
  贴到**敌方总部**，玩家通过浮标查看。
- `moscow`（友方总部）在 `card.ini` 里 `effect = ` 是空的，效果全靠运行时 `GetEffect` 挂。
- `[柏林之路]` 的解析链已用 Python 原样复刻验证：`enemyHq` 拿到
  `AttackingHq:…[icon=action,description=…]`（结尾是 `]`，`ParseEffectAttribute` 能解析）；
  `myHq` 拿到 `FriendlyTurnBegin: SetMemory(enemyDmg,0)|SetMemory(enemyTriggered,0)`
  （无 `[icon=…]`、结尾是 `)` → 返回 null → 不显示图标）。**后者是内部记账，符合约定。**
- **用户明确要求：不要为此改 `card.ini` 的 `[柏林之路]`。**

待确认：用户看到的到底是哪个总部。若为敌方总部（berlin）不显示，则需查
`BuildAttributePanel` 的渲染链（图标位置、缓存比较、`RefreshState` 时机）。

### 48. 「友方指令打出」时点里的伤害打不死人

**现象**：场上有女狙击手，打出一张机动防御之后，有单位变成 0 血但没死亡。

**根因**：`ExecuteCommandAndDiscard` 里死亡检查排在时点**之前**——

```csharp
await ParseAndExecuteEffect(commandCard.effect, commandCard, targets);
await CheckIfAnyUnitDiedAsync();          // 只覆盖「指令自身的效果」
await TriggerUnitEffects("FriendlyCommandPlayed", commandCard);   // 时点跑完函数就结束了
```

女狙击手的效果是 `FriendlyCommandPlayed:GetRandomEnemyTarget|damage(2)`。
`damage(n)` 是**缓存型变更**，由 `ParseAndExecuteEffect` 末尾的 `ExecuteChangeLists()`
落地——所以时点跑完时目标防御确实已经是 0，只是没人再查一次死亡。

**修复**：在 `FriendlyCommandPlayed` 时点之后补一次 `await CheckIfAnyUnitDiedAsync();`。

**回归**：`tests/verify_trigger_death_check.py`。

### 49. `FriendlyCardDrawn` 时点同样缺死亡检查（已修）

**根因**：`TriggerFriendlyCardDrawn()` 是 `_ = TriggerUnitEffects("FriendlyCardDrawn", card)`
的**发后不理**包装，由 `Player.DrawCard()` 调用，之后没有任何死亡检查。与第 48 条同源。
当前挂在 `FriendlyCardDrawn:` 上的效果只有减费（如旧版标准弹药），所以还没暴露；
一旦有卡写成「抽牌时造成伤害」，就会重演「0 血不死」。

**修复**：`TriggerFriendlyCardDrawn` 改为 `public async Task`，内部 `await` 时点后补
`await CheckIfAnyUnitDiedAsync()`；唯一调用点 `Player.DrawCard` 改为 `await`。

**为什么没有上移到 `TriggerUnitEffects` 统一处理**：32 个调用点里多数
（`Attack` 内 8 处、`Move`、`AddCardToPlace`、死亡流程自循环）各自已有正确的检查时机，
有的还处在 `PauseDeathCheck()` 区间内。统一塞进函数末尾虽然能让调用方「谁也不用记」，
却一次性改变所有时点的时序；静态推演它能终止（重入只会把 `deathCheckRequested`
置真、多跑一轮定点循环），但无法在静态层面验证。故先逐点补齐，统一入口留待实机确认。

**回归**：`tests/verify_trigger_death_check.py`。

### 50. 「卡莫名被弃」的诊断日志（配合第 51 条的排查）

`ProcessDeadUnitsOnceAsync` 里，只要 `shouldBeRemoved == 1` 就会被无条件移除——
**即使没有任何效果点名要弃它**。现在移除前打一行日志（id / 阵营 / isHq / cardType / 状态），
让「卡莫名被弃」当场可查：是哪个效果点名的、还是状态泄漏带进来的。

### 51. 控制台敲 `retreat` 会把友方总部塞进手牌，之后被 `discardrandomly` 弃掉

**现象**：控制台输入 `discardrandomly(9)` 时，总部也一起被弃掉了。

**根因**：`RetreatUnit()` 从不检查目标是不是总部，而它的友方分支有一条
「回手牌」出口：

```csharp
if (isFriendly)
{
    if (player1.GetCardsInHand().Count < 9)
    {
        unitPlace.UnbondCard();
        unit.ClearMyPlace();
        unit.DisableCombatAbility();
        await player1.AddCardToHand(unit);   // ← 总部也会被塞进手牌
        return;
    }
}
```

**这是全项目唯一能把一张「场上卡」变成「手牌卡」的出口。** 而控制台的执行入口是
`ParseAndExecuteEffect(cmd, myHq, null, myHq)`——`targets` 就是友方总部，
所以在那儿敲一句 `retreat` 就能把总部送进手牌。总部进了手牌之后彻底失控：
它被手牌布局引擎当普通手牌摆放，被 `DiscardRandomly` 这类「随机弃一张手牌」的效果
抽中，然后 `RemoveCard(myHq)` 直接把这一局判负。

**卡牌那条路是安全的**：`IsValidTarget` 里 `aUnit` / `aFriendlyUnit` / `aFrontLineUnit`
都要求 `isHq == HQ.normalCard`，`${allTargets.unit}` 也排除总部；而**仅有**两个用
`anyTarget` 的卡（暴风雪、合成橡胶）效果里没有 `Retreat` / `Discard`。
所以只有控制台能把它喂进去。

**修复**（三层，从源头到兜底）：

1. `RetreatUnit()` 开头拦截 `unit.isHq == HQ.hq`，直接返回并留日志——补上总闸；
2. `Player.AddCardToHand(cardBase_)` 对进手牌的总部留日志——这是进手牌的唯一入口，
   任何别的路径把总部送进来都会当场留痕；
3. `Player.DiscardRandomly()` 跳过手牌里的总部并留日志——双保险，
   避免「总部被当普通手牌弃掉 → 直接判负」这个最坏结果。

**回归**：`tests/verify_card_state_lifecycle.py`。

### 52. 弃置/阵亡动画中的卡压在手牌上面

**症状**：实机反馈「起飞的单位不应该挡在手牌上面」。

**根因**（查出来两处，其中一处是真 bug）：

1. **真 bug**：`battlefield_.cs` 里 `private int _discardZCounter = 50;`，阵亡/弃置动画
   每张卡取一个递增的层级。手牌是 **20**，50 已经在上头——于是弃牌动画整段盖住
   玩家正要点的牌。而且它**无限自增**，弃到第 10 张时已经 59。
2. **历史坑**：飞掠抬起的卡在场景里曾写死 `TopZIndex = 200`。**场景值优先于 C# 默认值**，
   所以那段时间它当然压住手牌；后来编辑器保存时因为「与 C# 默认值相同」把这一行省略了，
   值才回到默认的 15。

**修复**：

- 弃置层级收敛成 `DiscardZBase(11)`..`DiscardZMax(19)`，经 `NextDiscardZIndex()` 取号并
  **封顶**（宁可几张同层，也不许爬到手牌之上）；每批弃置前 `ResetDiscardZCounter()`，
  让批内顺序从下往上。两条弃置路径（`DiscardUnitsWithStagger` / `CardDiscardAndRemove`）
  走同一个取号函数。
- `FlyingEffect.TopZIndex` 默认值改成 **12**，并在注释里写明「上限卡死在手牌 20 以下」。
- 完整的层级表与那条唯一不变式（**除拖拽与选项 UI，任何临时浮起来的卡都必须低于手牌**）
  记进 `docs/NOTICE.md`。

**回归**：`tests/verify_card_layering.py`。

### 53. 投弹要等起飞演完才开始（太晚）；阵亡到爆炸之间没有停顿

**症状**：① 航弹要等卡飞起来整整一秒才开始扔；② 单位防御归零的一瞬间就消失并爆炸，
「中弹」与「爆炸」挤在同一帧里，看不清。

**修复**：

1. `FlyingEffect` 新增 `DuringRiseAsync` 钩子（与起飞**并行**，父类用 `WhenAll` 把它和
   升起等在一起）；`AirStrikeEffect` 把投弹从 `StayAsync` 挪到它上面——卡刚一离地，
   航弹已经在飞。用 `WhenAll` 而不是 fire-and-forget 是刻意的：否则会出现
   「卡已落回桌面、航弹还在半路」，而且特效节点回收时会把没播完的弹一起删掉。
2. `ProcessDeadUnitAsync` 拆出 `PlayDeathPresentationAsync`：卡在场上停留 1 秒
   （`DeathPresentationDelaySeconds`）-> 消失 -> 冒烟 + 爆炸声。调用方**不 await**
   它，阵亡检查的时序不被纯表现拖住；**停留前先 `setState(CardState.destroyed)`**，
   这样 `IsDeadPlacedUnit` 不会把它当新的阵亡重复统计，
   `TriggerUnitEffects`（只挑 `state == placed`）也不会点到这具「还没消失的尸体」。

**回归**：`tests/verify_attack_effects.py`、`tests/verify_attack_death_timing.py`、
`tests/verify_unit_dead_trigger.py`（测试 2 相应重写——原断言钉的是
「触发在 `RemoveCard` 之后」，`RemoveCard` 挪进表现方法后它会变成永远成立的空断言）。

### 54. 飞掠/空袭的卡落回桌面之后，仍然压在手牌上面

**症状**：上一轮把抬起卡的层级降到 12 之后，**动画期间**已经不压手牌了，
但**播完落回桌面，那张卡还是压在手牌上面**，而且不会自己恢复。

**根因**：攻击是从**拖拽释放**发起的（`battlefield_.cs` 的 `Attack(cardNowChoose, ...)`），
而拖拽期间这张卡被抬到 `ZIndex = 100`（同文件 `cardNowChoose.ZIndex = 100;`）。

`FlyingEffect` 在 `Play` 开头存下 `baseZIndex = source.ZIndex`——那时候拿到的就是 **100**。
播完 `finally` 里又 `source.ZIndex = baseZIndex` 还原回去，于是 100 被永久留在身上；
手牌才 20，表现就是「落回桌面还压着手牌」。

它不会自己好，因为**层级只在 `RefreshAllCardDisplayOrder()` 里被改回去，而那个函数只在
`_displayOrderDirty` 为真时跑**——特效结束时没有任何人把标记置脏。只有下一次玩家碰巧
悬停别的卡（`RefreshMyHand` 会标脏）才会顺手修好，所以现象看起来像是"有时候好有时候不好"。

**修复**：`FlyingEffect` 的 `finally` 里还原完之后补一句
`if (GetParent() is battlefield_ field) field._displayOrderDirty = true;`——
**只负责说「该重算了」，不自己猜一个层级**。层级数字的唯一权威是战场的
`RefreshAllCardDisplayOrder()`（见 `docs/NOTICE.md` 的层级表）；在特效里写死一个 10
等于把同一份约定配到第二处。

**回归**：`tests/verify_card_layering.py`。

**顺带记一笔（本次未改）**：`ResourceManager` 的卡牌对象池在发放/回收时**不重置 `ZIndex`**，
所以池里的卡会带着上一任的层级出来（`SetCardInformation` 已经归零了
`shouldBeRemoved` / `isDiscarding` / `isUnderCardEffect`，唯独漏了 `ZIndex`）。
目前靠「上场/进手牌都会把显示顺序标脏」自愈；要根治得在 `SetCardInformation` 里一起归零，
但那会给新卡一个 0 的瞬态层级（可能有一帧沉到背景后面），所以这次没动。

### 55. 阵亡但还留在场上的单位能被选为攻击/指令目标

**症状**：实机反馈「被破坏但是暂留在场上的单位可能因为手快而被选为攻击/指令的目标，
这些单位实际上已经死了，不该再被选为目标，筛选器也不应该选到他们」。

**背景**：第 53 条给阵亡加了 1 秒停留（卡先留在场上，再消失 + 爆炸）。
那一拍里它的状态已经是 `destroyed`，但**节点还挂在 `cardInPlaces` 和它自己的格子上**。

**四个漏洞**：

| 位置 | 问题 |
|---|---|
| `IsValidTarget()` | 不检查状态。它是**目标高亮、目标计数、指令落点校验**的共同入口，一处漏三处漏 |
| 攻击落点校验（`P_InPlaceUnit` 分支） | **根本不走 `IsValidTarget`**（那边是给指令用的目标类型筛选），只比了阵营——可以直接攻击尸卡 |
| `CheckCardClick()` | 遍历 `cardInPlaces` 不检查状态。点中尸卡会让 `cardNowChoose` 变成它，层级被抬到 100 |
| `HighlightValidTargets()` | 只遍历 `placed`，所以尸卡**不会被变灰**——周围全灰它却保持原色，看起来像「这个能打」 |

**修复**：加**一个**判据 `CanBeSelected(card)`（`state != CardState.destroyed`），四处引用它：

1. `IsValidTarget()` 第一句 `if (!CanBeSelected(card)) return false;` —— 一次堵住高亮 / 计数 / 指令落点；
2. 攻击落点分支加 `CanBeSelected(result.GetMyCard())`；
3. `CheckCardClick()` 的命中判断前面加 `CanBeSelected(card)`；
4. `HighlightValidTargets()` 的筛选条件加上 `CardState.destroyed`，让尸卡跟着一起变灰。

判据只写一处是这个修法的重点——同一个规则在四个地方各写一遍，下次改状态机必然漏一个。

**回归**：`tests/verify_dead_unit_targeting.py`（15 条）。
