# 单位特性使用指南

## 特性列表

### 1. 闪击 (Blitz)
**效果**：单位部署后可立刻战斗

**使用示例**：
```ini
[突击步兵]
price       = 3
attack      = 3
defense     = 2
cardType    = Infantry
description  = 部署后可立刻攻击敌人
traits      = Blitz
```

**实现细节**：
- 在 `RefreshUnit()` 方法中，具有闪击特性的单位部署后 `attackAble` 被设置为 2
- 这意味着单位可以在部署回合立即攻击，并且在正常回合也能攻击一次

---

### 2. 奋战 (Determination)
**效果**：单位可再战斗1次

**使用示例**：
```ini
[老兵坦克]
price       = 5
attack      = 4
defense     = 4
cardType    = Tank
description  = 可以在同一个回合攻击两次
traits      = Determination
```

**实现细节**：
- 在 `RefreshUnit()` 方法中设置 `extraAttacks = 1`
- 在 `CheckIfCanAttack()` 方法中检查并使用额外攻击次数
- 单位在正常攻击次数用完后，可以使用额外攻击次数

---

### 3. 重甲 (HeavyArmor)
**效果**：单位受到的战斗伤害-1

**使用示例**：
```ini
[重型坦克]
price       = 6
attack      = 3
defense     = 5
cardType    = Tank
description  = 受到的战斗伤害减少1点
traits      = HeavyArmor
```

**实现细节**：
- 在 `Attack()` 方法中，攻击伤害计算时减去1（最小为0）
- 同时应用于攻击伤害和反击伤害

---

### 4. 烟幕 (SmokeScreen)
**效果**：本单位第一次移动/攻击之前不能成为攻击的目标。单位进入前线时失去烟幕

**使用示例**：
```ini
[侦察兵]
price       = 2
attack      = 2
defense     = 1
cardType    = Infantry
description  = 部署后不能被攻击，直到移动或攻击
traits      = SmokeScreen
```

**实现细节**：
- 在 `GetAllowedTargets()` 方法中，具有烟幕的单位不能成为攻击目标
- 在 `Move()` 方法中，单位第一次移动时失去烟幕
- 在 `Move()` 方法中，单位进入前线时失去烟幕
- 在 `Attack()` 方法中，单位第一次攻击时失去烟幕

---

### 5. 守护 (Guardian)
**效果**：在此单位左侧和右侧的目标不能成为攻击的目标。具有守护的单位不能被守护

**使用示例**：
```ini
[护卫坦克]
price       = 4
attack      = 2
defense     = 4
cardType    = Tank
description  = 保护相邻的友方单位
traits      = Guardian
```

**实现细节**：
- 在 `GetAllowedTargets()` 方法中，检查目标是否被守护单位保护
- 通过 `IsTargetProtectedByGuardian()` 方法检查目标左右两侧是否有守护单位
- 具有烟幕的单位，守护不生效
- 具有守护的单位不能被守护

---

### 6. 冲击 (Shock)
**效果**：本单位攻击时，不受到反击（无视伏击）。攻击后失去冲击

**使用示例**：
```ini
[突击队]
price       = 4
attack      = 4
defense     = 2
cardType    = Infantry
description  = 攻击时不受到反击
traits      = Shock
```

**实现细节**：
- 在 `Attack()` 方法中，具有冲击特性的单位攻击时 `willReceiveCounterAttack` 设为 false
- 攻击后自动移除冲击状态

---

### 7. 伏击 (Ambush)
**效果**：被攻击时，此单位先造成反击伤害。若敌方单位因此死亡，则不受到来自对方的伤害

**使用示例**：
```ini
[狙击手]
price       = 3
attack      = 3
defense     = 1
cardType    = Infantry
description  = 被攻击时先造成反击伤害
traits      = Ambush
```

**实现细节**：
- 在 `Attack()` 方法中，具有伏击特性的单位先造成反击伤害
- 如果攻击方因此死亡，则不受到来自对方的伤害
- 伏击伤害也会受到重甲和免疫特性的影响

