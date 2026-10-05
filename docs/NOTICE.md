# 修改注意事项

> ⚠️ **每次开始修改代码之前必须先阅读本文档！**

---

## 一、卡牌生命周期（最高优先级）

### 卡牌入场
- 必须调用 `battlefield_.AddToBattleField(card)` 将卡牌添加到战场
- 该方法执行：`AddChild(card)` + `cardInPlaces.Add(card)`
- 不要直接手动 `AddChild` 而不加入 `cardInPlaces`，否则排序/死亡检查会遗漏

### 卡牌出场/移除
- 必须调用 `battlefield_.RemoveCard(card)` 移除卡牌
- 该方法执行：检查敌方HQ死亡 → `cardInPlaces.Remove(card)` → `card.Dead()` → 刷新守护状态
- 【注意】`RemoveCard` **不会**主动调用 `place_.UnbondCard()`，但 `card.Dead()` 内部会解绑
- 临时选择卡（ShowCardChoice中的卡）**不要**调用 `RemoveCard`，应从父节点剥离并回收到对象池
- 弃牌走 `CardDiscardAndRemove(card)` 带弃牌动画

### 卡牌死亡
- `cardBase_.Dead()` 会解绑 place、设状态为destroyed、回收到 ResourceManager 对象池或 QueueFree
- 调用 `Dead()` 后不要再访问该卡牌对象

---

## 二、SetMyPlace 安全使用

- **绝对不能传 null**！`SetMyPlace(null)` 会在 `place.BondCard(this)` 处抛出 NullReferenceException
- 如需清空卡牌的位置绑定，请使用 `ClearMyPlace()`
- 在 `_Ready()` 中已注册好 `battleField` 引用

---

## 三、特性(Trait)修改规范

### 修改 traits 时必须检查 ConsoleCommands
- 如果新增/修改了特性名称，必须在 `battlefield_.cs` 的 `ConsoleCommands` 数组中同步更新相关指令名称
- 当前 ConsoleCommands 位置：`battlefield_.cs` line 923-936
- `AddTrait(name)` 和 `RemoveTrait(name)` 指令依赖 `GetUnitTraits()` 解析特性字符串
- 新增特性必须在以下位置同步：
  1. `cardBase_.cs` 的 `UnitTraits` 枚举（Flags，按位标记）
  2. `cardBase_.cs` 的 `AddTrait()` — 初始化运行时状态
  3. `cardBase_.cs` 的 `RemoveTrait()` — 清理运行时状态
  4. `cardBase_.cs` 的 `BuildTraitPrefix()` — 描述文本
  5. `cardBase_.cs` 的 `GetTraitDescription()` — 悬停提示
  6. `cardBase_.cs` 的 `IconCache.TraitIcons` — 图标映射
  7. `cardBase_.cs` 的 `GetTraitIconTint()` — 状态颜色
  8. `cardBase_.cs` 的 `GetAllAttributes()` — attribute列表

---

## 四、效果指令系统

### 新增效果指令必须修改的位置
1. `battlefield_.cs` `ParseAndExecuteEffect()` (~line 3372-4459) — 添加指令处理分支
2. `battlefield_.cs` `ConsoleCommands` 数组 — 添加用于Tab补全的指令名
3. 如需支持 &变量替换：`battlefield_.cs` `ReplaceVariables()` (~line 1397)

### 效果脚本语法规则
- 逗号 `,` 分割不同段（最高优先级分隔符）
- 竖线 `|` 分割不同指令
- `&` 结尾为跳转标签定义；`End&` 为 foreach 结束标记
- `[icon=xxx,description=yyy]` 为属性元数据（会被 StripBrackets 剥离）
- 引号内 `"..."` 和括号内 `()` 的分隔符会被忽略

---

## 五、数据配置原则

### 禁止魔鬼数字
- 所有数字/字符串应配入配置文件（cards/card.ini、bin/AreaPool.ini 等）
- 同一数据只能在一处配置，禁止多处硬编码

### 卡牌层级（`ZIndex`）约定

卡牌与特效的层级数字散在几个文件里，改之前先看这张表——**上界是硬约束**：

| 层 | 数字 | 写在哪 |
|---|---|---|
| 场上的普通卡 | 10 | `battlefield_.cs` 的 `RefreshAllCardDisplayOrder()` |
| **阵亡/弃置动画中的卡** | 11..19（`DiscardZBase`..`DiscardZMax`） | 同上，经 `NextDiscardZIndex()` 取号 |
| **飞掠/空袭抬起的卡** | 12（`FlyingEffect.TopZIndex`） | `core_logic/FlyingEffect.cs` |
| **手牌** | 20 | `RefreshAllCardDisplayOrder()` |
| 悬停的手牌 | 30 | 同上 |
| 选项 UI 里的卡 | 40 | 同上 |
| 正被拖拽的卡 | 100 | `cardNowChoose.ZIndex` |
| 控制台 / 顶层 UI | 1000 | 同上 |

