#!/usr/bin/env python3
"""效果脚本「时点前缀」合法性校验。

`TriggerUnitEffects`（battlefield_.cs）用它做时点判定：

    var prefix = segment.Split(":")[0].Trim();
    if (prefix != triggerPoint) continue;      // 区分大小写的精确比较

前缀写错（大小写不符、拼错名字）时**不报任何错**，只是这一整段效果永远不执行。
i1005「步兵第1005团」的亡记就是这么失效的：写成 `dead:`，而死亡触发点传的是
`Dead:`，于是 `"dead" != "Dead"` 直接 continue，效果一次都没跑过。
同一 bug 还波及「雅克9」（`dead:setResult(1)|DrawCard|`）。

校验覆盖两个来源：
1. `effect = ...` 行里的时点前缀；
2. `GetEffect("...")` **内嵌串**里的时点前缀——内嵌串会被赋给目标卡，之后由
   TriggerUnitEffects 按同样的规则解析，所以写错同样静默失效。

前缀的提取必须复刻 TriggerUnitEffects 的做法：先用 SplitEffectString 按顶层逗号
切段（引号 / () / [] 内的逗号不生效），再取第一个冒号之前的部分——**不剥离** []
元数据。形如 `addToSupportLine(x)[icon=..,description=..]` 的段没有时点前缀，
取出来的"前缀"不是纯字母标识符，会被本脚本跳过（这类段本就不是时点触发，
由 GetEffect / 手牌打出等其它路径执行）。
"""
import re
from pathlib import Path
import sys

# 与项目其他测试一致：Windows 控制台默认按本地代码页输出，中文会乱码
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
INI_FILES = ["cards/card.ini", "cards/enemyTurn.ini", "bin/event.ini"]

# 曾失效的两张卡：定点回归
FIXED_CARDS = {"i1005": "Dead:", "雅克9": "Dead:"}


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def split_effect(s, delimiter=","):
    """复刻 SplitEffectString：引号 / () / [] 内的分隔符不生效"""
    out, current, paren, bracket = [], "", 0, 0
    in_quotes, quote_char = False, ""
    for ch in s:
        if ch in "\"'" and not in_quotes:
            in_quotes, quote_char = True, ch
            current += ch
        elif ch == quote_char and in_quotes:
            in_quotes, quote_char = False, ""
            current += ch
        elif in_quotes:
            current += ch
        else:
            if ch == "(":
                paren += 1
            elif ch == ")":
                paren -= 1
            elif ch == "[":
                bracket += 1
            elif ch == "]":
                bracket -= 1
            if ch == delimiter and paren == 0 and bracket == 0:
                out.append(current)
                current = ""
            else:
                current += ch
    if current:
        out.append(current)
    return out


def prefixes_of(effect_string):
    """产出该效果串里所有形如时点前缀的片段：(前缀, 该段原文)"""
    for segment in split_effect(effect_string):
        if ":" not in segment:
            continue
        prefix = segment.split(":")[0].strip()
        # 只关心形如时点前缀的纯字母标识符
        if not re.fullmatch(r"[A-Za-z]+", prefix):
            continue
        yield prefix, segment


def code_trigger_points():
    """代码中实际传给 TriggerUnitEffects 的触发点"""
    points = set()
    for path in ROOT.rglob("*.cs"):
        if "addons" in path.parts:
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        points |= set(re.findall(r'TriggerUnitEffects\(\s*"([A-Za-z]+)"', text))
    return points


