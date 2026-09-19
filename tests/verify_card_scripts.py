#!/usr/bin/env python3
"""卡牌效果脚本的静态校验。

这里查的是「写错了也不报错、只是静默不生效」的那类问题：

1. 选择器语法。`setTargets` 用的是正则 `\\$\\{([^}]*)\\}`，**只认 `${...}`**。
   写成 `$(...)` 或裸 `$xxx` 时正则不匹配，targets 根本不会被赋值，
   后续指令遍历空列表，整张卡毫无效果且不报任何错。
   （`[第227号命令]` 与 `[血洒长空]` 都栽在这里。）

2. 选择器片段。`GetTargetsFromSelector` 对不认识的片段会走
   `ParseCardTypeFromName`，返回 null 时该片段被**静默忽略**——
   过滤条件凭空消失，结果集比预期大。

3. 跳转标签。`if(条件)标签&` 在 `labels` 里找不到 `标签` 时**不跳转**，
   于是条件形同虚设、效果体无条件执行。

解析逻辑按 battlefield_.cs 的 ParseAndExecuteEffect / GetTargetsFromSelector
复刻，注意 `SplitEffectString` 同时跟踪引号、圆括号与方括号。
"""
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

INI_FILES = ["cards/card.ini", "cards/enemyTurn.ini", "bin/event.ini"]

# GetTargetsFromSelector 支持的关键字片段
SELECTOR_KEYWORDS = {
    "allTargets", "allCardInHand", "unit", "command", "friend", "enemy",
    "hq", "land", "air", "damaged",
}
# 由 ParseCardTypeFromName 识别的卡牌类型（大小写不敏感）
CARD_TYPES = {"tank", "infantry", "artillery", "plane", "bomber", "command"}


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def strip_brackets(s):
    """剥离 [...] 元数据（description 里可能含 : | , 等分隔符）"""
    out, depth = [], 0
    for ch in s:
        if ch == "[":
            depth += 1
        elif ch == "]":
            depth -= 1
        elif depth == 0:
            out.append(ch)
    return "".join(out)


def strip_time_prefix(s):
    """复刻 ParseAndExecuteEffect 去时点前缀：只认引号外的第一个冒号。

    引号内的冒号必须跳过——`GetEffect("FriendlyTurnBegin: ...|skip&")` 这类写法
    如果把引号内的冒号当成时点前缀，整段会被截错位置、误判成标签缺失。
    """
    in_quotes, quote_char = False, ""
    for i, ch in enumerate(s):
        if ch in "\"'" and not in_quotes:
            in_quotes, quote_char = True, ch
        elif ch == quote_char and in_quotes:
            in_quotes, quote_char = False, ""
        elif ch == ":" and not in_quotes:
            return s[i + 1:]
    return s


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


def effect_blocks(path):
    """返回 [(section, effect_string)]"""
    text = read(path)
    for m in re.finditer(r"^\[([^\]]+)\](.*?)(?=^\[|\Z)", text, re.M | re.S):
        e = re.search(r"^effect\s*=\s*(.*)$", m.group(2), re.M)
        if e and e.group(1).strip():
            yield m.group(1), e.group(1).strip()


def main():
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    for f in INI_FILES:
        results.append(check((ROOT / f).exists(), f"{f} 存在"))
    cards = list(effect_blocks("cards/card.ini"))
    results.append(check(len(cards) > 0, f"card.ini 可解析出带效果的卡（{len(cards)} 张）"))

    # --------------------------- 1) 选择器语法 ---------------------------
    print("\n--- 选择器语法：只允许 ${...} ---")
    bare, dollar_paren = [], []
    for f in INI_FILES:
        text = read(f)
        for m in re.finditer(r"(?<![\$\{])\$([A-Za-z_][A-Za-z0-9_.]*)", text):
            bare.append((f, text[: m.start()].count("\n") + 1, m.group(0)))
        for m in re.finditer(r"\$\(([^)]*)\)", text):
            dollar_paren.append((f, text[: m.start()].count("\n") + 1, m.group(0)))

    for f, line, snippet in bare:
        print(f"        裸 $ 写法 {f}:{line}  {snippet}")
    for f, line, snippet in dollar_paren:
        print(f"        $( ) 写法 {f}:{line}  {snippet}")
    results.append(check(not bare,
                         "没有裸 $xxx 写法（正则只认 ${...}，裸写会静默失效）"))
    results.append(check(not dollar_paren,
                         "没有 $(...) 写法（正则只认 ${...}，圆括号会静默失效）"))

    # 对照：确有 ${...} 在用，证明该规则不是空转
    used_braces = sum(len(re.findall(r"\$\{[^}]*\}", read(f))) for f in INI_FILES)
    results.append(check(used_braces > 0, f"确有 ${{...}} 选择器在使用（{used_braces} 处）"))

    # --------------------------- 2) 选择器片段 ---------------------------
    print("\n--- 选择器片段合法性 ---")
    unknown = []
    selectors = set()
    for f in INI_FILES:
        for sel in re.findall(r"\$\{([^}]*)\}", read(f)):
            selectors.add(sel)
            for part in sel.split("."):
                part = part.strip()
                if not part:
                    continue
                if part in SELECTOR_KEYWORDS or part.lower() in CARD_TYPES:
                    continue
                unknown.append((f, sel, part))
    for f, sel, part in unknown:
        print(f"        未知片段 {f}: ${{{sel}}} 中的 '{part}'")
    results.append(check(not unknown,
                         f"${{...}} 里的每个片段都是解析器认识的（共 {len(selectors)} 个选择器）"))

    # --------------------------- 3) 跳转标签 ---------------------------
    print("\n--- 条件跳转的标签完整性 ---")
    missing, jumps = [], 0
    for path in ["cards/card.ini", "cards/enemyTurn.ini"]:
        for section, effect in effect_blocks(path):
            for segment in split_effect(effect):
                s = strip_brackets(segment)
                if ":" not in s:
                    continue
                s = strip_time_prefix(s)
                parts = [p.strip() for p in split_effect(s, "|")]
                labels = {p[:-1] for p in parts if p.endswith("&")}
                for p in parts:
                    m = re.match(r"^if\((.*?)\)(.+?)&$", p)
                    if not m:
                        continue
                    jumps += 1
                    if m.group(2).strip() not in labels:
                        missing.append((section, p, m.group(2).strip()))
    for section, p, target in missing:
        print(f"        [{section}] 跳转目标 '{target}' 无对应标签：{p[:70]}")
    results.append(check(not missing,
                         f"每处 if(...)标签& 都有对应的 标签&（共检查 {jumps} 处跳转）"))
    results.append(check(jumps > 0, "确有条件跳转在检查范围内，规则不是空转"))

    # --------------------------- 定点回归 ---------------------------
    print("\n--- 定点回归：曾写错的两张卡 ---")
    fixed = {
        "第227号命令": "${allTargets.unit.friend.damaged}",
        "血洒长空": "${allTargets.unit.friend.air}",
    }
    for section, selector in fixed.items():
        effect = dict(cards).get(section, "")
        results.append(check(selector in effect,
                             f"[{section}] 使用正确的 {selector} 写法"))
        results.append(check(f"${selector[2:]}" not in effect.replace(selector, ""),
                             f"[{section}] 不再残留漏花括号的写法"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
