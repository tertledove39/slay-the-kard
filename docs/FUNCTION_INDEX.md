# 函数索引目录

> 按文件列出所有函数及其行数

## battlefield_.cs (bin/battlefield_.cs, ~5642 行)

### 控制与状态

| 函数 | 行数 | 说明 |
|------|------|------|
| `AllowControl()` | 31 | 允许玩家输入 |
| `ForbidControl()` | 40 | 禁止玩家输入 |
| `ReadControlState()` | 49 | 读取输入控制状态 |
| `PauseDeathCheck()` | 57 | 暂停死亡检查 |
| `ResumeDeathCheck()` | 65 | 恢复死亡检查 |
| `ReadDeathCheckState()` | 73 | 读取死亡检查状态 |

### 卡牌管理

| 函数 | 行数 | 说明 |
|------|------|------|
| `ReadCardInPlaces()` | 97 | 读取场上卡牌列表 |
| `SetCardInPlaces(list)` | 106 | 设置场上卡牌列表 |
| `SortCardList(list)` | 176 | 按Y再X排序卡列表 |
| `RefreshAllCardDisplayOrder()` | 195 | 刷新卡牌显示层级ZIndex |
| `CheckIfThePlaceIsOccupied(place)` | 253 | 检查格子是否被占据 |
| `AddCardToPlace(card, place)` | 271 | 将卡加入指定格子 |
| `CheckCardClick(pos)` | 307 | 检测鼠标点击的卡牌 |
| `AddToBattleField(card)` | 327 | 将卡加入战场全局 |
| `GetPlaceWithPosition(pos)` | 337 | 获取指定坐标的格子 |
| `GetPlaceById(id)` | 149 | 按ID获取格子引用 |
| `RemoveCard(card)` | 3057 | 从战场移除卡牌 |
| `CardDiscardAndRemove(card)` | 3121 | 弃牌动画+移除 |
| `GetTheFirstValidFriendlyPlace()` | 4725 | 获取第一个空的友方支援格 |
| `GetTheFirstValidEnemyPlace()` | 2821 | 获取第一个空的敌方支援格 |
| `GetTheFirstValidEnemySupportPlace()` | 4740 | 同GetTheFirstValidEnemyPlace |

### 阵线与位置

| 函数 | 行数 | 说明 |
|------|------|------|
| `CheckIfFrontLineIsFriend()` | 2138 | 前线是否被友方控制 |
| `CanCardMoveFromPlace(card)` | 2154 | 检查卡牌是否可从当前位置移动 |
| `GetLeftPlace(place)` | 2628 | 获取指定位置左侧位置 |
| `GetRightPlace(place)` | 2650 | 获取指定位置右侧位置 |
| `RefreshAllBeGuardianedStatus()` | 3010 | 刷新所有单位的被守护状态 |
| `IsUnitProtectedByGuardian(unit)` | 3023 | 检查单位是否被守护 |

### 效果系统

| 函数 | 行数 | 说明 |
|------|------|------|
| `ReplaceVariables(str, result, targets, source)` | 1397 | &变量替换 |
| `ReadMemory(name)` | 1533 | 读取内存变量 |
| `SetMemory(name, value)` | 1546 | 设置内存变量 |
| `EvaluateExpression(expr, ...)` | 1694 | 计算表达式（+-*/） |
| `EvaluateCondition(cond, ...)` | 4764 | 条件判断（if指令用） |
| `GetTargetsFromSelector(selector)` | 4653 | 点号选择器解析 |
| `SplitEffectString(str, delimiter)` | 1620 | 智能分割效果字符串 |
| `StripBracketsOutsideQuotes(s)` | 1577 | 去除引号外的[...]元数据 |
| `ParseAndExecuteEffect(effect, source, targets, target)` | 3290 | **效果脚本解释器** |

### 时点触发

| 函数 | 行数 | 说明 |
|------|------|------|
| `TriggerUnitEffects(triggerPoint, source, targets, checkOnly)` | 1728 | 触发时点效果 |
| `TriggerSharedHatred(targetedUnit)` | 3092 | 触发同仇特性 |

### 战斗与移动

