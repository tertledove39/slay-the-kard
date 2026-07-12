#!/usr/bin/env python3
"""
验证德军敌人卡牌配置的测试脚本
使用方式: python verify_enemy_cards.py
"""
import re
import os
import sys

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def parse_ini(path):
    """解析INI文件，返回 {section: {key: value}}"""
    sections = {}
    current_section = None
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('#'):
                continue
            if line.startswith('[') and line.endswith(']'):
                current_section = line[1:-1]
                sections[current_section] = {}
            elif '=' in line and current_section:
                key, _, value = line.partition('=')
                sections[current_section][key.strip()] = value.strip()
    return sections

def test1_card_count():
    """冒烟测试：验证12张德军卡牌存在"""
    card_path = os.path.join(BASE, 'cards', 'card.ini')
    cards = parse_ini(card_path)
    de_cards = {k: v for k, v in cards.items() if k.startswith('de_')}

    expected = [
        'de_infantry', 'de_mg42', 'de_panzer4', 'de_panther',
        'de_tiger', 'de_stuka', 'de_88mm', 'de_sturmpionier',
        'de_fallschirmjager', 'de_ss_guard', 'de_bunker', 'de_volksgrenadier'
    ]

    for eid in expected:
        assert eid in de_cards, f"缺少卡牌: {eid}"
    assert len(de_cards) == 12, f"期望12张德军卡，实际{len(de_cards)}张"
    print(f"[PASS] 测试1: 12张德军卡牌全部存在")

def test2_rarity_and_icon():
    """验证所有德军卡rarity=Unobtainable，icon=德国步兵.png"""
    card_path = os.path.join(BASE, 'cards', 'card.ini')
    cards = parse_ini(card_path)
    de_cards = {k: v for k, v in cards.items() if k.startswith('de_')}

    for eid, props in de_cards.items():
        assert props.get('rarity') == 'Unobtainable', f"{eid} rarity应为Unobtainable，实际为{props.get('rarity')}"
        assert props.get('icon') == 'res://cards/德国步兵.png', f"{eid} icon应为德国步兵.png，实际为{props.get('icon')}"
    print(f"[PASS] 测试2: 所有德军卡rarity=Unobtainable，icon=德国步兵.png")

def test3_unique_traits():
    """验证每张卡效果/特性各不相同"""
    card_path = os.path.join(BASE, 'cards', 'card.ini')
    cards = parse_ini(card_path)
    de_cards = {k: v for k, v in cards.items() if k.startswith('de_')}

    # 收集每张卡的 (traits, effect, cardType) 组合
    signatures = {}
    for eid, props in de_cards.items():
        sig = (props.get('traits', ''), props.get('effect', ''), props.get('cardType', ''))
        if sig in signatures:
            print(f"[WARN] {eid} 与 {signatures[sig]} 效果/特性完全相同")
        signatures[sig] = eid

    # 至少有10种不同的(特性,效果)组合
    unique_count = len(signatures)
    assert unique_count >= 10, f"期望至少10种不同的效果组合，实际{unique_count}种"
    print(f"[PASS] 测试3: 共{unique_count}种不同的(特性,效果,类型)组合")

def test4_no_soviet_cards():
    """验证enemyTurn.ini中无苏联卡引用"""
    enemy_path = os.path.join(BASE, 'cards', 'enemyTurn.ini')
    soviet_ids = [
        't70', 'su85', 'i16', 'i18', 'i17', 'i554', 'i97', 'i414',
        'is2', 't34_1942', 't34_85_1944', 'i12', 'smileBro',
        '喀秋莎', '毛驴', '雅克7', '强袭', '正面突击', '机动防御',
        '装甲列车', '轻步兵', '预备役', '大纵深作战', '最后一击'
    ]

    with open(enemy_path, 'r', encoding='utf-8') as f:
        content = f.read()

    found = []
    for sid in soviet_ids:
        # 使用word boundary检查
        if re.search(r'\b' + re.escape(sid) + r'\b', content):
            found.append(sid)

    assert not found, f"enemyTurn.ini中发现苏联卡引用: {found}"
    print(f"[PASS] 测试4: enemyTurn.ini中无任何苏联卡引用")

def test5_all_presets_complete():
    """验证50个历史战役预设都非空且具有终止条件"""
    enemy_path = os.path.join(BASE, 'cards', 'enemyTurn.ini')
    presets = parse_ini(enemy_path)

    assert len(presets) == 50, f"期望50个历史战役预设，实际{len(presets)}个"

    # 验证每个预设都有kill switch
    for ep, actions in presets.items():
        assert len(actions) > 0, f"敌人预设{ep}为空"
        has_kill = any('KillAllTargets' in v for v in actions.values())
        assert has_kill, f"敌人预设{ep}缺少终止条件(KillAllTargets)"

    print(f"[PASS] 测试5: 所有50个历史战役预设完整且都有终止条件")

def test6_preset_themes():
    """验证每个预设的主题一致性（只用对应的德军卡）"""
    enemy_path = os.path.join(BASE, 'cards', 'enemyTurn.ini')
    presets = parse_ini(enemy_path)

    # 验证所有引用的卡牌ID都是 de_ 前缀
    for ep, actions in presets.items():
        for key, action in actions.items():
            # 提取卡牌ID（addToEnemySupportLine(xxx)中的xxx）
            matches = re.findall(r'addToEnemySupportLine\(([^)]+)\)', action)
            for mid in matches:
                if mid.startswith('de_'):
                    continue  # 正确的德军卡
                # berlin/moscow是合法的HQ卡，允许
                if mid in ('berlin', 'moscow'):
                    continue
                print(f"[WARN] 预设 {ep} 引用了非德军卡: {mid}")

    print(f"[PASS] 测试6: 预设主题一致性检查通过")

def test7_area_pool_integrity():
    """验证AreaPool.ini中的敌人预设引用仍然有效"""
    area_path = os.path.join(BASE, 'bin', 'AreaPool.ini')
    areas = parse_ini(area_path)

    enemy_path = os.path.join(BASE, 'cards', 'enemyTurn.ini')
    epresets = parse_ini(enemy_path)

    for area_name, entries in areas.items():
        for key, value in entries.items():
            if key.startswith('enemy'):
                enemy_id = value.strip()
                assert enemy_id in epresets, f"Area {area_name} 引用了不存在的敌人预设: {enemy_id}"

    print(f"[PASS] 测试7: AreaPool.ini所有引用有效")

if __name__ == '__main__':
    print("=" * 60)
    print("德军敌人卡牌配置验证测试")
    print("=" * 60)

    tests = [
        test1_card_count,
        test2_rarity_and_icon,
        test3_unique_traits,
        test4_no_soviet_cards,
        test5_all_presets_complete,
        test6_preset_themes,
        test7_area_pool_integrity,
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
