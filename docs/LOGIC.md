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
| `&lifeTime` | 目标单位存活回合数（优先targets[0]，否则sourceCard）。取值含义见下方「`&lifeTime` 的计数约定」 |
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
| `&hp` | 战役血量——**世界地图上心形图标后面那个数**，即「还能失败几次」（`BattleStateManager.Hp`，与 `WorldMap.RefreshHp()` 读的是同一个字段）。非战役模式（直接跑战场场景调试）没有血量概念，会停在 `InitialHp` |
| `&theNumberOfSkirmisher` | 场上轻步兵单位的数量 |
| `&任意名称` | 自定义内存变量（通过 `SetMemory()` 设置） |

> ⚠️ **变量名里不能带点。** `ReplaceVariables` 扫描变量名时只接受字母、数字与下划线
> （`battlefield_.cs:1408`），遇到 `.` 就截断——所以 `&target.attack` 解析出的名字是
> `target`，不是已知变量，会走「自定义内存变量」分支返回 **0**，指令静默失效。
>
> 上表中所有 `A / B` 写法**只有 A 合法**（`&sourceAttack`、`&targetsCount`、
> `&fieldFriendUnitCount`、`&friendHqDefence`…）；带点的 B 形式**一个都不能用**。
> 代码里 `ReplaceVariables` 的 `case "target.attack":` 一类分支是**不可达的死代码**，
> 不要照它写卡。`[全面总攻]` 的两张辅助卡曾因写成 `&target.attack` 而完全无效；
> `card.ini:1855`、`card.ini:2301` 两张老卡也有同一处错误。
>
> 权威写法见 `bin/timesList.ini` 的 `[keys]` 段。

#### `&lifeTime` 的计数约定

`lifeTime` 在 `SetCardInformation()` 中初始化为 0，之后**每轮在时点触发之后**由 `RunTurnTransitionAsync()` 各加一次（敌方单位在敌方回合开始后、友方单位在友方回合开始后）。因此**时点里读到的值 = 该单位已经在场度过的完整回合数**：

| 时点 | 单位创建当回合 | 下个回合 | 再下个回合 |
|------|---------------|---------|-----------|
| `FriendlyTurnEnd` | 0 | 1 | 2 |
| `FriendlyTurnBegin` | 0 | 1 | 2 |

即**「第 N 个回合」对应 `&lifeTime == N-1`**。写卡时按此换算，例如「存活的第三个回合开始时」应写 `if(&lifeTime!=2)skip&`（见 `[i38]`）、「下个友方回合结束时消灭自身」写 `if(&lifeTime==0)Jump`（见 `[t70_机动防御]`）。

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

**指令名拼写规则（易错，务必对照 `battlefield_.cs` 的 `ConsoleCommands` 数组）**

- 比较使用 `OrdinalIgnoreCase`，所以大小写无所谓，但**拼写必须完全一致**。
- 防御统一用**英式 `defence`**。`defense` 只是卡牌配置的键名（`defense = 5`），**不能作为指令名**。
- **不存在 `GetDefence`**：增加防御请用 `Heal(n)` 或 `addDefence(n)`。
- **不存在 `AddAttack`**：增加攻击请用 `GetAttack(n)`。
- 解释器对无法识别的指令**静默忽略**，写错不会报错，只会让效果无声失效。

**属性变更的结算时机不一致（写复杂效果时必须注意）**

| 立即生效 | 缓存到 `ChangeList`，由 `ExecChangeList()` 结算 |
|---|---|
| `LoseAttack`、`KillAllTargets`、`HealAllTargets`、`addCost`/`subCost`/`setCost` | `Heal`、`addDefence`、`damage`、`SetDefence`、`GetAttack` |

变量替换是**逐条指令**进行的（`battlefield_.cs:3699`），因此 `&target.defence` 读到的是**缓存结算前**的值。若要表达「攻击力等于加防后的防御力」，必须把增量显式算进去，例如合成橡胶：`setTarget|setResult(5)|Heal(&result)|LoseAttack(&targetAttack)|GetAttack(&targetDefence)|GetAttack(&result)`。
| `setResult(n)` | 设置 result 变量值 |
| `SetMemory(name, n)` | 设置自定义内存变量 |
| `getCount($selector)` | 计数字段上匹配selector的单位 |
| `setTargets($selector)` | 按selector设置targets列表 |
| `GetEffect("effectString")` | 为targets附加效果字符串 |
| `AddTrait(name)` / `RemoveTrait(name)` | 添加/移除targets特性 |
| `Refresh` | 刷新targets的行动次数 |
| `Retreat` | 使targets撤退（前线→支援线，支援线→手牌/弃牌）。**总部会被拒绝**——这是唯一能把场上卡变成手牌卡的出口，总部一旦进手牌就可能被「随机弃一张手牌」弃掉并直接判负 |
| `Discard` | 标记targets为待弃置 |
| `DiscardWithTarget` | 弃置targets（支持手牌，直接播放弃牌动画） |
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
| `GetHandMax()` | 读取手牌上限存入result。上限是 `Player.maxHandSize` 常量，脚本读不到，写「抽到手牌满」的效果必须用它，不要写死数字 |
| `AddPoint(n)` / `AddPointMax(n)` | 增加指挥点/最大点；`AddPointMax` 不会重复执行 `AddPoint`。`AddPoint` 的天花板是 `pointMaxMaxMax`(24) 而非当前上限 `pointMax`，即可把点数攒到本回合上限之上；逐回合的预算约束由回合开始的 `RefreshPoint()` 刷满提供 |
| `losePointAtNextTurnBegin(n)` | 下回合开始时失去n点指挥点（不足则清零） |
| `DiscardRandomly(n)` | 随机弃n张手牌，**弃的是「效果来源卡那一方」的手牌**。玩家卡上写它就是「自己弃自己」（如「抽3张弃1张」） |
| `DiscardPlayerRandomly(n)` | 随机弃**玩家**n张手牌，与来源阵营无关 |
| `DiscardWithName(pattern, n)` | 弃ID含pattern的n张手牌 |
| `GetCardsBeingTreated` | targets设为**最近入手的卡**（读 `Player.lastDrawnCards`） |
| `GetCardBeingAddToHand` | targets设为最近入手的那一张（读 `battlefield_.lastCardAddedToHand`） |
| `If(condition)label` | 条件满足则跳转到标签 |
| `foreach ... End&` | 遍历当前targets执行循环体 |
| `displayAllCardState` | 调试：打印所有单位状态 |

