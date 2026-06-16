# Easikard - 卡牌对战游戏

基于 Godot 4.5 (.NET) 的卡牌对战游戏

## 游戏规则
- 游戏中有支援阵线、前线、敌方支援阵线三条线
- 卡牌分为坦克、步兵、飞机、轰炸机、火炮、指令等类型
- 单位具有特性（闪击、奋战、重甲、烟幕、守护、冲击、伏击、免疫）

## 最新修改 (2026-06-16)
- 指挥点数显示改为电表式滚动效果：每个数位独立裁剪窗口+垂直数字滚条+Tween动画，低位先动高位级联
- 新增 MeterLabel 组件（bin/MeterLabel.cs），可复用于任意需要数字滚动动画的场景
- 移除旧的 RollLabelNumber 逐步计数方法，统一使用 MeterLabel.AnimateTo

## 最新修改 (2026-06-15)
- 修复 EvaluateCondition 中 target.name 条件判断使用 id 而非 name 字段的bug（导致正面突击等卡牌效果异常）
- 修复 AddToHand 数量参数逻辑错误（count<1 时错误设为0而非1）
- 改进控制台：setTarget 指令现在可以正确执行
- 实现控制台 Tab 自动补全：按 Tab 键在所有匹配指令之间轮流切换
- 控制台按 ` 键开关

## 效果系统语法
- 指令用 | 分隔，多段效果用逗号分隔
- 支持变量：&result, &targetsCount, &sourceAttack 等
- 支持条件跳转：if(条件)标签
- 支持 foreach/End& 循环结构
- 支持表达式计算（+-*/括号）
- 时间前缀触发：Deployed:, FriendlyTurnBegin:, Dead: 等
