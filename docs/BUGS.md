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
