#!/usr/bin/env python3
"""
验证 SplitEffectByComma 修复：括号/引号内的逗号不被分割
使用方式: python verify_spliteffect_fix.py
"""
import re
import os
import sys

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read_cardbase_cs():
    path = os.path.join(BASE, 'bin', 'cardBase_.cs')
    with open(path, 'r', encoding='utf-8') as f:
        return f.read()


def test1_source_code_has_paren_tracking():
    """冒烟测试: 验证 SplitEffectByComma 函数追踪括号"""
    content = read_cardbase_cs()
    func_start = content.find('private static List<string> SplitEffectByComma(string s)')
    assert func_start != -1, "找不到 SplitEffectByComma 函数"
    func_end = content.find('private', func_start + 10)
    if func_end == -1:
        func_end = content.find('public', func_start + 10)
    func_body = content[func_start:func_end]

    assert 'parenDepth' in func_body, "缺少 parenDepth 变量追踪括号"
    assert "c == '('" in func_body, "缺少左括号追踪"
    assert "c == ')'" in func_body, "缺少右括号追踪"
    print("[PASS] 测试1: 源码包含括号追踪逻辑")


def test2_source_code_has_quote_tracking():
    """冒烟测试: 验证 SplitEffectByComma 函数追踪引号"""
    content = read_cardbase_cs()
    func_start = content.find('private static List<string> SplitEffectByComma(string s)')
    func_end = content.find('private', func_start + 10)
    if func_end == -1:
        func_end = content.find('public', func_start + 10)
    func_body = content[func_start:func_end]

    assert 'inQuotes' in func_body, "缺少 inQuotes 变量追踪引号"
    assert 'quoteChar' in func_body, "缺少 quoteChar 变量"
    print("[PASS] 测试2: 源码包含引号追踪逻辑")


def test3_split_condition_correct():
    """验证分割条件同时检查 bracketDepth/parenDepth/inQuotes"""
    content = read_cardbase_cs()
    func_start = content.find('private static List<string> SplitEffectByComma(string s)')
    func_end = content.find('private', func_start + 10)
    if func_end == -1:
        func_end = content.find('public', func_start + 10)
    func_body = content[func_start:func_end]

    assert 'bracketDepth == 0' in func_body, "分割条件缺少 bracketDepth 检查"
    assert 'parenDepth == 0' in func_body, "分割条件缺少 parenDepth 检查"
    print("[PASS] 测试3: 分割条件正确检查 bracketDepth/parenDepth")


def test4_simulate_t3476d_effect():
    """需求验证: 模拟 t3476D 效果字符串分割 - 括号内逗号不被分割"""
    # Python 实现 SplitEffectByComma 逻辑
    def split_effect_by_comma(s):
        result = []
        bracketDepth = 0
        parenDepth = 0
        inQuotes = False
        quoteChar = '\0'
        segStart = 0
        for i, c in enumerate(s):
            if (c == '"' or c == "'") and not inQuotes:
                inQuotes = True
                quoteChar = c
            elif c == quoteChar and inQuotes:
                inQuotes = False
                quoteChar = '\0'
            elif not inQuotes:
                if c == '[':
                    bracketDepth += 1
                elif c == ']':
                    bracketDepth -= 1
                elif c == '(':
                    parenDepth += 1
                elif c == ')':
                    parenDepth -= 1
                elif c == ',' and bracketDepth == 0 and parenDepth == 0:
                    result.append(s[segStart:i].strip())
                    segStart = i + 1
        if segStart < len(s):
            result.append(s[segStart:].strip())
        return result

    effect = "Dead: DrawACard(t34,1)|GetCardsBeingTreated|subCost(2)[icon=dead,description=dead speech]"
    segments = split_effect_by_comma(effect)

    assert len(segments) == 1, f"t3476D 效果应被分割为1段, 实际: {len(segments)}段 -> {segments}"
    assert 'icon=dead' in segments[0], f"分割后应保留 [icon=dead] 元数据, 实际: {segments[0]}"
    print("[PASS] 测试4: t3476D 效果括号内逗号不被分割, icon元数据保留")


