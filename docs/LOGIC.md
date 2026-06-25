# 逻辑功能文档

## 一、效果脚本系统

### 变量替换 (ReplaceVariables)

位置：`battlefield_.cs` `ReplaceVariables()` (line 1397)

| 变量名 | 说明 |
|--------|------|
| `&result` | 当前效果段的result值 |
| `&targetsCount` / `&targets.count` | 目标列表数量 |
| `&sourceAttack` / `&source.attack` | sourceCard的攻击力 |
| `&sourceDefence` / `&source.defence` | sourceCard的防御力 |
| `&sourceCost` / `&source.cost` | sourceCard的费用 |
| `&lifeTime` | 目标单位存活回合数（优先targets[0]，否则sourceCard） |
| `&attackCountThisTurn` | 本回合该卡作为攻击方的战斗次数 |
| `&overflow` | 上次攻击溢出的伤害值 (lastOverflowDamage) |
| `&lastDamage` | 上次战斗攻击方造成的伤害值 |
| `&LastdeadFriendlyLandUnit` | 上一个死亡的友方陆军单位ID |
| `&fieldFriendUnitCount` / `&field.friend.unit.count` | 友方场上非HQ单位数量 |
| `&fieldEnemyUnitCount` / `&field.enemy.unit.count` | 敌方场上非HQ单位数量 |
| `&fieldFriend{Type}Count` | 友方场上某类型单位数量(如&fieldFriendTankCount) |
| `&fieldEnemy{Type}Count` | 敌方场上某类型单位数量 |
| `&friendHqDefence` / `&friend.hq.defence` | 友方总部防御力 |
| `&enemyHqDefence` / `&enemy.hq.defence` | 敌方总部防御力 |
| `&friendCommandPoint` | 友方当前指挥点 |
| `&friendCommandPointMax` | 友方最大指挥点 |
| `&friendHandCount` | 友方手牌数量 |
| `&friendDeckRemainingCount` | 友方卡组剩余数量 |
| `&theNumberOfSkirmisher` | 场上轻步兵单位的数量 |
| `&任意名称` | 自定义内存变量（通过 `SetMemory()` 设置） |

### 效果脚本解析 (ParseAndExecuteEffect)

位置：`battlefield_.cs` line 3290-4466

解析流程：
1. **去除方括号**：`StripBracketsOutsideQuotes()` 移除 `[icon=...]` 元数据
2. **去除时间前缀**：剥离 `Deployed:` 等前缀（冒号外的部分）
3. **逗号分割**：用 `SplitEffectString()` 按逗号分割为多个段
4. **每段处理**：按 `|` 分割为指令，预收集 `&` 结尾的跳转标签
5. **指令循环**：处理 `foreach`/`End&` 循环嵌套、`if()` 条件跳转、各指令

### 效果指令一览

位置：`battlefield_.cs` `ParseAndExecuteEffect()` 内