### 条件判断 (EvaluateCondition)

位置：`battlefield_.cs` line 4764

支持格式：
- 数值比较：`result>n`、`target.attack<=n`、`target.defence==n`、`target.cost>=n`、`targets.count<n`
- 布尔判断：`target.isFriend`、`!target.isFriend`、`target.isEnemy`、`target.isHq`、`!target.isHq`、`source.isFriend`、`source.isEnemy`
- 类型匹配：`target.cardType==Tank`
- ID匹配：`target.name==cardId`
- 变量引用：`&variableName`
- 表达式计算：支持 `+-*/%()` 和 &变量

### Target Selector (GetTargetsFromSelector)

位置：`battlefield_.cs` line 4653

Selector 使用点号分段过滤：`allTargets.unit.friend.Infantry`
- 首段：`allTargets` = 所有场上+HQ的卡 / `allCardInHand` = 所有手牌 / `deck` = 当前牌堆
- 后续段：`unit`=非HQ / `hq`=总部 / `friend`=友方 / `enemy`=敌方 / `land`=陆军 / `air`=空军 / `damaged`=防御力低于历史最大值 / 类型名=CardTypes过滤
- **阵线段**：`frontLine`=前线(Place6-10) / `supportLine`=友方支援阵线(Place11-15) / `enemySupportLine`=敌方支援阵线(Place1-5)。按 `GetMyPlace()` 是否属于该格子表筛选，**与阵营无关**，可组合成 `${allTargets.unit.frontLine}`。（代码里的字段名拼错作 `enemySupprotLine`，但选择器片段按正确拼写对外。）
- **特性段**：`UnitTraits` 的枚举名，如 `${allTargets.unit.Ambush}` 取场上所有伏击单位。识别顺序是**先试特性、再试卡牌类型**——`Enum.TryParse` 成功且非 `None` 才当特性，否则交给卡牌类型解析；两类都不认得才静默忽略。

**必须写成 `${...}`，花括号不能省。** `setTargets` 用的是正则
`\$\{([^}]*)\}`，只认带花括号的形式；写成 `$(...)` 或裸 `$xxx` 时正则不匹配，
`targets` 不会被赋值，后续指令遍历空列表——**整张卡毫无效果，且不报任何错**。
`[第227号命令]`、`[血洒长空]` 都曾栽在这里。

同理，选择器里**不认识的片段会被静默忽略**（走 `ParseCardTypeFromName` 返回
null 后不作处理），过滤条件凭空消失、结果集比预期大。写错片段同样不报错。
这两类问题由 `tests/verify_card_scripts.py` 静态守住。

**唯一的例外是 `Develop()` 的参数。** `Develop($选择器)` 走的是另一条解析路径：
代码对参数做 `Substring(1)` 后再交给本函数，所以那里**必须写不带花括号**的形式
（`$deck`、`$allTargets.unit`）；若写成 `${...}`，花括号会被当成片段名的一部分，
同样静默失效。`tests/verify_card_scripts.py` 已把 `Develop($...)` 从「裸 `$` 写法」
检查中排除，避免把它误报成漏花括号。

### Develop 与卡牌复制 `Copy()` (battlefield_.cs)

`Develop($选择器)` 的候选卡是**牌堆/手牌/场上的真实对象**。既不能改动它们，
也不能把它们直接交给选择界面与手牌（否则同一个对象会同时存在于两处：牌堆里
一张、手牌里一张）。所以三个分支产出的候选**统一先过一遍 `Copy(cardBase_)`**：

```csharp
cardsToShow = cardsToShow.Select(c => Copy(c)).Where(c => c != null).ToList();
```

`Copy()` 只做「建对象 + 搬数值」，**不调用任何会跑动画的 setter**：

| 搬运项 | 方式 | 为什么不能按 id 重读 |
|---|---|---|
| `cost`/`attack`/`defence` | 直接写字段 | 运行时被 setCost/subCost/伤害/治疗改过 |
| `effect` | 直接赋值 | `GetEffect(...)` 会往 `target.effect` 上追加字符串 |
| `traits` | `AddTrait(source.traits)` | 会同时初始化 `hasSmokeScreen`/`hasShock`/`hasMobilize`/`hasAmbushActive` 等运行时标志；只写 `traits` 字段会让复制出的伏击单位 `hasAmbushActive=false`，伏击静默失效 |