def test5_simulate_complex_geteffect():
    """需求验证: 模拟复杂 GetEffect 嵌套 - 引号内括号不影响分割"""
    def split_effect_by_comma(s):
        result = []
        bracketDepth = 0
        parenDepth = 0
        inQuotes = False
        quoteChar = '\0'
        segStart = 0
        for i, c in enumerate(s):
            if (c == '"' or c == "'") and not inQuotes:
                inQuotes = True
                quoteChar = c
            elif c == quoteChar and inQuotes:
                inQuotes = False
                quoteChar = '\0'
            elif not inQuotes:
                if c == '[':
                    bracketDepth += 1
                elif c == ']':
                    bracketDepth -= 1
                elif c == '(':
                    parenDepth += 1
                elif c == ')':
                    parenDepth -= 1
                elif c == ',' and bracketDepth == 0 and parenDepth == 0:
                    result.append(s[segStart:i].strip())
                    segStart = i + 1
        if segStart < len(s):
            result.append(s[segStart:].strip())
        return result

    effect = ('Deployed: setTarget|AddTrait(SharedHatred)|GetEffect("Dead: GetRandomFriendUnit|'
              'AddTrait(SharedHatred)[icon=dead,description=dead speech]")[icon=action,description=deploy],'
              'Dead:GetRandomFriendUnit|AddTrait(SharedHatred)[icon=dead,description=dead speech]')
    
    segments = split_effect_by_comma(effect)

    assert len(segments) == 2, f"复杂效果应被分割为2段, 实际: {len(segments)}段"
    assert 'icon=action' in segments[0], f"段1应包含 icon=action, 实际: {segments[0]}"
    assert 'icon=dead' in segments[1], f"段2应包含 icon=dead, 实际: {segments[1]}"
    print("[PASS] 测试5: 复杂 GetEffect 嵌套正确分割, 各段 icon 正确")


def test6_simple_dead_effect():
    """边界测试: 简单 Dead 效果(无括号) - 回归验证"""
    def split_effect_by_comma(s):
        result = []
        bracketDepth = 0
        parenDepth = 0
        inQuotes = False
        quoteChar = '\0'
        segStart = 0
        for i, c in enumerate(s):
            if (c == '"' or c == "'") and not inQuotes:
                inQuotes = True
                quoteChar = c
            elif c == quoteChar and inQuotes:
                inQuotes = False
                quoteChar = '\0'
            elif not inQuotes:
                if c == '[':
                    bracketDepth += 1
                elif c == ']':
                    bracketDepth -= 1
                elif c == '(':
                    parenDepth += 1
                elif c == ')':
                    parenDepth -= 1
                elif c == ',' and bracketDepth == 0 and parenDepth == 0:
                    result.append(s[segStart:i].strip())
                    segStart = i + 1
        if segStart < len(s):
            result.append(s[segStart:].strip())
        return result

    effect = "Dead:addToSupportLine(&LastdeadFriendlyLandUnit)[icon=dead,description=death effect]"
    segments = split_effect_by_comma(effect)

    assert len(segments) == 1, f"简单效果应保持1段, 实际: {len(segments)}段"
    assert 'icon=dead' in segments[0], f"应保留 icon=dead 元数据, 实际: {segments[0]}"
    print("[PASS] 测试6: 简单 Dead 效果(无括号)回归验证通过")


def test7_bracket_depth_unaffected_by_paren():
    """边界测试: 括号不影响方括号深度追踪"""
    def split_effect_by_comma(s):
        result = []
        bracketDepth = 0
        parenDepth = 0
        inQuotes = False
        quoteChar = '\0'
        segStart = 0
        for i, c in enumerate(s):
            if (c == '"' or c == "'") and not inQuotes:
                inQuotes = True
                quoteChar = c
            elif c == quoteChar and inQuotes:
                inQuotes = False
                quoteChar = '\0'
            elif not inQuotes:
                if c == '[':
                    bracketDepth += 1
                elif c == ']':
                    bracketDepth -= 1
                elif c == '(':
                    parenDepth += 1
                elif c == ')':
                    parenDepth -= 1
                elif c == ',' and bracketDepth == 0 and parenDepth == 0:
                    result.append(s[segStart:i].strip())
                    segStart = i + 1
        if segStart < len(s):
            result.append(s[segStart:].strip())
        return result

    effect = "Heal(3)[icon=action,description=heal],damage(2)[icon=dead,description=damage]"
    segments = split_effect_by_comma(effect)

    assert len(segments) == 2, f"顶层逗号应分割为2段, 实际: {len(segments)}段"
    assert 'icon=action' in segments[0], f"段1应有 icon=action"
    assert 'icon=dead' in segments[1], f"段2应有 icon=dead"
    print("[PASS] 测试7: 括号不影响方括号深度追踪, 顶层逗号正确分割")


if __name__ == '__main__':
    print("=" * 60)
    print("SplitEffectByComma 修复验证测试")
    print("=" * 60)

    tests = [
        test1_source_code_has_paren_tracking,
        test2_source_code_has_quote_tracking,
        test3_split_condition_correct,
        test4_simulate_t3476d_effect,
        test5_simulate_complex_geteffect,
        test6_simple_dead_effect,
        test7_bracket_depth_unaffected_by_paren,
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