**唯一的不变式：除了拖拽与选项 UI，任何「临时浮起来」的卡都必须低于手牌 20。**
手牌是玩家随时要点的东西，被盖住就没法操作了。

三条踩过的坑：

- `_discardZCounter` 曾经是 `= 50` 且无限自增——弃牌动画整段压在**手牌上面**。
  现在改成 `[DiscardZBase, DiscardZMax]` 区间内取号并**封顶**（宁可几张同层），
  每批弃置前由 `ResetDiscardZCounter()` 重置。
- 飞掠抬起的卡曾经在场景里写死 `TopZIndex = 200`。**场景值优先于 C# 默认值**，
  所以改 C# 默认值不会立刻生效——要连场景里那一行一起改（或删掉让它吃默认值）。
- **改过层级的特效，收尾时必须把 `_displayOrderDirty` 标脏**，光还原自己存的那个值是
  不够的。攻击是从**拖拽释放**发起的（`Attack(cardNowChoose, ...)`），那会儿卡被抬到
  **100**；`FlyingEffect` 存下的 `baseZIndex` 就是 100，播完还原回去等于把它永久留下——
  表现就是「落回桌面仍压着手牌」，而且**不会自己好**（层级只在
  `RefreshAllCardDisplayOrder()` 里被改回去，而那函数只在标脏时才跑）。
  正确做法是 `if (GetParent() is battlefield_ field) field._displayOrderDirty = true;`，
  **让权威去重算，而不是自己猜一个值**。同类：`CardDiscardAndRemove`、`RetreatUnit`。

由 `tests/verify_card_layering.py` 守着：抬起卡与弃置动画的层级都必须在 10 与 20 之间。

### export 的中文说明写在 C# 的 `/// <summary>` 上，不要指望 `.tscn`

`.tscn` 里确实可以写分号注释（**必须单独成行**，写在属性行尾会被当成值的一部分）：

```
; 摆动一个来回要几秒
SwaySecondsPerCycle = 4.0
```

**但它留不住**——已实测：在 Godot 编辑器里打开并保存一次场景，会

1. 把所有 `;` 注释**整段抹掉**；
2. **省略取值恰好等于 C# 默认值的属性**（`flying_effect.tscn` 原有 9 项导出值，保存后
   只剩 `SwayDegrees`、`SwaySecondsPerCycle` 两项——因为只有这两项和代码默认值不同）。

所以「每个 `[Export]` 都要在场景里显式写出值」这个要求本身**做不到**，不要再往上写。
中文说明的唯一可靠落点是 C# 的 `/// <summary>`，那也正是编辑器里悬停能看到的那份：

```csharp
/// <summary>摆动**一个来回**要几秒。</summary>
[Export] public float SwaySecondsPerCycle = 2f;
```

- 场景里写了 `;` 注释当然更好（编辑器打开时能直接看到），但**它随时会消失，别依赖**。
- 由 `tests/verify_attack_effects.py` 守着两件钉得住的事：①每个 `[Export]`（含继承来的）
  上方都有中文 `///` 说明；②场景里写的属性名必须真实存在——Godot 对拼错的属性名是
  **静默忽略**的（`SwayDegree = 5` 不报错、只是改了没反应），只能靠这条挡住。
- 场景注释只作提示打印，**不作失败**：拿一个编辑器保存一次就会消失的东西当断言，
  只会制造没人看的假红灯。

### 配置项位置
| 配置 | 文件 |
|------|------|
| 卡牌属性 | `cards/card.ini` |
| 敌方行动预设 + 关卡开局效果 `battleStart=` | `cards/enemyTurn.ini` |
| 区域池 | `bin/AreaPool.ini` |
| 玩家初始卡组 | `bin/deck.ini` |
| 事件 | `bin/event.ini` |
| 背景音乐槽位 `[music]` 与音效槽位 `[sfx]` | `configs/music.ini` |
| 设置项与音量滑块 | `bin/setting.ini` |

---

## 六、枚举类型位置

所有枚举定义在 `cardBase_.cs` 文件末尾（line 1695-1948）：
- `HQ`、`CardTypes`、`UnitTraits`、`Rarity`、`Stage`、`CardState`、`Times`、`IsFriend`、`ChangeType`、`TargetType`

---

## 七、异步函数规范