两点容易踩的坑：

1. **不能用 `SetCostValue()` / `SetDefence()` 搬数值。** 它们内部有
   `FlashAttributeWithColor` 与 `AnimateCostRoll`（都要 `CreateTween()`），
   而复制出来的卡此刻**还没进场景树**，直接报错并中断整个效果链。数值写字段，
   写完后补一句 `RefreshState()` 把三个 Label 同步过来（绕开 setter 就等于绕开了
   它的刷新副作用）。
2. **必须调 `PinDesignSize()`。** `cardbase.tscn` 的根 Control 是锚点布局
   （`anchor_right=0.112`、`anchor_bottom=0.267`），**尺寸由父节点 rect 算出来**。
   `ShowCardChoice` 会把候选卡 `Reparent` 到 `choiceLayer`（一个 `CanvasLayer`），
   尺寸不钉死就会跟着换父节点重算——而 `HandleChoiceCardClick` 判定「点到哪张卡」
   用的正是 `GetGlobalRect()`。这是「开发出来的卡点不中」的直接原因。
   `InitializeDeckFromIni` 的两条分支同样要钉，否则牌堆卡与手牌卡的命中框不一致。

### 「刚入手的卡」指针 `RecordCardsObtained()`

项目里有**两套互相独立**的指针，由两条不同指令读取，两者都必须写：

| 指针 | 读取它的指令 |
|---|---|
| `Player.lastDrawnCards` | `GetCardsBeingTreated`（经 `Player.GetLastDrawnCards()`，返回副本） |
| `battlefield_.lastCardAddedToHand` | `GetCardBeingAddToHand` |

**凡是让卡进入手牌的效果路径都要记一笔。** 漏记不会报任何错，只表现为
「后续指令作用到了别的卡上」——`[紧急投产]` 的
`Develop($deck)|GetCardsBeingTreated|setCost(0)` 就是这么减不到开发出来的那张卡上的：
`Develop` 当时两套都没写，`GetCardsBeingTreated` 于是返回**上一次抽到的卡**。

统一入口 `battlefield_.RecordCardsObtained(List<cardBase_> cards, IsFriend side)`，
取列表末张记入 `lastCardAddedToHand`，并按阵营写对应 Player 的 `lastDrawnCards`。
目前调用方：`Develop(...)` 的两个分支、「加入手牌」指令（`AddToHand`）。

**边界**：`DrawCard` / `DrawACard` 一系只写 `Player.lastDrawnCards`（抽牌语义），
不走本函数——`GetCardBeingAddToHand` 在纯抽牌后仍指向上一次 `AddToHand` 的结果。

结算时机：`setCost` 一类是**缓存型变更**（`AddChange`），由 `cardBase_.ExecChangeList()`
落地。`ParseAndExecuteEffect` 末尾就有 `await ExecuteChangeLists();`，
所以紧跟 `GetCardsBeingTreated` 的 `setCost` 会在本次效果结算完时生效。

---

## 二、回合流程

### OnNextTurnButtonPressed (battlefield_.cs line 2902)

完整回合顺序：
```
1. FriendlyTurnEnd  效果触发
2. 友方单位解除压制（Suppressed）
3. TurnEnd  效果触发
4. 死亡检查
5. EnemyTurnBegin  效果触发
6. ⚠ IncrementLifeTime（敌方单位）— 第一次
7. TurnBegin  效果触发
8. **`EnemyTurnAsync()`（敌方AI行动）**
9. **`RefreshCardsInField(IsFriend.friend)` 刷新友方单位**
10. FriendlyTurnBegin  效果触发
11. TurnBegin  效果触发
12. ApplyTurnStartTraits (Mobilize+1+1、前线去烟幕、守护刷新)
13. ⚠ IncrementLifeTime（友方单位）— 第二次（疑为BUG，见BUGS.md）
14. 死亡检查
15. player1.DrawCard() + AddPointMaxNatural()
```

> **第 9 步为什么必须在第 8 步之后、第 10 步之前**：行动能力是按阵营、在各自回合
> 开头刷新的（见下）。敌方回合里被效果刷进场的单位（例如近卫步兵272团的亡计拉出
> 的 IS-2），只有走到这一步才终于拿到 `attackAble/moveAble`。放到 FriendlyTurnBegin
> 之后也不行——时点里要读的 `attackCountThisTurn` 依赖刷新已经做过（刷新会把它清零）。

### 敌方回合 (EnemyTurnAsync → EnemyPerformActionsAsync)

位置：`EnemyTurnAsync` `battlefield_.cs` line 2768；`EnemyPerformActionsAsync` line 3450。

1. `RefreshCardsInField(IsFriend.enemy)` 刷新敌方单位
2. `ApplyEnemyTurnStartTraits()` 敌方动员buff
3. `ExecuteEnemyActionQueue()` 执行行动脚本（tN/everyNt/ADD/default）；固定`tN=ADD:`在触发时注册，并从当回合起每个敌方回合重复执行
4. `EnemyPerformActionsAsync()` AI行动循环

