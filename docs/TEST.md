# 测试设计

## 对白系统

测试脚本：`tests/verify_game_dialogue.py`。

### 冒烟测试

- Dialogue Manager与`GameDialogue`均已注册autoload。
- 自定义气泡场景已配置并位于高层CanvasLayer。
- 项目可以通过.NET构建。

### 基本验证

- 同时提供等待式`PlayAsync`和非等待式`Play`。
- 示例对白包含地图、战斗和缺失立绘回退标题。
- happy、angry、normal素材存在并可被场景引用。

### 边界白盒测试

- 开始菜单被唯一排除。
- 并发播放请求串行等待。
- 全局静态事件在节点退出时解除订阅。
- sad素材缺失时回退normal立绘。
- 对白和气泡资源缺失时记录错误并返回失败，不阻塞调用流程。

## 性能优化批次A/B

测试脚本：`tests/verify_performance_batches_ab.py`。

- 死亡检查使用门闩和循环，不存在未等待递归。
- change list、Dead效果、Trait结算和抽牌保持串行等待。
- 回合入口拒绝重复触发并在`finally`恢复状态。
- 箭头绘制已恢复原始实现，专项测试只验证原始函数存在，不再约束批次B箭头优化。
- 卡牌和商店移动会取消旧Tween。
- 商店主题颜色只在状态变化时写入。
- Tooltip使用局部GUI输入且只在图标变化时重算文本尺寸。

## 伏击特性

测试脚本：`tests/verify_ambush.py`。

- 伏击独立于普通反击兵种限制触发。
- 伏击在攻击伤害之前结算；击杀攻击方时跳过攻击伤害。
- 普通反击仅在未触发伏击时执行。
- 冲击消耗在伏击判定之前，绕过伏击且不消耗目标伏击状态。

## 按钮动态效果

测试脚本：`tests/verify_button_animations.py`。

- `worldMap.tscn`中的`deck`按钮连接到`_on_deck_pressed`。
- `WorldMap.cs`不再动态创建卡组按钮。
- WorldMap的`store`与`deck`按钮使用统一hover缩放Tween。
- ChooseMission、Store、EventScene、PostBattleReward、DisplayCard中的按钮使用统一1.08倍/0.12秒hover缩放。 
- 所有按钮hover缩放前都会把`PivotOffset`设为控件中心，避免围绕锚点缩放。

## 全局背景音乐

测试脚本：`tests/verify_music_manager.py`。

- `MusicManager`已注册为autoload。
- `configs/music.ini`包含`start_menu`、`world_map`、`battle`三个槽位。
- `StartMenu`、`WorldMap`和`battlefield_`会在进入场景时请求对应BGM。
- `Store`、`ChooseMission`、`EventScene`和`PostBattleReward`不主动切歌，继承当前音乐。

## 音量设置

测试脚本：`tests/verify_volume_settings.py`。

- 设置配置包含主音量、配乐音量、效果音量和UI音量及可配置范围。
- 设置界面按配置动态创建数值滑块，并将用户值保存到`user://settings.cfg`。
- `Master`、`Music`、`SFX`和`UI` Audio Bus分类存在。
- 背景音乐使用`Music`，当前战斗及卡牌音效使用`SFX`。

## 卡牌视觉效果

测试脚本：`tests/verify_card_effects.py`。

- `CardData`和运行时卡牌支持可选`playEffect`与`attackEffect`。
- 两条卡牌配置加载路径都安全读取可选字段。
- `Effect`提供位置列表和可选时间参数，`EffectRegistry`负责短名称映射。
- 原有子弹攻击表现由`BulletEffect`管理，`Bullet`成为`Effect`子类。
- 出牌、主动攻击、反击和伏击路径调用对应视觉效果。
- 现有非指令卡配置`attackEffect=bullet`，未配置字段的卡牌不播放效果。
- `smoke`效果正确注册并切分4×4图集，单位实际死亡时在移除前快照的中心位置播放。
- 烟雾帧序列与透明度由单个Tween驱动，不再逐帧创建Timer。
- 战斗初始化预加载并预渲染Effect资源，Bullet与Smoke播放后回到战场级对象池。
- Bullet相关随机偏移与发射间隔使用共享随机数生成器。

## 音量、机动防御与奖励界面回归

测试脚本：`tests/verify_reward_and_t70_fixes.py`及`tests/verify_volume_settings.py`。

- 音量系统在应用设置前确保`Music`、`SFX`、`UI` Bus存在，并在重复初始化时重新应用用户音量。
- 机动防御生成的T-70跳过生成当回合的结束时点，在下个友方回合结束时消灭。
- 卡牌奖励界面使用原生尺寸卡牌，三组奖励位于垂直滚动区域，跳过按钮固定可用。

## 友方总部失败流程

测试脚本：`tests/verify_hq_defeat_flow.py`。

- 友方总部被消灭后只启动一次失败流程。
- End界面显示暗幕、灰色国徽、“战斗失败”和“返回主菜单”按钮。
- 玩家点击后直接进入开始菜单，不结算物资、完成区域或显示卡牌奖励。

## Attribute悬浮提示

测试脚本：`tests/verify_attribute_tooltips.py`。

- Attribute图标通过不消费事件的全局鼠标移动检测显示说明，不依赖可能被遮挡的GUI路由。
- 面板重建、指令卡复用和隐藏对象池卡牌会清理旧悬浮状态。

## 商店卡牌显示状态

测试脚本：`tests/verify_store_card_presentation.py`。

- 商店复用同一卡牌节点时，单位、指令和总部显示状态都由当前CardData重新赋值。
- 指令卡刷新为`su76m`等单位卡后会恢复普通卡框、攻防标签和单位UI。

## 敌人意图图标

测试脚本：`tests/verify_enemy_intent_icons.py`。

- 意图面板按每条行动的`icon`元数据加载`normalUnit`、`bigUnit`、`heal`、`damage`或`upgrade`。
- 图标缺失或名称未知时回退到`boss`，不再为所有行动硬编码同一图标。

## 战斗名称配置

测试脚本：`tests/verify_battle_names_in_ini.py`。

- 50个敌人战斗section各自包含`name`，任务界面直接读取该名称。
- `name`不会进入敌人行动队列，缺失时记录错误并回退显示section ID。
- 旧`HistoricalBattleNames.cs`和未使用的`EnemyDisplayNames`字典已删除。

## 攻击与死亡动画时序

测试脚本：`tests/verify_attack_death_timing.py`。

- 主动攻击、反击或伏击中首个实际启动的攻击Effect建立500ms展示窗口。
- 战斗死亡检查在该窗口结束后执行，再移除单位并播放smoke；非战斗死亡不增加延迟。

## 区域解锁进度

测试脚本：`tests/verify_area_unlock_progression.py`。

- `UnlockedArea`由`BattleStateManager`跨场景保存，初始仅`area1=1`。
- 战斗或事件完成后将当前区域设为0、下一区域设为1，区域进度只使用`UnlockedArea`。

## 剧情事件资源点

测试脚本：`tests/verify_event_material_points.py`。

- 冒烟测试：事件配置至少存在一个合法的 `materialPoints(n)` 效果。
- 基本验证：效果写入 `BattleStateManager.MaterialPoints` 并即时刷新世界地图 `pointNum`。
- 边界白盒测试：拒绝负数、非整数和超出 `int` 范围的参数，记录带时间和代码位置的错误；累计结果限制在 `int.MaxValue`。
