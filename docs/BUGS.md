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

### 24. LoadCardDataCache 缺失 IconPath 导致卡图不显示 (WorldMap.cs line 91)

commit `ebc702f`（CardParser 重构）在替换 `GetRarity`/`GetTypes` 等本地方法为 `CardParser.xxx` 调用时，误删了 `cd.IconPath = configFile[section.Key]["icon"].GetString();` 这一行。

**影响**: `WorldMap.LoadCardDataCache()` 加载到 `BattleStateManager` 缓存的 `CardData` 均无 `IconPath`（默认空字符串）。战役模式下 `battlefield_._Ready()` 从缓存取卡牌数据，`SetCardInformation` 将空 `IconPath` 传给 `cardBase_`，`RefreshState()` 中 `FileAccess.FileExists("")` 返回 false，不加载任何纹理，卡图为空白。冷启动直接运行战场场景时走 `battlefield_.cs:479` 的备用加载路径（有 `IconPath` 赋值），故该路径不受影响。

**修复**: 在 `WorldMap.cs` `LoadCardDataCache()` 中补回 `cd.IconPath = configFile[section.Key]["icon"].GetString();`。

### 25. ShowSettlement 结算面板位置错误+缺少确认按钮 (End.cs)

`ShowSettlement` 使用了 `SetAnchorsPreset(Control.LayoutPreset.Center)` 后又设置了相对坐标 `Position = (viewSize.X/2 - 200, 120)`，Center 预设将锚固点设为 (0.5,0.5)，最终 X = viewSize.X - 200，面板跑到了屏幕右下角。

此外，原流程在 `RemoveCard` 中同步调用 `ShowSettlement` 后立即 fire-and-forget `ReturnToWorldMapAfterVictory`（直接跳到奖励选择），缺少结算→用户确认→奖励的等待环节。

**修复**: (1) 去掉 `SetAnchorsPreset`，直接用 `(viewSize - panelSize) / 2` 居中定位；(2) 添加确认按钮，`ShowSettlement` 改为 `async Task` 等待按钮按下；(3) 将 `ShowSettlement` 调用移入 `ReturnToWorldMapAfterVictory` 最前面，确保确认后才进入奖励选择。