def top_level_entries():
    """来源 1：effect = ... 行"""
    for rel in INI_FILES:
        if not (ROOT / rel).exists():
            continue
        section = None
        for lineno, line in enumerate(read(rel).splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("[") and stripped.endswith("]"):
                section = stripped[1:-1]
                continue
            if not stripped.startswith("effect") or "=" not in stripped:
                continue
            for prefix, segment in prefixes_of(stripped.split("=", 1)[1].strip()):
                yield rel, lineno, section, prefix, segment


def nested_entries():
    """来源 2：GetEffect("...") 内嵌串"""
    for rel in INI_FILES:
        if not (ROOT / rel).exists():
            continue
        for lineno, line in enumerate(read(rel).splitlines(), 1):
            for m in re.finditer(r'GetEffect\(\s*"([^"]*)"', line):
                for prefix, segment in prefixes_of(m.group(1)):
                    yield rel, lineno, "GetEffect内嵌", prefix, segment


def classify(entries, triggers):
    """把无法精确命中的前缀分成「仅大小写不符」与「完全未知」"""
    case_only, unknown = [], []
    for rel, lineno, section, prefix, segment in entries:
        if prefix in triggers:
            continue
        near = [t for t in triggers if t.lower() == prefix.lower()]
        (case_only if near else unknown).append((rel, lineno, section, prefix, near, segment))
    return case_only, unknown


def report(case_only, unknown):
    for rel, lineno, section, prefix, near, segment in case_only:
        print(f"        大小写不符 {rel}:{lineno} [{section}] "
              f"'{prefix}:' 应为 '{near[0]}:' —— {segment[:56]}")
    for rel, lineno, section, prefix, near, segment in unknown:
        print(f"        未知前缀 {rel}:{lineno} [{section}] "
              f"'{prefix}:' 不在触发点集合中 —— {segment[:56]}")


def main():
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    triggers = code_trigger_points()
    results.append(check(len(triggers) > 0,
                         f"从代码中抽到触发点（{len(triggers)} 个）"))
    results.append(check("Dead" in triggers,
                         "触发点集合包含 Dead（死亡时点确实存在）"))
    results.append(check(all((ROOT / f).exists() for f in INI_FILES),
                         "三个效果配置文件均存在"))

    top_entries = list(top_level_entries())
    nest_entries = list(nested_entries())
    results.append(check(len(top_entries) > 0,
                         f"从 effect 行抽到时点前缀（{len(top_entries)} 段）"))
    results.append(check(len(nest_entries) > 0,
                         f"从 GetEffect 内嵌串抽到时点前缀（{len(nest_entries)} 段）"))

    # ------------------- 来源 1：effect 行的前缀必须精确匹配 -------------------
    print("\n--- 需求基本验证：effect 行的每个时点前缀都能精确匹配 ---")
    case_only, unknown = classify(top_entries, triggers)
    report(case_only, unknown)
    results.append(check(not case_only,
                         f"effect 行没有大小写不符的时点前缀（异常 {len(case_only)} 处）"))
    results.append(check(not unknown,
                         f"effect 行没有未知的时点前缀（异常 {len(unknown)} 处）"))

    # ------------------- 来源 2：GetEffect 内嵌串同样必须匹配 -------------------
    # 内嵌串会被赋给目标卡，之后由 TriggerUnitEffects 按同样规则解析，
    # 所以这里写错一样是静默失效——上一版测试漏了这条路径。
    print("\n--- 边界白盒测试：GetEffect 内嵌串的时点前缀 ---")
    nest_case, nest_unknown = classify(nest_entries, triggers)
    report(nest_case, nest_unknown)
    results.append(check(not nest_case,
                         f"GetEffect 内嵌串没有大小写不符的前缀（异常 {len(nest_case)} 处）"))
    results.append(check(not nest_unknown,
                         f"GetEffect 内嵌串没有未知的前缀（异常 {len(nest_unknown)} 处）"))

    # ------------------------------ 定点回归 ------------------------------
    print("\n--- 定点回归：曾失效的两张卡 ---")
    sections = {}
    for rel in INI_FILES:
        if not (ROOT / rel).exists():
            continue
        text = read(rel)
        for m in re.finditer(r"^\[([^\]]+)\](.*?)(?=^\[|\Z)", text, re.M | re.S):
            e = re.search(r"^effect\s*=\s*(.*)$", m.group(2), re.M)
            if e:
                sections[m.group(1)] = e.group(1).strip()

    for card, expected in FIXED_CARDS.items():
        effect = sections.get(card, "")
        results.append(check(effect.startswith(expected),
                             f"[{card}] 时点前缀为 {expected}（当前：{effect[:30]}）"))
        results.append(check(re.match(r"^([a-z]+):", effect) is None,
                             f"[{card}] 不再残留小写时点前缀"))

    # --------------------------- 边界：规则非空转 ---------------------------
    print("\n--- 边界检查：规则不是空转 ---")
    used = {prefix for _, _, _, prefix, _ in top_entries}
    results.append(check(len(used) >= 5,
                         f"配置中确实使用了多个时点前缀（{len(used)} 种），校验非空转"))
    results.append(check("Dead" in used and "Deployed" in used,
                         "常用的 Dead / Deployed 都在校验范围内"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