---

### 8. 免疫 (Immunity)
**效果**：此单位不受到战斗伤害

**使用示例**：
```ini
[隐形战机]
price       = 5
attack      = 3
defense     = 2
cardType    = Plane
description  = 不受到战斗伤害
traits      = Immunity
```

**实现细节**：
- 在 `Attack()` 方法中，攻击伤害设为0
- 同时应用于攻击伤害和反击伤害

---

## 特性组合示例

### 多特性组合
```ini
[精英重装步兵]
price       = 5
attack      = 3
defense     = 4
cardType    = Infantry
description  = 部署后可攻击，受到伤害减少，可攻击两次
traits      = Blitz | HeavyArmor | Determination
```

这个单位具有：
- **闪击**：部署后可立刻攻击
- **重甲**：受到的战斗伤害-1
- **奋战**：可以攻击两次

### 守护与烟幕组合
```ini
[隐形护卫]
price       = 4
attack      = 2
defense     = 3
cardType    = Tank
description  = 部署后不能被攻击，保护相邻单位
traits      = Guardian | SmokeScreen
```

注意：具有烟幕的单位，守护不生效，所以这个组合的实际效果是：
- 部署后不能被攻击（烟幕）
- 第一次移动或攻击后，烟幕消失，守护特性生效

---

## 特性优先级

1. **烟幕 > 守护**：具有烟幕的单位不能被守护
2. **冲击 > 伏击**：具有冲击特性的单位攻击时，无视伏击
3. **免疫 > 重甲**：免疫特性完全不受战斗伤害，重甲无效

---

## 注意事项

1. **特性在 card.ini 中的设置**：
   - 使用 `traits` 字段
   - 多个特性使用 `|` 分隔
   - 特性名称区分大小写

2. **特性初始化**：
   - 在 `SetCardInformation()` 方法中初始化
   - 烟幕和冲击状态在部署时初始化

3. **特性状态管理**：
   - 烟幕在第一次移动、攻击或进入前线时移除
   - 冲击在攻击后移除
   - 奋战的额外攻击次数在使用后减少

4. **战斗计算顺序**：
   - 计算攻击伤害（考虑重甲和免疫）
   - 应用伤害
   - 检查伏击
   - 计算反击伤害（考虑重甲和免疫）
   - 应用反击伤害
   - 检查单位死亡

---

## 代码实现位置

### cardBase_.cs
- 特性枚举定义（UnitTraits）
- 特性字段和状态跟踪变量
- 特性管理方法（HasTrait、AddTrait、RemoveTrait等）
- RefreshUnit 方法（闪击和奋战）
- CheckIfCanAttack 方法（奋战）

### battlefield_.cs
- GetAllowedTargets 方法（烟幕和守护）
- IsTargetProtectedByGuardian 方法（守护检查）
- GetLeftPlace/GetRightPlace 方法（守护位置查找）
- Attack 方法（重甲、免疫、冲击、伏击）
- Move 方法（烟幕移除）

---

## 测试建议

1. **闪击测试**：
   - 部署具有闪击特性的单位
   - 验证是否可以立即攻击

2. **奋战测试**：
   - 部署具有奋战特性的单位
   - 验证是否可以攻击两次

3. **重甲测试**：
   - 攻击具有重甲特性的单位
   - 验证伤害是否减少1点

4. **烟幕测试**：
   - 部署具有烟幕特性的单位
   - 验证是否不能被攻击
   - 移动或攻击后验证烟幕是否消失

5. **守护测试**：
   - 部署守护单位和被保护单位
   - 验证被保护单位是否不能被攻击

6. **冲击测试**：
   - 使用具有冲击特性的单位攻击
   - 验证是否不受到反击

7. **伏击测试**：
   - 攻击具有伏击特性的单位
   - 验证是否先受到反击伤害
   - 验证死亡后是否不受到攻击伤害

8. **免疫测试**：
   - 攻击具有免疫特性的单位
   - 验证是否不受到伤害
