# 配置项文档

## 卡牌配置

### cards/card.ini
定义所有卡牌的数据。每个节是一个卡牌。

**卡牌字段说明：**
| 字段 | 说明 | 示例 |
|------|------|------|
| price | 费用 | 1 |
| attack | 攻击力 | 2 |
| defense | 防御力 | 2 |
| icon | 卡牌图片路径 | res://cards/德国步兵.png |
| name | 卡牌名称 | 德国步兵 |
| rarity | 稀有度：Common/Rare/Epic/Legendary/Unobtainable | Unobtainable |
| cardType | 类型：Infantry/Tank/Plane/Bomber/Artillery/Command | Infantry |
| description | 效果描述文字 | 德国国防军标准步兵 |
| effect | 效果脚本 | Deployed:... |
| isHq | 是否总部：0=普通卡,1=总部 | 1 |
| targetType | 目标类型 | NOTarget |
| traits | 特性（逗号分隔） | HeavyArmor,Guardian |

### cards/enemyTurn.ini
定义敌方关卡预设的回合行动脚本。

**行动格式：**
- `tN=行动` - 第N回合执行的行动
- `everyNt=行动` - 每N回合执行的行动
- `ADD:行动` - 添加行动到永久队列（每回合执行）
- `default=行动` - 无特定行动时的默认行动

### bin/AreaPool.ini
定义区域任务池。每个区域包含敌人预设和事件条目。

### bin/deck.ini
定义玩家的初始卡组。

## 德军敌人卡牌清单（v2.0）

| ID | 名称 | 费用 | 攻/防 | 类型 | 特性 |
|----|------|------|--------|------|------|
| de_infantry | 德国步兵 | 1 | 2/2 | Infantry | - |
| de_mg42 | MG42机枪组 | 2 | 3/1 | Infantry | Ambush |
| de_panzer4 | 四号坦克 | 3 | 3/3 | Tank | Blitz |
| de_panther | 黑豹坦克 | 4 | 4/4 | Tank | Determination |
| de_tiger | 虎式重坦 | 6 | 6/6 | Tank | HeavyArmor |
| de_stuka | 斯图卡 | 2 | 2/1 | Plane | Shock |
| de_88mm | 88毫米炮 | 3 | 4/2 | Artillery | HeavyArmor,Guardian |
| de_sturmpionier | 突击工兵 | 2 | 3/2 | Infantry | Blitz,Shock |
| de_fallschirmjager | 伞兵 | 2 | 2/2 | Infantry | SmokeScreen |
| de_ss_guard | 党卫军卫队 | 4 | 4/3 | Infantry | Guardian,Determination |
| de_bunker | 混凝土碉堡 | 5 | 2/6 | Artillery | HeavyArmor,Guardian |
| de_volksgrenadier | 国民掷弹兵 | 1 | 1/1 | Infantry | Mobilize |

所有德军敌人卡牌均使用 `res://cards/德国步兵.png` 作为图标，rarity=Unobtainable（不可获得）。

## 敌人预设主题

| 预设名 | 主题 | 难度定位 |
|--------|------|---------|
| wehrmacht | 步兵师 | 早期关卡 |
| luftflotte | 空军联队 | 中期关卡 |
| ss_panzer | 装甲师 | 中后期关卡 |
| ostwall | 防御阵地 | 后期关卡 |
| volkssturm | 人海冲锋 | 中后期关卡 |
| fuehrerbunker | 精锐卫队 | 后期关卡 |
| berlin | 最终防线 | 终局关卡 |
