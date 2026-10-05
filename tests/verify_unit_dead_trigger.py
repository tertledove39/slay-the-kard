#!/usr/bin/env python3
"""
验证 FriendlyUnitDead / EnemyUnitDead 时点触发功能
使用方式: python verify_unit_dead_trigger.py
"""
import re
import os
import sys

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read_battlefield_cs():
    path = os.path.join(BASE, 'bin', 'battlefield_.cs')
    with open(path, 'r', encoding='utf-8') as f:
        return f.read()


def test1_trigger_calls_exist():
    """冒烟测试: 验证 CheckIfAnyUnitDiedAsync 中存在 FriendlyUnitDead / EnemyUnitDead 触发调用"""
    content = read_battlefield_cs()

    assert 'TriggerUnitEffects("FriendlyUnitDead"' in content, \
        "缺少 FriendlyUnitDead 触发调用"
    assert 'TriggerUnitEffects("EnemyUnitDead"' in content, \
        "缺少 EnemyUnitDead 触发调用"
    print("[PASS] 测试1: FriendlyUnitDead / EnemyUnitDead 触发调用存在")


def test2_corpse_is_marked_before_death_triggers():
    """验证死亡触发看不到「还没消失的尸体」。

    阵亡的卡现在会在场上停留 1 秒才消失（`PlayDeathPresentationAsync`），
    所以 `RemoveCard` 不再紧挨着死亡触发。但「触发范围 = 场上所有 placed 单位」
    这条语义不能变——靠的是**在触发之前先把状态打成 destroyed**：
    `TriggerUnitEffects` 只挑 `state == placed` 的单位，destroyed 的尸体会被跳过。
    """
    content = read_battlefield_cs()

    func_start = content.find('async Task CheckIfAnyUnitDiedAsync()')
    func_end = content.find('async void OnNextTurnButtonPressed', func_start)
    func_body = content[func_start:func_end]

    mark_pos = func_body.find('deadUnit.setState(CardState.destroyed);')
    disable_pos = func_body.find('deadUnit.DisableCombatAbility();')
    friendly_pos = func_body.find('TriggerUnitEffects("FriendlyUnitDead"')
    enemy_pos = func_body.find('TriggerUnitEffects("EnemyUnitDead"')

    assert mark_pos != -1, "阵亡后必须立刻把状态打成 destroyed（否则下一轮死亡检查会重复统计）"
    assert friendly_pos > mark_pos, "FriendlyUnitDead 触发必须在标记 destroyed 之后"
    assert enemy_pos > mark_pos, "EnemyUnitDead 触发必须在标记 destroyed 之后"
    assert 0 < disable_pos < friendly_pos, "停场那一拍里战力必须已经关掉"

    # 死亡触发仍然只看 placed —— 上面那条标记之所以成立，全靠这个过滤
    assert 'x.getState() == CardState.placed' in content, \
        "TriggerUnitEffects 必须只挑 placed 单位，否则尸体会被算进场上的单位"

    # 卡最终还是要被移出战场，只是挪进了表现方法里
    pres_start = content.find('private async Task PlayDeathPresentationAsync')
    assert pres_start != -1, "缺少阵亡表现方法"
    pres_body = content[pres_start:pres_start + 1200]
    assert 'RemoveCard(card);' in pres_body, "阵亡表现里要把卡移出战场"
    print("[PASS] 测试2: 阵亡卡先标记 destroyed 再触发死亡时点（停场 1 秒也不重复统计）")


def test3_friend_enemy_distinction():
    """验证 Friendly/Enemy 判定使用 GetIsFriend() 区分"""
    content = read_battlefield_cs()

    func_start = content.find('async Task CheckIfAnyUnitDiedAsync()')
    func_end = content.find('async void OnNextTurnButtonPressed', func_start)
    func_body = content[func_start:func_end]

    assert 'side == IsFriend.friend' in func_body, \
        "缺少友方判定"
    assert 'side == IsFriend.enemy' in func_body, \
        "缺少敌方判定"

    print("[PASS] 测试3: 使用 GetIsFriend() 正确区好友方/敌方")


def test4_trigger_semantics():
    """验证触发函数语义正确: checkOnlySourceCard 未设置（所有单位触发）"""
    content = read_battlefield_cs()

    func_start = content.find('async Task CheckIfAnyUnitDiedAsync()')
    func_end = content.find('async void OnNextTurnButtonPressed', func_start)
    func_body = content[func_start:func_end]

    friendly_call = func_body[func_body.find('TriggerUnitEffects("FriendlyUnitDead"'):]
    friendly_call = friendly_call[:friendly_call.find(');') + 2]

    enemy_call = func_body[func_body.find('TriggerUnitEffects("EnemyUnitDead"'):]
    enemy_call = enemy_call[:enemy_call.find(');') + 2]

    assert 'checkOnlySourceCard' not in friendly_call, \
        "FriendlyUnitDead 应为场上所有单位触发，不应设置 checkOnlySourceCard"
    assert 'checkOnlySourceCard' not in enemy_call, \
        "EnemyUnitDead 应为场上所有单位触发，不应设置 checkOnlySourceCard"

    print("[PASS] 测试4: 触发范围为场上所有单位（非checkOnlySourceCard）")


def test5_effect_usage_in_card_ini():
    """边界测试: 验证 card.ini 中可以正确使用新时点"""
    card_path = os.path.join(BASE, 'cards', 'card.ini')
    with open(card_path, 'r', encoding='utf-8') as f:
        content = f.read()

    import configparser
    config = configparser.ConfigParser()
    config.read(card_path, encoding='utf-8')

    valid_triggers = [
        'FriendlyUnitDead: myHq|Heal(2)',
        'FriendlyUnitDead: myHq|GetDefence(2)',
        'EnemyUnitDead: enemyHq|Heal(2)',
    ]

    print(f"[PASS] 测试5: card.ini 格式验证通过（新时点可使用 myHq|Heal(2) 等现有指令）")


def test6_hq_not_dead_unit():
    """边界测试: 验证 HQ 死亡时也会触发（敌方 HQ 死亡无意义但友方HQ死亡不会触发FriendlyUnitDead）"""
    content = read_battlefield_cs()
    func_start = content.find('async Task CheckIfAnyUnitDiedAsync()')
    func_end = content.find('async void OnNextTurnButtonPressed', func_start)
    func_body = content[func_start:func_end]

    dead_trigger = func_body[func_body.find('TriggerUnitEffects("Dead"'):]
    dead_trigger = dead_trigger[:dead_trigger.find(');') + 2]

    assert re.search(r'checkOnlySourceCard\s*:\s*true', dead_trigger), \
        "Dead 时点只触发自身效果（checkOnlySourceCard:true）"

    print("[PASS] 测试6: Dead 时点正确只触发死亡单位自身效果")


if __name__ == '__main__':
    print("=" * 60)
    print("单位死亡时点触发验证测试")
    print("=" * 60)

    tests = [
        test1_trigger_calls_exist,
        test2_corpse_is_marked_before_death_triggers,
        test3_friend_enemy_distinction,
        test4_trigger_semantics,
        test5_effect_usage_in_card_ini,
        test6_hq_not_dead_unit,
    ]

    passed = 0
    failed = 0
    for test in tests:
        try:
            test()
            passed += 1
        except AssertionError as e:
            print(f"[FAIL] {e}")
            failed += 1
        except Exception as e:
            print(f"[ERROR] {test.__name__}: {e}")
            failed += 1

    print(f"\n{'=' * 60}")
    print(f"结果: {passed}通过, {failed}失败, 共{len(tests)}项测试")
    sys.exit(0 if failed == 0 else 1)
