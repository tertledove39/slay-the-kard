# 逻辑功能文档

## 效果脚本变量系统

| 变量名 | 说明 | 代码位置 |
|--------|------|----------|
| `&result` | 当前效果段的result值 | battlefield_.cs ReplaceVariables() |
| `&targetsCount` / `&targets.count` | 目标列表数量 | battlefield_.cs ReplaceVariables() |
| `&sourceAttack` / `&source.attack` | sourceCard的攻击力 | battlefield_.cs ReplaceVariables() |
| `&sourceDefence` / `&source.defence` | sourceCard的防御力 | battlefield_.cs ReplaceVariables() |
| `&sourceCost` / `&source.cost` | sourceCard的费用 | battlefield_.cs ReplaceVariables() |
| `&lifeTime` | 单位存活回合数（优先读取targets[0]，否则sourceCard） | battlefield_.cs ReplaceVariables() + cardBase_.cs ReadLifeTime() |
| `&attackCountThisTurn` | 本回合该卡作为攻击方的战斗次数（从sourceCard读取） | battlefield_.cs ReplaceVariables() + cardBase_.cs ReadAttackCountThisTurn() |
| `&fieldFriendUnitCount` | 友方场上单位数量 | battlefield_.cs ReplaceVariables() |
| `&fieldEnemyUnitCount` | 敌方场上单位数量 | battlefield_.cs ReplaceVariables() |
| `&friendHqDefence` | 友方总部防御力 | battlefield_.cs ReplaceVariables() |
| `&enemyHqDefence` | 敌方总部防御力 | battlefield_.cs ReplaceVariables() |
| `&friendCommandPoint` | 友方当前指挥点 | battlefield_.cs ReplaceVariables() |
| `&friendCommandPointMax` | 友方最大指挥点 | battlefield_.cs ReplaceVariables() |
| `&friendHandCount` | 友方手牌数量 | battlefield_.cs ReplaceVariables() |
| `&friendDeckRemainingCount` | 友方卡组剩余数量 | battlefield_.cs ReplaceVariables() |
| `&任意名称` | 自定义变量（通过SetMemory设置，未定义时默认0） | battlefield_.cs ReadMemory() |

## 攻击次数计数逻辑

- **存储位置**: `cardBase_.attackCountThisTurn` 字段
- **自增时机**: `battlefield_.Attack()` 中，攻击成功后在 `from.HaveAttacked()` 之后调用 `from.IncrementAttackCountThisTurn()`
- **重置时机**: `cardBase_.RefreshUnit()` 中重置为0（每回合开始时通过 `RefreshAllCardInField()` 触发）
- **读取方式**: 效果脚本中使用 `&attackCountThisTurn`，从 `sourceCard` 读取

## 战斗核心流程 (Attack)

### 函数位置
`battlefield_.cs` - `Attack(cardBase_ from, cardBase_ to)` (行1654)

### 执行顺序
1. 控制锁定与死亡检查暂停
2. 检查攻击者是否可以攻击 (`CheckIfCanAttack`)
3. 步兵/坦克攻击范围合法性检查
4. 目标烟幕检查
5. 守护保护检查
6. 触发各种时点效果 (BePicked, Attacking, BeingAttacked等)
7. 计算攻击伤害（考虑重甲、免疫）
8. 应用伤害，移除动员、烟幕
9. 冲击特性处理
10. 反击计算（伏击、重甲、免疫）
11. 播放战斗音效和飞弹动画
12. 标记已攻击 (`HaveAttacked`)
13. 自增本回合攻击计数 (`IncrementAttackCountThisTurn`)
14. trait闪烁
15. 战后移动限制处理
16. 恢复死亡检查，检查单位死亡
17. 解锁控制

## 支援阵线相关方法

| 方法 | 说明 | 代码位置 |
|------|------|----------|
| `GetCardBeingAddToSupportLine` | 效果指令：将上一个加入支援阵线的卡设为 targets，供后续 pipe 指令操作 | battlefield_.cs ParseAndExecuteEffect() |
| `addToSupportLine(cardId)` | 向友方支援阵线添加卡牌，同时更新 lastCardAddedToSupportLine | battlefield_.cs ParseAndExecuteEffect() |
| `addToEnemySupportLine(cardId)` | 向敌方支援阵线添加卡牌，同时更新 lastCardAddedToSupportLine | battlefield_.cs ParseAndExecuteEffect() |

### 使用示例
```
addToSupportLine(t70)|GetCardBeingAddToSupportLine|addDefence(1)
```
向支援阵线添加 t70 后，将 targets 设为该卡，再对其增加 1 点防御力。

### 实现细节
- **存储位置**: `battlefield_.lastCardAddedToSupportLine` 字段
- **赋值时机**: 在 `addToSupportLine`/`addToEnemySupportLine` 指令中，`AddCardToPlace` 成功后赋值
- **指令效果**: 在效果脚本中作为 pipe 指令使用时，将 `targets` 设置为 `{ lastCardAddedToSupportLine }`，若为 null 则不改变 targets

## 效果脚本解析规则

### 分隔符处理
- **逗号**：最高优先级分隔符，分割不同触发时段的效果
- **竖线 `|`**：分割不同指令（pipe）
- 分隔符在以下范围内会被忽略：
  - 引号内 `"..."` 或 `'...'`
  - 圆括号 `()` 内
  - 方括号 `[]` 内（如 `[icon=action,description=含,逗号]`）

### 标签定义
- 以 `&` 结尾的指令为跳转标签定义（如 `Jump&`）
- `End&` 特例：用于标记 foreach 循环结束
- 标签收集时会先剥离 `[icon=...]` 后缀再检测 `&`
- 示例：`Jump&[icon=action,description=跳转]` 会正确识别为标签 `Jump`

### 条件跳转
- 格式：`if(条件)标签`
- 条件满足时跳转到标签位置执行
- 条件不支持跨行

## GetEffect 指令说明

- **功能**: 使 targets 中的单位获得指定的 effect 字符串
- **用法**: `GetEffect("TriggerName:指令[icon=xxx,description=yyy]")`
- **内层变量**: 内层效果字符串中的 `&变量` 保留原始形式，不会在外层被求值。当内层效果稍后触发时，用目标单位自身数据动态求值
- **UI 刷新**: 赋值后自动调用 `RefreshState()` 更新 attribute 图标面板

## 效果图标显示规则

- 每段逗号分隔的效果独立解析其 `[icon=...]` 属性
- trait 属性各自显示独立图标
- 被守护状态也有独立图标
- `BuildAttributePanel` 缓存 attribute 列表，内容未变时跳过重建
