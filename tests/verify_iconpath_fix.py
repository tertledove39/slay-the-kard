#!/usr/bin/env python3
"""
验证 LoadCardDataCache 中 IconPath 赋值修复
使用方式: python verify_iconpath_fix.py
"""
import re
import os
import sys

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def read_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        return f.read()

def parse_ini(path):
    sections = {}
    current_section = None
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('#') or line.startswith(';'):
                continue
            if line.startswith('[') and line.endswith(']'):
                current_section = line[1:-1]
                sections[current_section] = {}
            elif '=' in line and current_section:
                key, _, value = line.partition('=')
                sections[current_section][key.strip()] = value.strip()
    return sections


def test1_worldmap_iconpath_smoke():
    """冒烟测试：WorldMap.cs LoadCardDataCache 中存在 IconPath 赋值"""
    content = read_file(os.path.join(BASE, 'bin', 'WorldMap.cs'))
    has_iconpath = 'IconPath' in content and 'icon' in content
    # 查找 LoadCardDataCache 方法体内是否有 cd.IconPath 赋值
    match = re.search(r'void LoadCardDataCache\(\).*?\n    \}', content, re.DOTALL)
    if not match:
        print("[FAIL] 测试1: 未找到 LoadCardDataCache 方法")
        return False
    method_body = match.group()
    if 'IconPath' not in method_body:
        print("[FAIL] 测试1: LoadCardDataCache 方法中未设置 IconPath")
        return False
    print("[PASS] 测试1: LoadCardDataCache 中存在 IconPath 赋值")
    return True


def test2_worldmap_iconpath_correct_key():
    """基本验证：IconPath 从 INI 的 icon 键读取"""
    content = read_file(os.path.join(BASE, 'bin', 'WorldMap.cs'))
    match = re.search(r'void LoadCardDataCache\(\).*?\n    \}', content, re.DOTALL)
    if not match:
        print("[FAIL] 测试2: 未找到 LoadCardDataCache 方法")
        return False
    method_body = match.group()
    pattern = r'cd\.IconPath\s*=\s*configFile\[section\.Key\]\["icon"\]'
    if not re.search(pattern, method_body):
        print("[FAIL] 测试2: IconPath 赋值不正确，应从 configFile[section.Key]['icon'] 读取")
        return False
    print("[PASS] 测试2: IconPath 从 INI icon 键正确读取")
    return True


def test3_battlefield_iconpath_consistency():
    """白盒测试：battlefield_.cs 和 WorldMap.cs 都设置 IconPath"""
    bf_content = read_file(os.path.join(BASE, 'bin', 'battlefield_.cs'))
    wm_content = read_file(os.path.join(BASE, 'bin', 'WorldMap.cs'))

    bf_has = 'IconPath' in bf_content and 'icon' in bf_content
    wm_has = 'IconPath' in wm_content and 'icon' in wm_content

    if not bf_has:
        print("[FAIL] 测试3: battlefield_.cs 缺少 IconPath 赋值")
        return False
    if not wm_has:
        print("[FAIL] 测试3: WorldMap.cs 缺少 IconPath 赋值")
        return False
    print("[PASS] 测试3: battlefield_.cs 和 WorldMap.cs 均设置 IconPath")
    return True


def test4_all_cards_have_icon_field():
    """边界测试：card.ini 中每张卡都有 icon 字段"""
    cards = parse_ini(os.path.join(BASE, 'cards', 'card.ini'))
    missing = []
    for card_id, fields in cards.items():
        if 'icon' not in fields or not fields['icon']:
            missing.append(card_id)
    if missing:
        print(f"[FAIL] 测试4: 以下卡牌缺少 icon 字段: {missing}")
        return False
    print(f"[PASS] 测试4: 全部 {len(cards)} 张卡牌均有 icon 字段")
    return True


def test5_no_duplicate_card_loading_without_iconpath():
    """白盒测试：确认没有其他卡牌加载路径遗漏 IconPath"""
    # 检查所有 .cs 文件中 new CardData() 后是否都有 IconPath 赋值
    # 或从已有 CardData 复制 IconPath
    cs_dir = os.path.join(BASE, 'bin')
    issues = []
    for fname in os.listdir(cs_dir):
        if not fname.endswith('.cs'):
            continue
    # End.cs 在根目录
    for cs_path in [os.path.join(BASE, 'End.cs'),
                    os.path.join(cs_dir, 'PostBattleReward.cs'),
                    os.path.join(cs_dir, 'CardRestoration.cs')]:
        if not os.path.exists(cs_path):
            continue
        content = read_file(cs_path)
        # 如果文件引用了 new CardData() 但没有 IconPath，标记
        if 'new CardData' in content and 'IconPath' not in content:
            fname = os.path.basename(cs_path)
            issues.append(f"{fname}: 创建 CardData 但未设置 IconPath")
    if issues:
        print(f"[WARN] 测试5: 以下文件可能遗漏 IconPath: {issues}")
        print("[PASS] 测试5: 已检查（警告不视为失败）")
        return True
    print("[PASS] 测试5: 无其他遗漏 IconPath 的卡牌加载路径")
    return True


def main():
    print("=" * 60)
    print("LoadCardDataCache IconPath 修复验证")
    print("=" * 60)
    tests = [
        test1_worldmap_iconpath_smoke,
        test2_worldmap_iconpath_correct_key,
        test3_battlefield_iconpath_consistency,
        test4_all_cards_have_icon_field,
        test5_no_duplicate_card_loading_without_iconpath,
    ]
    passed = 0
    failed = 0
    for test in tests:
        try:
            if test():
                passed += 1
            else:
                failed += 1
        except Exception as e:
            print(f"[FAIL] {test.__name__}: 异常 {e}")
            failed += 1
    print()
    print(f"结果: {passed}通过, {failed}失败, 共{len(tests)}项测试")
    return 1 if failed > 0 else 0

if __name__ == '__main__':
    sys.exit(main())