| 指令 | 功能 |
|------|------|
| `this` | 设置 targets 为 sourceCard 自身 |
| `target` | 重置 targets 为传入的原始 targets |
| `myHq` / `enemyHq` | 设置 targets 为友方/敌方总部 |
| `GetTargetByIndex(n)` | 从 targets 中取第n个作为新 targets |
| `GetCardBeingAddToSupportLine` | targets 设为上一个加入支援阵线的卡 |
| `Heal(n)` / `damage(n)` | 为所有 targets 增减防御 |
| `GetAttack(n)` / `LoseAttack(n)` | 为所有 targets 增减攻击 |
| `SetDefence(n)` | 设置 targets 防御力为n |
| `addDefence(n)` | 同 Heal |
| `setResult(n)` | 设置 result 变量值 |
| `SetMemory(name, n)` | 设置自定义内存变量 |
| `getCount($selector)` | 计数字段上匹配selector的单位 |
| `setTargets($selector)` | 按selector设置targets列表 |
| `GetEffect("effectString")` | 为targets附加效果字符串 |
| `AddTrait(name)` / `RemoveTrait(name)` | 添加/移除targets特性 |
| `Refresh` | 刷新targets的行动次数 |
| `Retreat` | 使targets撤退（前线→支援线，支援线→手牌/弃牌） |
| `Discard` | 标记targets为待弃置 |
| `drawCard` | 抽result张卡（最少1张） |
| `DrawUnitCards(n)` | 抽n张单位卡 |
| `DrawACard(name, n)` | 抽n张ID含name的卡 |
| `DrawACardWithType(type, n)` | 抽n张指定类型的卡 |
| `AddToHand(id, count)` | 向手牌添加count张指定ID的卡 |
| `addToSupportLine(id)` | 向友方支援阵线添加指定ID的卡 |
| `addToEnemySupportLine(id)` | 向敌方支援阵线添加指定ID的卡 |
| `addToDeck(id)` | 向卡组添加指定ID的卡并洗牌 |
| `ShuffleIntoDeck` | 将targets中的单位洗入卡组 |
| `GetCardsShuffledIntoDeck` | 获得上次ShuffleIntoDeck洗入的卡列表 |
| `Play` / `Play(selector)` | 从手牌免费打出一张卡 |
| `Choose(a, b)` | 显示2选1界面，执行选中卡效果 |
| `Develop` / `Develop(selector)` | 显示最多3张随机卡选1加入手牌 |
| `KillAllTargets` | 直接清空targets的防御 |
| `HealAllTargets` | 将targets恢复到历史最大防御 |
| `GetAllFriendUnits` / `GetAllEnemyUnits` | 所有友方/敌方非HQ单位 |
| `GetAllFriendTargets` / `GetAllEnemyTargets` | 所有友方/敌方单位+HQ |
| `GetRandomFriendUnit` / `GetRandomEnemyUnit` | 随机友方/敌方非HQ单位 |
| `GetRandomFriendTarget` / `GetRandomEnemyTarget` | 随机友方/敌方单位+HQ |
| `GetRandomNumber(min, max)` | 随机整数存入result |
| `GetFriendHq` / `GetEnemyHq` | targets设为友方/敌方总部 |
| `GetPoint()` / `GetPointMax()` | 读取指挥点/最大点存入result |
| `AddPoint(n)` / `AddPointMax(n)` | 增加指挥点/最大点 |
| `losePointAtNextTurnBegin(n)` | 下回合开始时失去n点指挥点（不足则清零） |
| `DiscardRandomly(n)` | 随机弃n张手牌 |
| `DiscardWithName(pattern, n)` | 弃ID含pattern的n张手牌 |
| `GetCardsBeingTreated` | targets设为最近抽到的卡列表 |
| `If(condition)label` | 条件满足则跳转到标签 |
| `foreach ... End&` | 遍历当前targets执行循环体 |
| `displayAllCardState` | 调试：打印所有单位状态 |

### 条件判断 (EvaluateCondition)

位置：`battlefield_.cs` line 4764

支持格式：
- 数值比较：`result>n`、`target.attack<=n`、`target.defence==n`、`target.cost>=n`、`targets.count<n`
- 布尔判断：`target.isFriend`、`target.isEnemy`、`source.isFriend`、`source.isEnemy`
- 类型匹配：`target.cardType==Tank`
- ID匹配：`target.name==cardId`
- 变量引用：`&variableName`
- 表达式计算：支持 `+-*/%()` 和 &变量

### Target Selector (GetTargetsFromSelector)

位置：`battlefield_.cs` line 4653

Selector 使用点号分段过滤：`allTargets.unit.friend.Infantry`
- 首段：`allTargets` = 所有场上+HQ的卡
- 后续段：`unit`=非HQ / `hq`=总部 / `friend`=友方 / `enemy`=敌方 / 类型名=CardTypes过滤

---

## 二、回合流程

### OnNextTurnButtonPressed (battlefield_.cs line 2902)

完整回合顺序：
```
1. FriendlyTurnEnd  效果触发
2. TurnEnd  效果触发
3. 死亡检查
4. EnemyTurnBegin  效果触发
5. ⚠ IncrementLifeTime (ALL) — 第一次
6. TurnBegin  效果触发
7. FriendlyTurnBegin  效果触发
8. ApplyTurnStartTraits (Mobilize+1+1、前线去烟幕、守护刷新)
9. ⚠ IncrementLifeTime (ALL) — 第二次（疑为BUG，见BUGS.md）
10. 死亡检查
11. EnemyTurnAsync() (敌方AI行动)
12. player1.DrawCard() + AddPointMaxNatural()
```

### 敌方回合 (EnemyTurnAsync → EnemyPerformActionsAsync)

位置：`battlefield_.cs` line 2195 / 2673

1. `RefreshAllCardInField()` 刷新所有单位
2. `ApplyEnemyTurnStartTraits()` 敌方动员buff
3. `ExecuteEnemyActionQueue()` 执行行动脚本（tN/everyNt/ADD/default）
4. `EnemyPerformActionsAsync()` AI行动：
   - 阶段1：前线无我方单位时，把敌方支援线非空军单位推到前线
   - 阶段2：攻击（优先级：能一击杀死HQ > 能杀死单位 > 攻击HQ > 随机攻击）

---

## 三、战斗核心流程 (Attack)

位置：`battlefield_.cs` line 1788