#### AI 行动循环：移动 → 攻击 → 有人动过就回到移动

    循环(最多 MaxEnemyActionRounds=20 轮):
        anybodyMoved    = EnemyAdvancePhaseAsync()    // 推进前线
        anybodyAttacked = EnemyAttackPhaseAsync()     // 攻击
        if (!anybodyMoved && !anybodyAttacked) return // 场上没变化 -> 收敛
        if (anybodyAttacked) await WaitForCorpsesClearedAsync()

| 方法 | 位置 | 返回值语义 |
|---|---|---|
| `EnemyPerformActionsAsync()` | 3450 | 无。循环本体 |
| `WaitForCorpsesClearedAsync()` | 3483 | 无。等场上所有 `destroyed` 状态单位被收走 |
| `EnemyAdvancePhaseAsync()` | 3505 | 这一轮**有没有人真的移动过** |
| `EnemyAttackPhaseAsync()` | 3549 | 这一轮**有没有人真的打出去过** |

**循环判据是「这一轮场上有没有变化」，不是「场上还有没有能移动的单位」。**
后者会死循环：空军/炮兵/已经在后方的单位永远满足 `CheckIfCanMove()`，
但推进阶段永远不会动它们。

**判据里必须有 `anybodyAttacked`。** 前线被我方单位占住时推进阶段是空转的
（提前 `return false`，压根没试），只看 `anybodyMoved` 的话第 1 轮攻击打死挡路的
之后就立刻退出，前线空出来了也不再推——那正是这个循环要修的场景。

**攻击轮之后必须 `WaitForCorpsesClearedAsync()`。** 阵亡有两个阶段
（`ProcessDeadUnitAsync`）：状态立刻变 `destroyed`，节点却要等
`PlayDeathPresentationAsync` 的那一拍（`DeathPresentationDelaySeconds`，实机约 1s）
结束才 `RemoveCard` 解绑格子。这一拍里格子还被尸体占着，而 `frontLine` 与 `Move`
都只看格子有没有卡，不等就会把尸体当成活人。等待有硬上限
`DeathSettleTimeoutSeconds`——`PlayDeathPresentationAsync` 在节点已失效时会直接
`return`、不执行 `RemoveCard`，尸体可能永远留在 `cardInPlaces` 里。

**只有坦克能体现这个循环。** 规则「步兵/火炮/飞机攻击后不能移动」在 `Attack`
尾部对这几类调 `HaveMoved()`，所以它们打完就再也推不动了；坦克不在其列，
「先打死挡路的、再推进上去」对坦克成立。验证用例因此用的是四号坦克。

**收敛性**：移动会 `HaveMoved()` 清 `moveAble`、攻击会 `HaveAttacked()` 减
`attackAble`，两者每回合只在开头刷一次，所以「移动 + 攻击」总次数有限，
每转一轮至少消耗掉一次。`MaxEnemyActionRounds` 只是保险丝，跑满会 `PushWarning`。

**推进阶段不补 `HaveAttacked()`**：`HaveMoved()` 已经是「本回合不能再移动 +
非坦克也不能再攻击」的唯一入口，再补一次会把 `attackAble` 减成 -1，
让「本回合额外 +1 次攻击」的效果白给。

#### 攻击优先级（`EnemyAttackPhaseAsync`）

能一击摧毁总部 → 能一击摧毁某单位 → 打总部 → 随机打一个合法目标。
四处分支全部走内部的 `Strike()` 局部函数，它统一转发给 `Attack` 并置
`anybodyAttacked = true`——漏记任何一处都会让循环提前收敛。

### 敌方行动脚本键 (enemyTurn.ini)

加载：`LoadEnemyActionQueue()` `battlefield_.cs` line 2189；执行：`ExecuteEnemyActionQueue()` line 2404。

| 键 | 语义 |
|---|---|
| `tN=动作` | 第N个敌方回合执行一次 |
| `tN=ADD:动作` | 第N回合把动作注册进永久队列，此后每个敌方回合都执行 |
| `everyNt=动作` | 第N、2N、3N…回合各执行一次 |
| `everyNt=ADD:动作` | 每个周期都往永久队列再追加一份同名动作 |
| `default=动作` | 当前回合没有任何`tN`命中时执行 |

前缀必须写成 `ADD:`（冒号）。写成 `ADD=` 不会被识别，会当作未知指令静默丢弃。

注意 `everyNt=ADD:` 的叠加行为：它不是在永久队列里维持一份，而是每个周期追加一份，第kN回合后该效果每回合执行k次。需要周期性增长时改用固定回合的 `tN=ADD:` 或纯周期性的 `everyNt=`。

### 战斗状态串行化

`CheckIfAnyUnitDiedAsync()`使用运行门闩合并重复请求，以固定点循环依次等待change list、Dead效果、移除和阵营死亡效果，不再异步递归。回合按钮使用`turnTransitionRunning`拒绝重复点击，Trait结算、死亡检查和抽牌均在同一可等待流程中完成。