| 函数 | 行数 | 说明 |
|------|------|------|
| `Attack(from, to)` | 1788 | **完整战斗流程** |
| `Move(card, position)` | 2027 | 单位移动/部署 |
| `RetreatUnit(unit)` | 4475 | 单位撤退 |
| `IsTargetProtectedByGuardian(target, attacker)` | 2564 | 守护保护检查 |
| `GetAllowedTargets(attacker)` | 2450 | 获取合法攻击目标 |
| `CanDestroyTarget(attacker, target)` | 2441 | 判断是否能一击摧毁 |
| `FlyBullets(from, to)` | 765 | 飞弹视觉动画 |
| `PlayBattleSound(id)` | 822 | 战斗音效 |
| `PlayDeadSound(id)` | 834 | 死亡音效 |

### 目标高亮

| 函数 | 行数 | 说明 |
|------|------|------|
| `HighlightValidTargets(targetType)` | 3170 | 高亮合法目标 |
| `HighlightValidAttackTargets(attacker)` | 3132 | 高亮合法攻击目标 |
| `RestoreAllTargetsColor()` | 3239 | 恢复所有卡牌颜色 |
| `GetHowManyCardIsValid(targetType)` | 3190 | 计数合法目标 |
| `IsValidTarget(card, targetType)` | 3209 | 检查是否是合法目标 |

### 敌方系统

| 函数 | 行数 | 说明 |
|------|------|------|
| `EnemyInit()` | 2172 | 初始化敌方卡组（30张随机） |
| `EnemyTurnAsync()` | 2195 | 敌方回合主流程 |
| `ExecuteEnemyActionQueue()` | 2324 | 执行敌方行动队列 |
| `ExecuteEnemyAction(action)` | 2390 | 执行单条敌方行动 |
| `LoadEnemyActionQueue(enemyHqName)` | 2240 | 从enemyTurn.ini加载行动 |
| `EnemyExecuteEffect(effect, source, targets)` | 2219 | 敌方执行效果 |
| `EnemyPerformActionsAsync()` | 2673 | 敌方AI行动（推进+攻击） |
| `enemySummonAsync()` | 2794 | 敌方召唤单位 |
| `enemySummonAsync(id)` | 2806 | 敌方召唤指定ID单位 |

### 死忘检查

| 函数 | 行数 | 说明 |
|------|------|------|
| `CheckIfAnyUnitDiedAsync()` | 2840 | 检查并处理死亡单位，触发 `Dead`/`FriendlyUnitDead`/`EnemyUnitDead` 时点 |
| `ExecuteChangeLists()` | 4710 | 执行所有卡牌的缓冲变更 |

### 回合流程

| 函数 | 行数 | 说明 |
|------|------|------|
| `OnNextTurnButtonPressed()` | 2902 | **下一回合按钮**（完整回合流程） |
| `RefreshAllCardInField()` | 2945 | 刷新所有场上卡牌 |
| `ApplyTurnStartTraits()` | 2956 | 应用友方回合开始trait |
| `ApplyEnemyTurnStartTraits()` | 2990 | 应用敌方回合开始trait |

### 选择界面

| 函数 | 行数 | 说明 |
|------|------|------|
| `CreateChoiceOverlay()` | 570 | 创建选择界面覆盖层 |
| `ShowCardChoice(cards, animateExit)` | 591 | 显示卡牌选择界面 |
| `HandleChoiceCardClick(pos)` | 708 | 处理选择界面点击 |

### 控制台

| 函数 | 行数 | 说明 |
|------|------|------|
| `ToggleConsole()` | 938 | 开关控制台 |
| `CreateConsole()` | 946 | 创建控制台面板 |
| `HandleConsoleAutocomplete()` | 1004 | Tab自动补全 |
| `OnConsoleSubmit(text)` | 983 | 控制台提交指令 |
| `ConsolePrint(text)` | 977 | 控制台输出 |

### 工具函数

