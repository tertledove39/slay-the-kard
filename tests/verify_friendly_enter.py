#!/usr/bin/env python3
"""
验证 FriendlyUnitEnteringField 时点功能
使用方式: python verify_friendly_enter.py
"""
import re
import os
import sys

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read_file(relative_path):
    path = os.path.join(BASE, relative_path)
    with open(path, 'r', encoding='utf-8') as f:
        return f.read()


def test1_enum_exists():
    """冒烟测试: Times 枚举中包含 friendlyUnitEnteringField"""
    content = read_file('bin/cardBase_.cs')
    func_start = content.find('public enum Times')
    func_end = content.find('public enum Rarity', func_start)
    func_body = content[func_start:func_end]

    assert 'friendlyUnitEnteringField' in func_body, \
        "Times 枚举中缺少 friendlyUnitEnteringField"
    print("[PASS] 测试1: friendlyUnitEnteringField 已加入 Times 枚举")


def test2_addcardtoplace_trigger():
    """冒烟测试: AddCardToPlace 中存在 FriendlyUnitEnteringField 触发"""
    content = read_file('bin/battlefield_.cs')

    func_start = content.find('async Task AddCardToPlace(cardBase_ card, place_ place)')
    func_end = content.find('cardBase_ CheckCardClick', func_start)
    func_body = content[func_start:func_end]

    assert 'FriendlyUnitEnteringField' in func_body, \
        "AddCardToPlace 中缺少 FriendlyUnitEnteringField 触发"
    print("[PASS] 测试2: AddCardToPlace 中存在 FriendlyUnitEnteringField 触发")


def test3_move_trigger():
    """冒烟测试: Move 函数部署分支中存在 FriendlyUnitEnteringField 触发"""
    content = read_file('bin/battlefield_.cs')

    func_start = content.find('async Task Move(cardBase_ card, place_ position)')
    func_end = content.find('async Task EnemyTurnAsync(', func_start)
    func_body = content[func_start:func_end]

    assert 'FriendlyUnitEnteringField' in func_body, \
        "Move 函数中缺少 FriendlyUnitEnteringField 触发"
    print("[PASS] 测试3: Move 部署分支中存在 FriendlyUnitEnteringField 触发")


def test4_excludes_hq():
    """边界测试: 两处触发均检查 isHq != HQ.hq"""
    content = read_file('bin/battlefield_.cs')

    func_start = content.find('async Task AddCardToPlace(cardBase_ card, place_ place)')
    func_end = content.find('cardBase_ CheckCardClick', func_start)
    addcard_body = content[func_start:func_end]

    func_start2 = content.find('async Task Move(cardBase_ card, place_ position)')
    func_end2 = content.find('async Task EnemyTurnAsync(', func_start2)
    move_body = content[func_start2:func_end2]

    assert 'isHq != HQ.hq' in addcard_body, \
        "AddCardToPlace 中未排除 HQ"
    assert 'isHq != HQ.hq' in move_body, \
        "Move 中未排除 HQ"
    print("[PASS] 测试4: 两处触发均排除 HQ 单位")


def test5_friend_check():
    """边界测试: 两处触发均检查 isFriend.friend"""
    content = read_file('bin/battlefield_.cs')

    func_start = content.find('async Task AddCardToPlace(cardBase_ card, place_ place)')
    func_end = content.find('cardBase_ CheckCardClick', func_start)
    addcard_body = content[func_start:func_end]

    func_start2 = content.find('async Task Move(cardBase_ card, place_ position)')
    func_end2 = content.find('async Task EnemyTurnAsync(', func_start2)
    move_body = content[func_start2:func_end2]

    assert "GetIsFriend() == IsFriend.friend" in addcard_body, \
        "AddCardToPlace 中未检查友方阵营"
    assert "GetIsFriend() == IsFriend.friend" in move_body, \
        "Move 中未检查友方阵营"
    print("[PASS] 测试5: 两处触发均检查 isFriend.friend")


def test6_target_is_card():
    """需求验证: target 设为被加入的单位自身"""
    content = read_file('bin/battlefield_.cs')

    func_start = content.find('async Task AddCardToPlace(cardBase_ card, place_ place)')
    func_end = content.find('cardBase_ CheckCardClick', func_start)
    addcard_body = content[func_start:func_end]

    friendly_enter_pos = addcard_body.find('FriendlyUnitEnteringField')
    friendly_enter_section = addcard_body[friendly_enter_pos:friendly_enter_pos + 200]

    assert 'new List<cardBase_> { card }' in friendly_enter_section, \
        "target 应该设为卡片自身"

    func_start2 = content.find('async Task Move(cardBase_ card, place_ position)')
    func_end2 = content.find('async Task EnemyTurnAsync(', func_start2)
    move_body = content[func_start2:func_end2]

    friendly_enter_pos2 = move_body.find('FriendlyUnitEnteringField')
    friendly_enter_section2 = move_body[friendly_enter_pos2:friendly_enter_pos2 + 200]

    assert 'new List<cardBase_> { card }' in friendly_enter_section2, \
        "Move 中 target 应该设为卡片自身"
    print("[PASS] 测试6: target 设置为被加入的单位自身")


def test7_checkonlysourcecard():
    """边界测试: 使用 checkOnlySourceCard:true"""
    content = read_file('bin/battlefield_.cs')

    func_start = content.find('async Task AddCardToPlace(cardBase_ card, place_ place)')
    func_end = content.find('cardBase_ CheckCardClick', func_start)
    addcard_body = content[func_start:func_end]

    friendly_enter_pos = addcard_body.find('FriendlyUnitEnteringField')
    friendly_enter_section = addcard_body[friendly_enter_pos:friendly_enter_pos + 200]

    assert 'checkOnlySourceCard: true' in friendly_enter_section, \
        "应使用 checkOnlySourceCard:true"

    func_start2 = content.find('async Task Move(cardBase_ card, place_ position)')
    func_end2 = content.find('async Task EnemyTurnAsync(', func_start2)
    move_body = content[func_start2:func_end2]

    friendly_enter_pos2 = move_body.find('FriendlyUnitEnteringField')
    friendly_enter_section2 = move_body[friendly_enter_pos2:friendly_enter_pos2 + 200]

    assert 'checkOnlySourceCard: true' in friendly_enter_section2, \
        "Move 中应使用 checkOnlySourceCard:true"
    print("[PASS] 测试7: 使用 checkOnlySourceCard:true")


if __name__ == '__main__':
    print("=" * 60)
    print("FriendlyUnitEnteringField 时点验证测试")
    print("=" * 60)

    tests = [
        test1_enum_exists,
        test2_addcardtoplace_trigger,
        test3_move_trigger,
        test4_excludes_hq,
        test5_friend_check,
        test6_target_is_card,
        test7_checkonlysourcecard,
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
    print(f"Result: {passed} passed, {failed} failed, {len(tests)} total")
    sys.exit(0 if failed == 0 else 1)