`ForbidControl()` / `AllowControl()` 是**嵌套感知的控制锁**（按 `controlLockDepth` 计数），只有最外层 `AllowControl()` 才真正解锁、恢复 Next 按钮可点击。必须如此的原因：`Attack()` 结尾**无条件**调用 `AllowControl()`，而敌方回合里每一次 `Attack` 都嵌套在 `EnemyTurnAsync` 与 `OnNextTurnButtonPressed` 的禁止之下。若把它当成扁平标志，最内层的解锁会在每次敌方行动后把按钮置回可点击、下一次行动又立刻禁用，表现为**Next 按钮在敌方回合闪烁**。新增调用点时不必关心自己所处的嵌套层级，`Forbid` / `Allow` 配对调用即可；未配对的 `AllowControl()`（计数已为 0）行为与从前一致。

### 交互热路径

瞄准箭头复用曲线与多边形数组，并将箭身合并为一次多边形绘制；鼠标位置未变化时不重绘。卡牌和商店悬停移动会取消旧Tween。商店价格颜色只在点数或槽位状态变化时更新，属性Tooltip通过局部`_GuiInput`处理并缓存当前图标。

---

## 战役胜利返回地图

位置：`battlefield_.cs` `ReturnToWorldMapAfterVictory()`

1. 显示结算并等待确认。面板是 `bin/settlement_panel.tscn`，由 `End.cs` 填入数值；五个分量的分值来自 `bin/BattleScore.cs`，与 `CalculateMaterialPoints()` 同源，因此明细相加恒等于总额。调整物资点平衡只需改 `BattleScore` 的常量。

**对敌方总部的伤害**是其中一项得分来源，取自卡牌自身的 `totalDefenceLost`——只在 `cardBase_.LoseDefence()` 累加，而治疗走 `GetDefence`/`AddDefence` 另一条路，因此回血不会抵消它。这一点是必要的：没有该项时击杀是唯一得分来源，「敌方不派兵、只不断加固总部」的关卡（如加里宁）无论打得多好都必然结算为 0，玩家实际要打穿的是总部防御加上它每回合回的血。
2. 消耗 1 点区域战斗烈度。
3. 完成或跳过战后奖励。
4. 未通关时调用 `SceneLoader.ChangeSceneAsync()` 返回地图；最后一个区域烈度归零时改走通关流程。
5. `WorldMap` 复用 `BattleStateManager` 中的卡牌、事件和区域池缓存，不重复解析 INI。

### 区域战斗烈度

位置：`CardRestoration.cs` `ReadAreaIntensity()` / `EnsureAreaIntensity()` / `ConsumeAreaIntensity()` / `IsFinalArea()`，`WorldMap.cs` `OnAreaPressed()`，`ChooseMission.cs` `RefreshIntensityLabel()`。

烈度来自 `AreaPool.ini` 每个分区的 `areaTimes`，缺失时为 3。

1. 玩家点击区域按钮时，`EnsureAreaIntensity()` 首次把剩余烈度初始化为该区域的 `areaTimes`；已进入过的区域不会被重置。
2. 任务选择面板的 `intensityLabel` 显示「战斗烈度：当前值」。
3. 战斗胜利与事件完成都会调用 `ConsumeAreaIntensity()` 减 1；事件与战斗同等对待。
4. 归零时 `ConsumeAreaIntensity()` 复用 `AdvanceArea()` 关闭当前区域并解锁下一区域。
5. `IsFinalArea()` 以 `AreaOrder` 末项判定最后一个区域；该区域烈度归零时不解锁任何区域，而是进入通关流程。
6. 烈度与 `UnlockedArea` 同为进程内静态状态，重启游戏后重置。

### 整局进度重置

位置：`CardRestoration.cs` `ResetCampaignProgress()`，调用点为 `battlefield_.cs` `ReturnToStartMenuAfterDefeat()` 与 `bin/CampaignVictory.cs`。

整局结束（战斗失败或战役通关）返回开始菜单时立即重置本局进度，避免下一局继承上一局状态：

- 卡组：清空 `DeckCardIds`、`Deck`，并把 `IsDeckInitialized` 复位，使下一局重新读取 `deck.ini`
- 区域：仅第一个区域（`AreaOrder[0]`）解锁，清空 `_areaIntensity`
- 商店：清空 `StoreCardQueue`，`StoreCurrentSlots` 置空以重新生成
- 物资点与上局战斗统计清零；`SelectedEnemy`/`SelectedArea`/`IsCampaignMode` 复位；释放上一局的 `battlefield` 节点引用

**不重置** `card.ini` / `event.ini` / `AreaPool.ini` 的解析缓存（`_allCards`/`_allEvents`/`_areaPools`），它们属于配置而非本局状态。

`SettingsMenu` 返回开始菜单**不**触发重置，因为它不是整局结束。

### 战役通关

位置：`bin/CampaignVictory.cs`，对白 `dialogues/campaign_victory.dialogue`。

最后一个区域烈度归零时，战斗路径（`battlefield_`）与事件路径（`EventScene`）都会调用 `CampaignVictory.ShowAndReturnToMenu()`：先叠加暗幕（`Layer = 90`，低于对白气泡的 100），播放与副官的战斗总结对白，对白结束后返回开始菜单。

### 事件资源点效果

位置：`EventScene.cs` `ExecuteEffect()` / `AddMaterialPoints()`，`EventMaterialPoints.cs`。

事件选项可配置 `materialPoints(n)` 获得非负整数资源点。`EventMaterialPoints.TryParse()` 负责参数校验，`Add()` 使用 `long` 中间值并将结果限制在 `int.MaxValue`；`EventScene` 写入 `BattleStateManager.MaterialPoints` 后立即刷新世界地图 `pointNum`。非法参数不改变资源点，并通过 Godot 错误日志记录时间与代码位置。