| 函数 | 行数 | 说明 |
|------|------|------|
| `GetRarity(str)` | 801 | 字符串转Rarity枚举 |
| `GetTypes(str)` | 847 | 字符串转CardTypes枚举 |
| `GetTargetType(str)` | 753 | 字符串转TargetType枚举 |
| `GetTraitList(str)` | 727 | 字符串转UnitTraits位标记 |
| `GetUnitTraits(input)` | 4623 | 解析特性字符串 |
| `GetFieldUnitCount(side, type?)` | 1516 | 获取场上某方单位数 |
| `GetHqDefence(side)` | 1527 | 获取总部防御 |
| `ParseCardTypeFromName(name)` | 1554 | 类型名→CardTypes |
| `GetCardBeingAddToSupportLine()` | 164 | 获取上一个加入支援阵线的卡 |
| `DarkenScreen()` | 2432 | 画面暗化（胜利效果） |
| `ReturnToWorldMapAfterVictory()` | 3076 | 战役胜利后返回世界地图 |
| `ExecuteCommandAndDiscard(card, targets, restoreColor)` | 4524 | 执行指令卡并弃牌 |
| `PlayCardWithoutCost(card, targetType)` | 4560 | 免费打出卡牌 |

### 初始化和设置

| 函数 | 行数 | 说明 |
|------|------|------|
| `_Ready()` | 381 | 场景初始化 |
| `_Input(event)` | 1058 | 全局输入处理 |
| `_Process(delta)` | 1369 | 每帧刷新 |
| `SetupMeterLabels()` | 524 | 创建电表数字组件 |
| `CreateDeckViewButton()` | 505 | 创建卡组查看按钮 |

### 调试

| 函数 | 行数 | 说明 |
|------|------|------|
| `TestButtonPressed()` | 3148 | 测试按钮1 |
| `OnButtonTest2Pressed()` | 3154 | 测试按钮2 |

---

## cardBase_.cs (bin/cardBase_.cs, ~1948 行)

### 生命周期

| 函数 | 行数 | 说明 |
|------|------|------|
| `_Ready()` | 670 | 初始化 |
| `_DeferredFontSizeAdjust()` | 746 | 延迟字号自适应(布局完成后补调) |
| `_Input(event)` | 1463 | 鼠标悬停检测(tooltip) |
| `Dead()` | 1493 | 死亡/回收 |
| `SetCardInformation(data)` | 849 | 设置卡牌数据 |

### 状态

| 函数 | 行数 | 说明 |
|------|------|------|
| `getState()` | 1517 | 读取卡牌状态 |
| `setState(state)` | 1526 | 设置卡牌状态 |
| `RefreshState()` | 1087 | 刷新所有UI显示 |
| `RefreshUnit()` | 96 | 重置行动次数 |
| `DisableCombatAbility()` | 117 | 禁用战斗能力 |

### 行动相关

| 函数 | 行数 | 说明 |
|------|------|------|
| `CheckIfCanMove()` | 386 | 检查可否移动 |
| `HaveMoved()` | 394 | 标记已移动 |
| `CheckIfCanAttack()` | 405 | 检查可否攻击 |
| `HaveAttacked()` | 424 | 标记已攻击 |
| `ReadAttackable()` | 430 | 读取可攻击次数 |
| `CanMoveFromCurrentPlace()` | 159 | 检查实际可移动性 |
| `UpdateMoveableLight()` | 129 | 更新移动/攻击指示灯 |
| `IncrementAttackCountThisTurn()` | 376 | 攻击计数+1 |
| `ReadAttackCountThisTurn()` | 368 | 读取攻击计数 |
| `IncrementLifeTime()` | 360 | 存活回合+1 |
| `ReadLifeTime()` | 352 | 读取存活回合 |

### 属性修改

| 函数 | 行数 | 说明 |
|------|------|------|
| `ReadAttack()` | 601 | 读取攻击力 |
| `GetAttack(n)` | 556 | 增加攻击力 |
| `LoseAttack(n)` | 570 | 减少攻击力 |
| `ReadDefence()` | 583 | 读取防御力 |
| `GetDefence(n)` | 516 | 增加防御力 |
| `LoseDefence(n)` | 544 | 减少防御力 |
| `SetDefence(n)` | 530 | 设置防御力 |
| `ReadCost()` | 610 | 读取费用 |
| `AddCost(n)` | 618 | 增加费用 |
| `ReduceCost(n)` | 632 | 减少费用 |
| `AnimateCostRoll(from, to)` | 647 | 费用数字滚动动画 |
| `ReadMaxHistoryDefence()` | 592 | 读取历史最大防御 |
| `FlashAttributeWithColor(attr, val, init, extreme, invert)` | 1608 | 属性闪烁动画 |

### 特性管理

