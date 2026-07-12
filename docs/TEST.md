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
- 箭头每帧不创建曲线/三角形数组，箭身只绘制一个多边形。
- 箭身使用宽Polyline，不存在动态凹多边形三角剖分；近零长度箭头不绘制。
- 拖尾终点向箭头头部内部延伸半个拖尾宽度，避免抗锯齿接缝。
- 卡牌和商店移动会取消旧Tween。
- 商店主题颜色只在状态变化时写入。
- Tooltip使用局部GUI输入且只在图标变化时重算文本尺寸。