---

## 历史战役难度

`AreaPool.ini` 将战役和事件分配至area1-7。战役ID对应的中文名称直接读取`enemyTurn.ini`中相应section的`name`，任务界面不显示section ID。行动脚本各键的语义与叠加行为见「敌方行动脚本键 (enemyTurn.ini)」。

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
9. **冲击判定+消耗**（攻击后失去冲击）
10. **伏击先制**：无冲击、且攻击方不是远程单位（火炮/轰炸机）、且防守者伏击可用时，先对攻击方造成反击伤害；若攻击方死亡，跳过本次攻击伤害
11. **攻击伤害**：攻击方存活时才对防守方执行 `LoseDefence`；受伤后移除动员
12. 移除烟幕（攻击后）
13. **普通反击**：无冲击且未触发伏击时，按兵种限制执行普通反击
14. 飞弹动画+音效（**一处选完，三种可能**）：
    - 攻击特效里带了自定义音效（`flying` / `airstrike` / `sfx(...)`）→ 它自己会响，不放别的；
    - 是 `TankAttack`（坦克与火炮）→ 放一声**按口径分档的开火音**，`PlayBattleSound(1)` 不响；
      命中音**不在这里**，等炮弹飞到了由 `BulletEffect` 自己响；
    - 其余 → 通用开火声（机枪「哒哒」）。

    判定读的是 `ResolveAttackEffect(from)` **解析后**的串而不是 `card.attackEffect` 原文：
    坦克/火炮卡上根本没写 `attackEffect`（吃默认），读原文会拿到空串，于是又叠一层机枪声。
    全文件只有一处 `PlayBattleSound(1);`。
15. `HaveAttacked()` 标记 + `IncrementAttackCountThisTurn()`
16. trait闪烁
17. 战后移动限制（非坦克单位攻击后禁止移动，奋战例外）
18. 恢复死亡检查+单位死亡判定
19. 解锁控制

### 特性在战斗中的交互

| 特性 | 攻击方 | 被攻击方 |
|------|--------|----------|
| 重甲 | 使反击伤害-1 | 使攻击伤害-1 |
| （兵种限制） | 火炮/轰炸机攻击时不受任何反击，也不触发对方伏击 | 轰炸机不能发动普通反击 |
| 免疫 | 免疫反击伤害 | 免疫攻击伤害 |
| 冲击 | 不受反击，攻击后失去 | - |
| 伏击 | - | 先造成反击伤害，杀死攻方则免伤；火炮/轰炸机攻击时不触发 |
| 烟幕 | 攻击后失去烟幕 | 不可被选为目标 |
| 守护 | - | 两侧有守护单位时不可被攻击；**火炮与轰炸机除外** |
| 动员 | - | 受到伤害后消失 |
| 同仇 | - | 被指向时，所有友方同仇+1+1 |

**免疫的作用范围**（`battlefield_.cs:1842` 与 `1898`）：免疫只把**战斗伤害**归零——

```
if (to.HasTrait(UnitTraits.Immunity)) attackDamage = 0;    // 1842，被打
if (from.HasTrait(UnitTraits.Immunity)) counterDamage = 0;  // 1898，打人时的反击
```

因此免疫单位**仍然会死**于：`damage(n)` 效果伤害、`KillAllTargets`/`KillAllTarget`（直接调 `LoseDefence(ReadDefence())`，不走战斗伤害）、以及自身防御被削到0（例如 `EnemyTurnBegin:this|damage(1)` 的自衰减）。设计「打不动」的单位时必须同时给出其退场途径。

**亡语时点**：`Dead` 触发时 `sourceCard` 就是死亡单位本身，因此 `&source.attack` 可以读到它生前的攻击力。`&source.attack` 解析到 `sourceCard.ReadAttack()`（`battlefield_.cs:1379-1381`）。范例：`de_karl` 的 `Dead:myHq|damage(&source.attack)`。

**一张卡可带多个时点**：`SplitEffectString` 同时跟踪 `()` 与 `[]` 的嵌套深度，因此 `[icon=...,description=...]` 元数据里的逗号不会把两个时点切开。

### 守护的豁免：火炮与轰炸机

判定集中在 `battlefield_.cs` 的 `IsTargetProtectedByGuardian(target, attacker)` 一处，
玩家侧的 `Attack()` 与敌方 AI 选目标都调它，所以规则天然对称，不会出现
「玩家能打、AI 不能打」。豁免本身写在 `cardBase_.IgnoresGuardian(CardTypes)`：

```csharp
return type == CardTypes.Artillery || type == CardTypes.Bomber;
```

**只看兵种、不看阵营**，且判定排在烟幕检查之前——排在后面的话，
目标带烟幕时函数会先返回，豁免被绕过。新增同类兵种时改这一个函数即可。

---

## 拖拽的收尾必须能被外部打断

位置：`battlefield_.cs` `CancelCurrentDrag()`

拖拽是「按下进入、松开退出」的一对事件：按下时把卡置为 `caught` /
`inplaceAndCaught` 并记进 `cardNowChoose`，松开时再落位。问题在于
**松开那一半可能永远不来**——截图工具（系统截图、Snipaste、QQ 截图等）会抢走鼠标，
Alt-Tab 会让窗口失焦。这时卡会永久停在 `caught`，而 `RefreshMyHand()` 对拖拽中的卡
是 `continue` 跳过的，于是它再也不会归位，显示层级也跟着乱。