流程顺序：
1. 控制锁定+死亡检查暂停
2. 检查 `CheckIfCanAttack`
3. 步兵/坦克攻击范围合法性检查（只能相邻阵线）
4. 目标烟幕检查
5. 守护保护检查
6. BePicked 时点 + 同仇触发
7. 预计算伤害（考虑重甲-1、免疫=0）+ 溢出量
8. Attacking / BeingAttacked 等时点效果触发
9. 应用伤害（`LoseDefence`）
10. 移除动员（受伤后）+ 移除烟幕（攻击后）
11. 冲击特性处理（无视伏击+反击免疫）
12. 反击计算（伏击先发反击、重甲/免疫影响）
13. 飞弹动画+音效
14. `HaveAttacked()` 标记 + `IncrementAttackCountThisTurn()`
15. trait闪烁
16. 战后移动限制（非坦克单位攻击后禁止移动，奋战例外）
17. 恢复死亡检查+单位死亡判定
18. 解锁控制

### 特性在战斗中的交互

| 特性 | 攻击方 | 被攻击方 |
|------|--------|----------|
| 重甲 | 使反击伤害-1 | 使攻击伤害-1 |
| 免疫 | 免疫反击伤害 | 免疫攻击伤害 |
| 冲击 | 不受反击，攻击后失去 | - |
| 伏击 | - | 先造成反击伤害，杀死攻方则免伤 |
| 烟幕 | 攻击后失去烟幕 | 不可被选为目标 |
| 守护 | - | 两侧有守护单位时不可被攻击 |
| 动员 | - | 受到伤害后消失 |
| 同仇 | - | 被指向时，所有友方同仇+1+1 |

---

## 四、移动系统 (Move)

位置：`battlefield_.cs` line 2027

移动规则：
- 友方单位：手牌→支援阵线（部署） / 支援阵线→前线（推进）
- 敌方单位：支援阵线→前线（推进）
- 前线单位不可再移动
- 部署时触发 `Deployed` / `FriendlyTankDeployed` / `FriendlyInfantryDeployed` 时点
- 推进时触发 `Moving` 时点
- 移动后：非坦克单位攻击力归零
- 烟幕：首次移动失去 + 进入前线失去

### 阵线规则

- 三条阵线各5格
- 步兵/坦克只能攻击相邻阵线单位
- 空军(Plane/Bomber)和火炮(Artillery)可攻击任意阵线

---

## 五、内存变量系统

位置：`battlefield_.cs` line 1533-1552

| 方法 | 说明 |
|------|------|
| `SetMemory(name, value)` | 设置自定义变量 |
| `ReadMemory(name)` | 读取自定义变量（不存在则返回0并自动创建） |

变量通过 `&variableName` 在效果脚本中引用，未定义时默认值为0。

---

## 六、攻击次数计数

| 方法 | 位置 | 说明 |
|------|------|------|
| `IncrementAttackCountThisTurn()` | cardBase_.cs line 376 | 攻击次数+1 |
| `ReadAttackCountThisTurn()` | cardBase_.cs line 368 | 读取攻击次数 |
| `RefreshUnit()` | cardBase_.cs line 96 | 重置moveAble=1, attackAble=1, 奋战=2, 恢复伏击 |

---

## 七、时点系统 (TriggerUnitEffects)

位置：`battlefield_.cs` line 1728

所有时点（对应效果脚本前缀，如 `Deployed:` `Attacking:` 等）：

| 时点前缀 | 触发时机 |
|----------|----------|
| Deployed | 卡牌从手牌部署到场上 |
| FriendlyTankDeployed | 友方坦克部署 |
| FriendlyInfantryDeployed | 友方步兵部署 |
| FriendlyTurnBegin | 友方回合开始 |
| FriendlyTurnEnd | 友方回合结束 |
| TurnBegin | 任意回合开始 |
| TurnEnd | 任意回合结束 |
| EnemyTurnBegin | 敌方回合开始 |
| Attacking | 单位发起攻击时 |
| BeingAttacked | 单位被攻击时 |
| BecomingAttackTarget | 成为敌方攻击目标时 |
| FightingInfantry | 对战步兵时 |
| AttackingHq | 攻击总部时 |
| FriendlyUnitAttacking | 友方单位发起攻击 |
| EnemyUnitAttacking | 敌方单位发起攻击 |
| FriendlyUnitBeingAttacked | 友方单位被攻击 |
| EnemyUnitBeingAttacked | 敌方单位被攻击 |
| Moving | 单位移动（非部署）时 |
| Dead | 单位死亡时 |
| FriendlyUnitDead | 友方单位死亡时（所有剩余单位触发） |
| EnemyUnitDead | 敌方单位死亡时（所有剩余单位触发） |
| BeingAddedToField | 单位被加入战场时 |
| BePicked | 被指向（被选为目标时） |

---

## 八、同仇特性 (SharedHatred)

位置：`battlefield_.cs` `TriggerSharedHatred()` (line 3092)

触发时机：BePicked 时点（被攻击选中 / 被友方指令选中）

效果：被指向的单位若具有 SharedHatred 特性，所有与该单位同阵营的同仇单位获得 +1 攻击 +1 防御（被指向的单位自身除外）。
