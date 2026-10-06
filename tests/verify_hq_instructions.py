#!/usr/bin/env python3
"""总部(HQ)效果指令验证。

HQ 的「血」是 defence，attack 恒为 0 且总部不会攻击，
因此对 HQ 施加增减攻击力的指令没有任何效果。

步兵第845团原写作 `TakingDamage: myHq|GetAttack(&lastDamage)`，
描述却是「受到伤害时友方总部恢复等量的防御力」——加的是攻击力，
总部防御力纹丝不动，表现为「无法给总部加血」。现改为 `Heal`。
"""
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

HQ_SELECTORS = {"myhq", "enemyhq"}

# 切换 targets 的选择器：出现后 HQ 不再是当前目标
OTHER_SELECTORS = {
    "this", "target", "getallfriendunits", "getallenemyunits",
    "getallfriendtargets", "getallenemytargets",
    "getrandomfriendunit", "getrandomenemyunit",
    "getrandomfriendtarget", "getrandomenemytarget",
    "getcardbeingaddtosupportline", "getcardbeingaddtohand",
    "getlefttarget", "getrighttarget", "settarget",
}

# 对 HQ 无意义的指令：HQ 不攻击，增减攻击力不产生任何可观察效果
FORBIDDEN_ON_HQ = {"getattack", "loseattack"}

# 合法的 HQ 指令（用于反向确认规则不是空转）
KNOWN_HQ_INSTRUCTIONS = {"heal", "damage", "adddefence", "setdefence", "killalltarget", "killalltargets"}


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def strip_brackets(text):
    """剥离所有 [...] 元数据块（description 文本里可能含 | 或 ,）。"""
    out, depth = [], 0
    for ch in text:
        if ch == "[":
            depth += 1
        elif ch == "]":
            depth -= 1
        elif depth == 0:
            out.append(ch)
    return "".join(out)


def normalize(part):
    """去掉触发前缀（如 TakingDamage:）与首尾空白，返回小写形式。"""
    part = part.strip()
    if ":" in part:
        part = part.split(":", 1)[1].strip()
    return part.lower()


def hq_instructions(effect):
    """返回施加在 HQ 上的指令名列表（小写，不含参数）。"""
    found = []
    on_hq = False
    for raw in strip_brackets(effect).split("|"):
        token = normalize(raw)
        if token in HQ_SELECTORS:
            on_hq = True
            continue
        if token in OTHER_SELECTORS:
            on_hq = False
            continue
        if on_hq:
            match = re.match(r"([A-Za-z_][A-Za-z0-9_]*)\s*\(", token)
            if match:
                found.append(match.group(1))
    return found


def parse_ini(path):
    """返回 {section: {key: value}}，保留原始大小写。"""
    sections, section = {}, None
    for line in read(path).splitlines():
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]"):
            section = stripped[1:-1]
            sections[section] = {}
            continue
        if "=" not in stripped or stripped.startswith("#") or section is None:
            continue
        key, value = stripped.split("=", 1)
        sections[section][key.strip()] = value.strip()
    return sections


def main():
    cards = parse_ini("cards/card.ini")
    battles = parse_ini("cards/enemyTurn.ini")

    results = [
        check(len(cards) > 0, f"card.ini 可解析（{len(cards)} 张卡）"),
        check(len(battles) > 0, f"enemyTurn.ini 可解析（{len(battles)} 个战斗）"),
    ]

    # ---- 收集所有 HQ 指令 ----
    sites, violations = [], []
    for source, sections in (("card.ini", cards), ("enemyTurn.ini", battles)):
        for section, fields in sections.items():
            for key, value in fields.items():
                if key in ("name", "icon", "description", "price", "attack", "defense", "rarity"):
                    continue
                for ins in hq_instructions(value):
                    sites.append((source, section, key, ins))
                    if ins in FORBIDDEN_ON_HQ:
                        violations.append((source, section, key, ins, value))

    results.append(check(
        not violations,
        f"没有任何效果对总部增减攻击力（HQ 的 attack 恒为 0，不产生可观察效果）"
        + ("" if not violations else f" —— 违规：{[(s, sec) for s, sec, _, _, _ in violations]}")))

    for source, section, key, ins, value in violations:
        print(f"         [{source}] [{section}] {key}: {ins}  <- {value[:80]}")

    # ---- 回归：规则确有约束对象，不是空转 ----
    used = {ins for _, _, _, ins in sites}
    results += [
        check(len(sites) > 0, f"确实存在施加在总部上的指令（{len(sites)} 处）"),
        check(bool(used & KNOWN_HQ_INSTRUCTIONS),
              f"总部指令使用了防御类的合法写法（出现：{sorted(used & KNOWN_HQ_INSTRUCTIONS)}）"),
        check("heal" in used, "总部回血使用 heal（→ ChangeType.GetDefence），而非加攻类指令"),
    ]

    # ---- 定点回归：步兵第845团 ----
    i845 = cards.get("i845", {}).get("effect", "")
    results += [
        check("i845" in cards, "步兵第845团存在"),
        check("myHq|Heal(&lastDamage)" in i845,
              "步兵第845团对总部使用 Heal(&lastDamage) 恢复等量防御力"),
        check("GetAttack" not in i845, "步兵第845团不再对总部加攻击力"),
    ]

    # 卡面主描述与实际效果必须一致
    desc = cards.get("i845", {}).get("description", "")
    results.append(check("恢复" in desc and "防御" in desc,
                         "步兵第845团的卡面描述仍为「恢复等量的防御力」，与效果一致"))

    # ---- 触发链完整性：&lastDamage 必须在 TakingDamage 之前被赋值 ----
    battle = read("bin/battlefield_.cs")
    assign_at = battle.find("lastDamage = attackDamage;")
    trigger_at = battle.find('TriggerUnitEffects("TakingDamage"')
    results += [
        check(0 < assign_at < trigger_at,
              "lastDamage 在 TakingDamage 触发之前赋值，&lastDamage 取得到正确数值"),
        check('if (attackDamage > 0)' in battle, "TakingDamage 在伤害大于 0 时才触发"),
        check('if(ins == "myhq")' in battle and "targets = [myHq];" in battle,
              "myhq 选择器解析为玩家总部"),
    ]

    # 非 HQ 目标仍可用加攻类指令（避免规则过宽）
    friendly_buff = [k for k, v in cards.items()
                     if re.search(r"GetAllFriendUnits\|GetAttack\(", v.get("effect", ""))]
    results.append(check(bool(friendly_buff),
                         f"对友方单位使用 GetAttack 仍被允许（{len(friendly_buff)} 张卡）"))

    if sites:
        print(f"\n[INFO] 全库共 {len(sites)} 处对总部下发的指令，"
              f"指令种类：{sorted(used)}")

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
