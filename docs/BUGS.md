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