| 函数 | 行数 | 说明 |
|------|------|------|
| `HasTrait(trait)` | 168 | 检查是否有指定特性 |
| `AddTrait(trait)` | 176 | 添加特性 |
| `RemoveTrait(trait)` | 194 | 移除特性 |
| `HasSmokeScreenActive()` | 221 | 烟幕是否激活 |
| `RemoveSmokeScreen()` | 229 | 移除烟幕 |
| `HasShockActive()` | 239 | 冲击是否激活 |
| `RemoveShock()` | 247 | 移除冲击 |
| `HasMobilizeActive()` | 257 | 动员是否激活 |
| `RemoveMobilize()` | 265 | 移除动员 |
| `HasAmbushActive()` | 275 | 伏击是否可用 |
| `UseAmbush()` | 283 | 使用伏击 |
| `RestoreAmbush()` | 293 | 恢复伏击 |
| `SetBeGuardianed(val)` | 331 | 设置被守护状态 |
| `IsBeGuardianed()` | 343 | 是否被守护 |
| `FlashTraitIcon(name)` | 309 | trait图标闪烁 |

### 阵营与位置

| 函数 | 行数 | 说明 |
|------|------|------|
| `SetIsFriend(isFriend)` | 440 | 设置阵营 |
| `GetIsFriend()` | 461 | 读取阵营 |
| `GetMyPlace()` | 1430 | 读取所在格子 |
| `SetMyPlace(place)` | 1439 | 设置所在格子 |
| `ClearMyPlace()` | 1449 | 清空格子绑定 |

### 视觉

| 函数 | 行数 | 说明 |
|------|------|------|
| `SetHover(hover, scale, rotation)` | 793 | 悬停高亮 |
| `ResetVisualsInstant()` | 829 | 即时重置视觉 |
| `SetHqImage(num)` | 737 | 设置总部图片 |
| `OnRefreshUnitType(x)` | 1396 | 刷新单位类型图标 |
| `SetGrayscale()` | 1414 | 设为灰度 |
| `RestoreColor()` | 1424 | 恢复颜色 |
| `MouceEntered()` | 1675 | 鼠标进入（拼写错误） |
| `MouceExited()` | 1683 | 鼠标离开（拼写错误） |

### 动画

| 函数 | 行数 | 说明 |
|------|------|------|
| `MoveToPosition(dest, duration)` | 1545 | 移动到目标位置 |
| `DiscardCard()` | 1560 | 弃牌动画 |
| `AttackInf(target)` | 1688 | 步兵攻击动画（空桩） |

### 描述与图标

| 函数 | 行数 | 说明 |
|------|------|------|
| `BuildTraitPrefix()` | 890 | 构建trait描述前缀 |
| `ParseEffectAttribute(str)` | 925 | 解析[icon=...]属性 |
| `SplitEffectByComma(str)` | 965 | 按逗号分割效果（跳过[]内） |
| `GetAllAttributes()` | 988 | 收集所有attribute |
| `GetTraitIconTint(trait)` | 1042 | trait图标颜色 |
| `GetTraitDescription(trait)` | 1072 | trait中文描述 |
| `RefreshDescriptionText()` | 209 | 刷新描述文本 |
| `BuildAttributePanel()` | 1152 | 构建/刷新attribute图标面板 |

### 字体

| 函数 | 行数 | 说明 |
|------|------|------|
| `AdjustFontSizeToFit(label)` | 1332 | CJK字符计数估算调整Label字号 |
| `FindBestFontSizeForLabel(...)` | 1378 | Label字号二分搜索(已弃用,保留备用) |
| `AdjustRichTextFontSizeToFit(label)` | 1423 | 二分调整RichTextLabel字号 |
| `FindBestFontSizeForRichText(...)` | 1440 | RichTextLabel字号二分搜索 |

### 变更缓冲

| 函数 | 行数 | 说明 |
|------|------|------|
| `AddChange(type, value)` | 467 | 添加变更 |
| `ExecChangeList()` | 475 | 执行缓冲变更 |
| `AddPendingDiscard()` | 507 | 挂起弃置操作 |

---

## Cardbase.cs (bin/Cardbase.cs, ~288 行)

