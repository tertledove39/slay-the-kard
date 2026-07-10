# 项目目录说明

## 顶层目录

| 目录/文件 | 说明 |
|-----------|------|
| bin/ | **核心源码目录** — 所有 .cs 文件和 .tscn 场景文件 |
| cards/ | 卡牌图片素材 + card.ini（卡牌配置）+ enemyTurn.ini（敌方行动配置） |
| data_*/ | Godot构建输出数据（.NET运行时DLL等），已配置进.gitignore |
| docs/ | 项目文档目录 |
| tests/ | 测试脚本（Python） |
| assest/ | 其它美术素材（国旗、图标、字体等） |
| memory/ | （空目录，预留） |
| .godot/ | Godot编辑器自动生成目录 |
| project.godot | Godot项目配置文件 |
| 新建游戏项目.sln / .csproj | .NET 解决方案和项目文件 |

## 核心源码文件 (bin/)

| 文件 | 行数 | 说明 |
|------|------|------|
| `battlefield_.cs` | ~5642 | **主战场类** — 最核心文件。包含：战场输入控制、卡牌管理、效果脚本解析执行、攻击/移动系统、敌方AI、回合流程。内含 `Player` 和 `CardMaganer` 内部类。 |
| `cardBase_.cs` | ~1948 | **卡牌单位类** — 卡牌UI节点。管理卡牌属性（攻防费）、特性状态、动画（移动/弃牌/闪烁/悬停）、attribute图标面板。内含所有枚举定义和 `CardData`、`IconCache`、`EffectAttribute` 等辅助类型。 |
| `Cardbase.cs` | ~288 | **箭头渲染器** — `Node2D` 子类，用于绘制从卡牌到鼠标/目标之间的贝塞尔曲线箭头。 |
| `place_.cs` | ~33 | **位置类** — `Node2D` 子类，表示战场上的一个放置格子。管理格子上卡牌的绑定/解绑。 |
| `Player` 类 | 嵌入 battlefield_.cs (line ~4904) | **玩家类** — 管理手牌、卡组、指挥点。包含手牌布局引擎（弧形/悬停/缩放动画）。 |
| `CardMaganer` 类 | 嵌入 battlefield_.cs (line ~5551) | **卡牌数据管理器** — 卡牌数据库，按ID存取 `CardData`，加载总部卡。 |
| `Bullet.cs` | ~44 | **子弹/飞弹动画** — 战斗中卡牌之间飞行的飞弹视觉效果。 |
| `iniHandler.cs` | ~806 | **INI解析器** — 通用的 INI 文件读写库，支持 `IniFile`/`IniSection`/`IniValue`，支持有序节。 |
| `ResourceManager.cs` | ~333 | **资源管理器** — 单例节点。缓存 Texture/Scene/Font，维护空卡池（对象池）。 |
| `SceneLoader.cs` | ~151 | **场景加载器** — 静态类。异步场景切换，支持后台预加载 `PackedScene`，带 Loading 覆盖层。 |
| `BattleStateManager` 类 | CardRestoration.cs (~167) | **跨场景状态管理** — 静态类。持久化卡组ID、选中的敌人、已完成的区域、卡牌数据缓存。 |
| `MeterLabel.cs` | ~210 | **电表数字组件** — 机械式数字滚动显示（指挥点计数用）。 |
| `WorldMap.cs` | ~499 | **世界地图场景** — 战役主界面。10个区域按钮，随递次解锁。点击弹出 ChooseMission。含调试控制台。 |
| `ChooseMission.cs` | ~113 | **任务选择界面** — 在 WorldMap 上叠加，显示3个任务（战斗/事件）。 |
| `EventScene.cs` | ~340 | **事件界面** — 剧情事件叠加层。显示配图+描述+选项，支持 replaceCard/replaceRandomCard 效果。 |
| `PostBattleReward.cs` | ~462 | **战后奖励界面** — 战斗胜利后的奖励系统。3组卡牌选择→卡组替换（稀有度限制）。 |
| `ChooseSomeCard.cs` | ~210 | **统一卡组选卡界面** — 事件、商店、战后奖励共用；最高层CanvasLayer叠加，浅黑遮罩拦截下层输入。 |
| `DisplayCard.cs` | ~128 | **卡组查看器** — 显示玩家卡组，支持滚轮翻页。 |
| `End.cs` | ~70 | **屏幕暗化效果** — 单例 CanvasLayer，用于战场结束/胜利时的画面暗化+国徽显示。 |
| `Area1.cs` | ~9 | **区域按钮桩** — `TextureButton` 扩展，空实现。 |
| `TextureButton1.cs` | ~21 | **选择按钮桩** — `TextureButton` 扩展，三个空信号处理函数。 |
| `oldInput.cs` | ~319 | **废弃的输入处理代码** — 整文件被注释掉，是旧版输入逻辑的存档。 |

## 配置/数据文件

| 文件 | 说明 |
|------|------|
| `cards/card.ini` | 卡牌数据定义（苏联卡 + 德军卡共12张de_前缀卡） |
| `cards/enemyTurn.ini` | 敌方预设回合行动脚本（7个预设：berlin/wehrmacht/luftflotte/ss_panzer/ostwall/volkssturm/fuehrerbunker） |
| `bin/AreaPool.ini` | 区域任务池配置 |
| `bin/deck.ini` | 玩家初始卡组配置 |
| `bin/event.ini` | 事件数据配置 |

## 测试文件

| 文件 | 说明 |
|------|------|
| `tests/verify_enemy_cards.py` | 德军卡牌配置验证（7项测试：卡数量、稀有度、唯一性、无苏联卡引用等） |
| `tests/verify_unit_dead_trigger.py` | 单位死亡时点触发验证（6项测试） |
| `tests/verify_spliteffect_fix.py` | SplitEffectByComma 修复验证（7项测试：括号/引号内逗号不分割） |
| `tests/verify_choose_some_card_overlay.py` | 统一选卡入口、最高层CanvasLayer、浅黑遮罩顺序与精确选卡数量验证 |
| `tests/verify_command_point_meter.py` | 指挥点效果互斥解析、当前点数上限及下回合扣点路径验证 |
