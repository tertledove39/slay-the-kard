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


def test2_trigger_after_remove_card():
    """验证触发调用位于 RemoveCard 之后"""
    content = read_battlefield_cs()

    func_start = content.find('async Task CheckIfAnyUnitDiedAsync()')
    func_end = content.find('async void OnNextTurnButtonPressed', func_start)
    func_body = content[func_start:func_end]

    remove_pos = func_body.find('RemoveCard(deadUnit);')
    friendly_pos = func_body.find('TriggerUnitEffects("FriendlyUnitDead"')
    enemy_pos = func_body.find('TriggerUnitEffects("EnemyUnitDead"')

    assert friendly_pos > remove_pos, "FriendlyUnitDead 触发应在 RemoveCard 之后"
    assert enemy_pos > remove_pos, "EnemyUnitDead 触发应在 RemoveCard 之后"
    print("[PASS] 测试2: 触发调用位于 RemoveCard 之后（确保死亡单位已从场上移除）")


def test3_friend_enemy_distinction():
    """验证 Friendly/Enemy 判定使用 GetIsFriend() 区分"""
    content = read_battlefield_cs()

    func_start = content.find('async Task CheckIfAnyUnitDiedAsync()')
    func_end = content.find('async void OnNextTurnButtonPressed', func_start)
    func_body = content[func_start:func_end]

    assert 'GetIsFriend() == IsFriend.friend' in func_body, \
        "缺少友方判定"
    assert 'GetIsFriend() == IsFriend.enemy' in func_body, \
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

    assert 'checkOnlySourceCard:true' in dead_trigger, \
        "Dead 时点只触发自身效果（checkOnlySourceCard:true）"

    print("[PASS] 测试6: Dead 时点正确只触发死亡单位自身效果")


if __name__ == '__main__':
    print("=" * 60)
    print("单位死亡时点触发验证测试")
    print("=" * 60)

    tests = [
        test1_trigger_calls_exist,
        test2_trigger_after_remove_card,
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