所以收尾不能指望那个事件，改由外部主动撤。两条兜底：

| 触发 | 位置 | 覆盖的情形 |
|------|------|-----------|
| `_Notification(NotificationApplicationFocusOut)` | `battlefield_.cs` | Alt-Tab、被截图工具抢焦点 |
| 收到鼠标移动事件、但物理左键已不在按下状态 | `_Input` 开头 | 截图工具吃掉 mouse-up 但不抢焦点 |

`_Input` 里那条**必须放在 `ReadControlState()` 之前**——收尾不能被「当前不允许操作」
挡住，否则解锁前永远轮不到它。这与 `ShowCardChoice` 的模态点击同属一类：
**凡是「等一个可能永远不来的事件」的收尾，都要能被打断。**

---

## 单位卡的弃置动画节奏

位置：`battlefield_.cs` `DiscardUnitsWithStagger()` / `FinishUnitDiscard()`

`cardBase_.DiscardCard()` 单张就要 3.5 秒（飞入 1s + 停留 1s + 飞出 1.5s）。
从前 `ProcessDeadUnitsOnceAsync` 里是一张播完再播下一张，撤退 3 个单位就要干等 10 秒。

现在改为**卡与卡之间错开 `DiscardStaggerSeconds`（0.5s）起飞**，动画互相重叠，
最后 `Task.WhenAll` 统一等待——全部播完函数才返回，调用方的死亡检查时序不变。
单张时不平白多等，行为与从前一致。

关战斗能力（`DisableCombatAbility()`）在起飞**之前**一次性做完：动画期间这些卡还挂在
`place` 上、`state` 仍是 `placed`，不能有任何一张处在「已宣布弃置、却仍可被攻击」的中间态。
`isDiscarding` 也在这里置位，让刷新显示顺序跳过它们。

---

## 关卡的「开局效果」`battleStart=`

位置：`cards/enemyTurn.ini` 每个 section 的可选键；执行在
`battlefield_.cs` `RunBattleStartEffectAsync()`，由 `StartBattleAsync()` 在
`_Ready` 末尾接进异步链。

这是「游戏开始时，若 xxx 则 xxx」的通用入口：

- **只结算一次**，不进 `enemyActionQueue`——队列里的每一条都会作为「敌方意图」
  显示在左侧面板上，开局效果不是意图。因此它在 `LoadEnemyActionQueue` 里被单独摘出，
  存进 `_battleStartEffect`（换关卡时一并清空）。
- **来源卡固定为敌方总部**，所以 `enemyHq` 指敌方、`myHq` 指玩家，与其它效果一致。
- **条件用效果脚本自己的 `if(...)` 跳转写**，不需要为关卡规则加特例代码。
- **时序**：排在起手抽牌与换牌之前（`StartBattleAsync` 先 `RunBattleStartEffectAsync`
  再 `StartOpeningHandAsync`），玩家在换牌界面上看到的就是最终数值。

```ini
[berlin_final_battle]
name=攻克柏林
battleStart=myHq|heal(&hp*10)[icon=heal,description=开局:每有1条命,友方总部额外获得10点防御力]
```

`&hp` 是战役血量，即世界地图上心形图标后面那个数。

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
| FriendlyCommandPlayed | 友方打出指令时（在 `ExecuteCommandAndDiscard` 中、该指令自身效果结算之后触发） |

> ⚠️ **时点里的效果一样能打死人，跑完时点必须有人做死亡检查。**
> `damage()` / `Heal()` 一类是**缓存型变更**，由 `ParseAndExecuteEffect` 末尾的
> `ExecuteChangeLists()` 落地——所以时点跑完时目标防御可能已经是 0，但**单位不会自己死**，
> 必须由调用方 await `CheckIfAnyUnitDiedAsync()`。
> `[女狙击手]` 的 `FriendlyCommandPlayed:GetRandomEnemyTarget|damage(2)` 就栽在这上面：
> `ExecuteCommandAndDiscard` 里的死亡检查排在时点**之前**，于是单位顶着 0 防杵在场上
> （见 `BUGS.md` 第 48 条）。新增「会造成伤害的时点」时，务必确认时点之后有检查。
>
> 已经写对的参照：`Move()`（`Moving` 时点 → `ResumeDeathCheck()` → 查死亡）、
> `AddCardToPlace()`（`FriendlyUnitEnteringField` 之后同样）。
> `TriggerFriendlyCardDrawn()`（`FriendlyCardDrawn:` 时点）原先也是发后不理、之后无人查死亡，
> 已一并改为 `async Task` 并在时点后补检查，见 `BUGS.md` 第 49 条。

**前缀必须与上表逐字一致（含大小写）。** `TriggerUnitEffects` 的判定是：

```csharp
var prefix = segment.Split(":")[0].Trim();
if (prefix != triggerPoint) continue;      // 区分大小写的精确比较
```

写错时**不报任何错**，只是该段效果永远不执行。`[i1005]` 的亡记曾写成 `dead:`（小写），而死亡触发点传的是 `Dead:`，于是整段效果一次都没跑过；`[雅克9]` 同样如此。上面 `Times` 枚举里的成员名是小写，但它**从未被使用**，不构成命名依据——权威写法就是本表的 PascalCase。

