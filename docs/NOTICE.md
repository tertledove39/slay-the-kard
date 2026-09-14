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

### 配置项位置
| 配置 | 文件 |
|------|------|
| 卡牌属性 | `cards/card.ini` |
| 敌方行动预设 | `cards/enemyTurn.ini` |
| 区域池 | `bin/AreaPool.ini` |
| 玩家初始卡组 | `bin/deck.ini` |
| 事件 | `bin/event.ini` |

---

## 六、代码架构约束

### 文件行数限制
- 每个代码文件**推荐不超过200行**，**红线300行**
- `battlefield_.cs` 当前已 ~5642 行，**严禁再新增功能到此文件**，应在拆分后再扩展
- `cardBase_.cs` 当前已 ~1948 行，同样接近需要拆分的临界点

### 函数行数限制
- 每个函数**不超过50行**

### 目录结构规范
| 目录 | 用途 |
|------|------|
| `docs/` | 文档 |
| `configs/` | 配置项（yaml/json） |
| `statics/` | 美术/音乐/文本素材 |
| `tests/` | 测试脚本 |
| `datas/` | 运行时可变数据（gitignore） |
| `core_logic/` | 纯逻辑计算代码 |
| `core_ui/` | UI/界面显示代码 |
| `core_data/` | 数据结构/数据存取 |
| `core_util/` | 底层工具函数 |
| `scripts/` | 独立脚本（不能被引用） |

---

## 七、枚举类型位置

所有枚举定义在 `cardBase_.cs` 文件末尾（line 1695-1948）：
- `HQ`、`CardTypes`、`UnitTraits`、`Rarity`、`Stage`、`CardState`、`Times`、`IsFriend`、`ChangeType`、`TargetType`

---

## 八、异步函数规范

- 所有涉及动画/等待的函数必须使用 `async Task`，**禁止**使用 `async void`
- `async void` 异常无法被捕获，会导致静默崩溃
- fire-and-forget 调用格式：`_ = SomeAsyncMethod();`
- 关键流程（如死亡检查 `CheckIfAnyUnitDiedAsync()`）**必须 await**，禁止 fire-and-forget

---

## 九、已知陷阱

| 陷阱 | 说明 |
|------|------|
| `IncrementLifeTime()` 被调用两次 | `OnNextTurnButtonPressed()` 中重复调用，修改回合流程时注意 |
| `PlayCardWithoutCost` 双倍执行 | 有 targetType 参数时卡效果执行两次，修改时需考虑 |
| `AttackInf` 是空桩 | 步兵攻击动画未实现 |
| `GetCardMaganer` 拼写错误 | 多处使用此方法名（少了一个'a'），新增调用时保持一致性 |
| `MouceEntered`/`MouceExited` 拼写错误 | 方法名拼写错误但 Godot 信号连接可能依赖此名称，不要轻易改名 |

---

## 十、Git 规范

- 每次需求完成后进行一次 git 提交推送
- 严禁修改 `.gitignore` 中的文件
- 提交前检查 `git status` 和 `git diff`

---

## 十一、代码风格

- 不允许使用 emoji
- 不在代码中添加注释（除非用户明确要求）
- 遵循现有代码的命名风格（下划线后缀如 `cardBase_`、`place_` 保持已有模式）