- 所有涉及动画/等待的函数必须使用 `async Task`，**禁止**使用 `async void`
- `async void` 异常无法被捕获，会导致静默崩溃
- fire-and-forget 调用格式：`_ = SomeAsyncMethod();`
- 关键流程（如死亡检查 `CheckIfAnyUnitDiedAsync()`）**必须 await**，禁止 fire-and-forget

---

## 八、已知陷阱

| 陷阱 | 说明 |
|------|------|
| `IncrementLifeTime()` 被调用两次 | `OnNextTurnButtonPressed()` 中重复调用，修改回合流程时注意 |
| `PlayCardWithoutCost` 双倍执行 | 有 targetType 参数时卡效果执行两次，修改时需考虑 |
| `AttackInf` 是空桩 | 步兵攻击动画未实现 |
| `GetCardMaganer` 拼写错误 | 多处使用此方法名（少了一个'a'），新增调用时保持一致性 |
| `MouceEntered`/`MouceExited` 拼写错误 | 方法名拼写错误但 Godot 信号连接可能依赖此名称，不要轻易改名 |
| **等一个可能永远不来的事件** | 凡是「按下进入、松开退出」这类成对的事件，收尾都不能指望后一半一定到达。截图工具抢鼠标、Alt-Tab 失焦都会让 mouse-up 送不进来，状态就此挂死（卡永久停在 `caught`）。正确做法是加外部兜底：`_Notification(NotificationApplicationFocusOut)` + 「收到鼠标移动但物理左键已不在按下状态」，且兜底必须放在控制锁判定**之前**。参考 `battlefield_.CancelCurrentDrag()`、回归见 `tests/verify_combat_action_timing.py` |
| 按阵营刷新行动能力 | `RefreshCardsInField(IsFriend side)` 只在**各自回合开头**刷自己那一方（敌方在 `EnemyTurnAsync` 开头、友方在 `FriendlyTurnBegin` 时点之前）。它**不是**「一次性刷全部」——写成那样，敌方回合里被效果刷进场的单位（亡计召唤等）会整回合动不了。`_Ready()` 给的是 `attackAble = 0 / moveAble = 0`，只有闪击会自行补刷 |
| `DiscardRandomly` 的阵营陷阱 | 它弃的是**效果来源卡那一方**的手牌。写在 `enemyTurn.ini` 里的行动来源卡是敌方总部，用它只会去弃敌方自己的（空）手牌，**不报任何错**。要让敌人弃玩家的牌必须用 `DiscardPlayerRandomly(n)` |
| 卡牌自身特效期间不能刷新显示顺序 | 目前只有 `flying`：它把卡的 Position / Scale / ZIndex 全接管了，`cardBase_.isUnderCardEffect` 置位期间 `RefreshAllCardDisplayOrder` 必须跳过它，否则那套「场上卡一律 ZIndex = 10」下一帧就把层级打回去，漂浮就压不住别的卡。该标记和 `shouldBeRemoved`/`isDiscarding` 一样是生命周期状态，已在 `SetCardInformation` 里归零 |
| 牌堆里的卡不在场景树上 | `Player.deck` 里的卡是「已实例化但不在场景树」的对象：改它们的属性合法，但任何走 `GetTree()` 的纯视觉动画都会空引用（`AnimateCostRoll` 已加 `if (!IsInsideTree()) return;`）。给牌堆卡加效果时先对照这条 |
| 输入事件漏校验 `ButtonIndex` | `_Input` 处理 `InputEventMouseButton` 时，「按下」与「抬起」是两个**兄弟分支**，任一分支的 `Pressed` 判断漏掉 `ButtonIndex == MouseButton.Left`，右键就会完整走进左键流程。战斗拖拽中后果尤其严重：`currentInputState` 被冲成 `nil` 后卡牌永久停在 `caught`，而 `RefreshMyHand()` 对拖拽中的卡 `continue` 跳过、不再归位，表现为**卡牌卡在场上**，且全部手牌悬停同时失效。正确写法参考 `Store.cs`、`MulliganScreen.cs`、`ChooseSomeCard.cs`；回归测试见 `tests/verify_drag_right_click.py` |

---

## 九、Git 规范

- 每次需求完成后进行一次 git 提交推送
- 严禁修改 `.gitignore` 中的文件
- 提交前检查 `git status` 和 `git diff`
- 远端为 `origin`（`tertledove39/slay-the-kard`），当前工作分支为 `rebuild`
- `tests/__pycache__/*.pyc` 已被跟踪（11 个），新跑测试产生的 `.pyc` 会以未跟踪状态出现。不要为了把它们一并提交而修改 `.gitignore`