| 函数 | 行数 | 说明 |
|------|------|------|
| `_Ready()` | 23 | 初始化Marker2D |
| `_Process(delta)` | 249 | 每帧更新箭头的起终点和控制点 |
| `_Draw()` | 30 | 绘制贝塞尔曲线箭头 |
| `DrawSimpleCurvesFill(curve1, curve2, color)` | 74 | 三角形带填充 |
| `BezierCurve(p1, p2, ctl1, ctl2, count)` | 266 | 贝塞尔曲线点生成 |
| `DrawBezierCurve(p1, p2, ctl1, ctl2, count)` | 277 | 绘制单条贝塞尔曲线 |
| `PVector2(angle, length)` | 260 | 极坐标转Vector2 |
| `IsValidTriangle(triangle)` | 112 | 验证三角形非退化 |
| `IsValidPolygon(polygon)` | 133 | 验证多边形有效 |
| `IsPolygonSelfIntersecting(polygon)` | 197 | 检测多边形自交 |
| `DoLinesIntersect(a1,a2,b1,b2)` | 224 | 线段相交检测 |
| `Cross(a, b)` | 244 | 二维向量叉积 |
| `GetTriangleArea(p1, p2, p3)` | 153 | 三角形面积 |
| `GetPolygonArea(polygon)` | 159 | 多边形面积 |

---

## Player 类 (battlefield_.cs ~line 4904-5545)

| 函数 | 行数 | 说明 |
|------|------|------|
| `ReadPoint()` | 4932 | 读取当前指挥点 |
| `ReadPointMax()` | 4937 | 读取最大指挥点 |
| `AddPoint(i)` | 4942 | 增加指挥点 |
| `AddPointMax(i)` | 4950 | 增加最大指挥点 |
| `UsePoint(x)` | 4958 | 消耗指挥点 |
| `HasPoint(x)` | 4977 | 检查是否有足够指挥点 |
| `RestorePoint(x)` | 4985 | 返还指挥点 |
| `RefreshPoint()` | 4993 | 重置指挥点=最大 |
| `AddPointMaxNatural()` | 5001 | 自然增长+1最大点 |
| `GetCardsInHand()` | 5013 | 获取手牌列表 |
| `ReadDeckCount()` | 5018 | 读取卡组数量 |
| `GetHoveredHandIndex()` | 5026 | 读取悬停手牌索引 |
| `AddCardToHand(card)` | 5035 | 添加卡牌实例到手牌 |
| `AddCardToHand(CardData)` | 5061 | 从数据创建卡牌并加入手牌 |
| `RemoveFromHand(card)` | 5100 | 从手牌移除 |
| `RefreshMyHand()` | 5221 | 手牌布局引擎（弧形/悬停/缩放） |
| `UpdateHover(pos)` | 5296 | 更新悬停状态 |
| `DrawCard()` | 5324 | 抽1张牌 |
| `DrawCard(n)` | 5348 | 抽n张牌 |
| `AddCardToDeck(id)` | 5360 | 添加卡牌到卡组 |
| `GetLastDrawnCards()` | 5381 | 获取最近抽到的卡 |
| `SetLastDrawnCards(c)` | 5386 | 设置最近抽到的卡 |
| `DrawCardsWithName(pattern, count)` | 5395 | 按名称抽卡 |
| `DrawCardsWithType(type, count)` | 5422 | 按类型抽卡 |
| `DrawUnitCards(count)` | 5454 | 抽单位卡 |
| `DiscardRandomly(count)` | 5483 | 随机弃手牌 |
| `DiscardCardsWithName(pattern, count)` | 5503 | 按名称弃手牌 |
| `ShuffleDeck()` | 5519 | Fisher-Yates洗牌 |
| `ReadMyDeck()` | 5538 | 读取卡组列表 |
| `InitializeDeckFromIni()` | 5133 | 从INI初始化卡组 |

---

## CardMaganer 类 (battlefield_.cs ~line 5551-5642)

| 函数 | 行数 | 说明 |
|------|------|------|
| `GetRandomCard()` | 5561 | 随机获取卡牌数据 |
| `SetCardDictionary(items)` | 5573 | 设置卡牌字典 |
| `GetCard(id)` | 5585 | 按ID获取卡牌数据 |
| `GetAllCards()` | 5600 | 获取所有卡牌数据 |
| `LoadHq(isFriend)` | 5609 | 加载总部卡 |
| `GetCardTemplate()` | 5632 | 获取卡牌模板 |