`tests/verify_timing_prefixes.py` 会把三个配置文件里所有时点前缀与代码中实际使用的触发点逐一比对，大小写不符或名字未知都会报错。

---

## 八、同仇特性 (SharedHatred)

位置：`battlefield_.cs` `TriggerSharedHatred()` (line 3092)

触发时机：BePicked 时点（被攻击选中 / 被友方指令选中）

效果：被指向的单位若具有 SharedHatred 特性，所有与该单位同阵营的同仇单位获得 +1 攻击 +1 防御（被指向的单位自身除外）。

---

## 血量系统

血量是「整局还能失败几次」的计数器，存在 `BattleStateManager`（`bin/CardRestoration.cs`）。

| 项 | 值 | 说明 |
|---|---|---|
| 开局 | 5 | `InitialHp` |
| 上限 | **无** | 事件与商店都能加，可以攒到 5 以上 |
| 下限 | 0 | `AddHp()` 里钳住，不会出现负血量 |
| 商店价格 | 200 资源点 / 1 点 | `HpPrice` |

### 战斗失败不再立刻结束本局

原先友方总部阵亡就直接 `ReturnToStartMenuAfterDefeat()`（含重置整局进度）。现在插入一层：

```
友方总部阵亡
  └─ 战役模式？
       ├─ 否（直接跑战场场景调试）→ 原行为：回主菜单
       └─ 是 → LoseHpOnBattleDefeat() 扣血
                 ├─ 血量 > 0 → ReturnToWorldMapAfterDefeat()：显示撤退面板 → 回世界地图继续
                 └─ 血量 = 0 → ReturnToStartMenuAfterDefeat()：游戏结束，回主菜单并重置进度
```

**扣血规则**（唯一实现在 `BattleStateManager.LoseHpOnBattleDefeat()`）：

| 情形 | 扣血 |
|---|---|
| area7（终局区域，`IsFinalArea`） | 直接清零 |
| boss 战 | -2 |
| 其余战斗 | -1 |

**boss 判定**：`IsBossBattle()` 拿「本次抽中的关卡名」与该区域 `AreaPool.ini` 里配的 `boss` 比对。
boss 按约定不写进 `enemyN`（由 `MissionDrawer` 在烈度为 1 时单独提供），所以只能这样反查。
area7 的 `berlin_final_battle` 没配 boss，由 `IsFinalArea` 特判，走不到 boss 分支。

**战败与战胜的差别**（`ReturnToWorldMapAfterDefeat`）：不发战后奖励、不结算物资点，
但**烈度照常消耗 1 点**——否则玩家可以靠一直输来无限重试、区域永远推不下去。

### 事件与商店里的血量

- 事件：`hp(n)`，正数加血、负数扣血，与 `materialPoints(n)` 写法一致
- 商店：`store.tscn` 的 `BuyHp` 按钮，逻辑在 `StoreHpShop.cs`（`Store` 的 partial 部分）

世界地图左上角用 `hearts.png` + 数字显示（`worldMap.tscn` 的 `heartPic` / `hpNum`）。
用数字承载数量而不是排 N 颗心，是因为血量无上限。

## 事件选项的资源点门槛与卡牌预览

- **门槛**：选项效果里 `materialPoints` 的负数部分累加即该选项的花费（`EventEffectRunner.ParseMaterialCost`），
  余额不足时按钮置灰并显示「（需要 N 资源点）」。执行侧仍有下限 0 的兜底。
- **预览**：含 `replaceCard` / `replaceRandomCard` 的选项，悬浮时弹出 `bin/event_card_preview.tscn`，
  按 id **去重后最多显示 2 张真卡面**。没有底板、没有边框、没有文字——所以去重只保证
  「20 张同名卡不会画 20 遍」，不再标出数量。

### 弹窗里排卡的标准写法（踩过坑，别再自己发明）

`EventCardPreview.AddCardFace()` 与 `ChooseSomeCard.BuildGrid()` 是同一套写法，改的时候照着抄：

```csharp
card.SetAnchorsPreset(LayoutPreset.TopLeft);   // 1 钉锚点
card.Size = cardBase_.DesignSize;              // 2 原生 180x240
card.Scale = new Vector2(CardScale, CardScale);// 3 再缩放（卡面内部坐标是按原生尺寸摆的）
container.AddChild(card);                      // 4 入树
card.SetCardInformation(data);                 // 5 填数据（必须在树里，否则字体测不准）
card.Position = new Vector2(x, y);             // 6 最后定位
```

**容器必须是普通 `Control`。** 曾经用 `HBoxContainer` 装预览卡，它**强行接管子节点的 Position
与 Size**，卡被撑到容器大小、整张糊满屏幕。`ChooseSomeCard` / `DisplayCard` 也都用普通 `Control`
加显式 `Position`，没有一个用 Container 类。

`Scale` 只影响绘制、不影响布局占位，所以缩放后的实际占位是
`DesignSize * scale`，命中框要另算——`ChooseSomeCard` 为此单独铺了一层透明 `ColorRect` 作点击区
（`pfX/pfY` 那两行就是在算这个偏移）。预览面板不吃鼠标，所以不需要点击区。
