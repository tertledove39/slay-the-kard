#!/usr/bin/env python3
"""`&overflow` 不能再打回这一击已经打过的那个总部（BUGS #64）。

`Attack` 里有一句提前算好的溢出，供 `Attacking:` 效果里的 `&overflow` 用：

    lastOverflowDamage = Math.Max(0, attackDamage - to.ReadDefence());

问题是**目标就是总部时也算**。三张卡（伊尔2M / 乌拉 / 步兵第756团）写的都是
`Attacking:enemyHq|damage(&overflow)`——于是那份「溢出」被效果再打回**同一个总部**，
等于把这一击对同一个目标算了两遍。

实测（真起 battleField.tscn，伊尔2M 攻击力 4）：

    修复前：总部防御 3 -> -2（掉 5）；总部防御 2 -> -4（掉 6）
    修复后：总部防御 3 -> -1（掉 4）

溢出机制的语义是「打**单位**打过量了，多出来的溅到敌方总部」，所以修法是
目标是总部时溢出恒为 0 —— 而不是去改那三张卡。
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def body_of(source, signature):
    """截出某个方法的函数体（按大括号配平）。"""
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[start:index + 1]
    raise AssertionError(f"大括号不配平: {signature}")


def main():
    source = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    cards = (ROOT / "cards" / "card.ini").read_text(encoding="utf-8")

    attack = body_of(source, "public async Task Attack(cardBase_ from,cardBase_ to)")
    results = []

    # --- 核心：目标是总部时溢出必须是 0 ---
    results.append(check("to.isHq == HQ.hq" in attack,
                         "计算溢出时排除了「目标就是总部」"))
    # 注意别用 attack.index("lastOverflowDamage = ")——那会先命中函数开头那句
    # `lastOverflowDamage = 0;`（每次攻击前清零）。直接锚定这一整句的写法。
    anchor_text = "lastOverflowDamage = to.isHq == HQ.hq"
    results.append(check(anchor_text in attack,
                         "总部那一支就地写在赋值里（`lastOverflowDamage = to.isHq == HQ.hq ? ...`）"))
    if anchor_text in attack:
        snippet = attack[attack.index(anchor_text):][:160]
        results.append(check("? 0" in snippet
                             and "Math.Max(0, attackDamage - to.ReadDefence())" in snippet,
                             "总部 -> 0；其余 -> 攻击力 - 目标防御（两支都在）"))
    else:
        results.append(check(False, "总部 -> 0；其余 -> 攻击力 - 目标防御（两支都在）"))

    # --- 别修过头：打单位时的溢出照旧算 ---
    results.append(check("Math.Max(0, attackDamage - to.ReadDefence())" in attack,
                         "打单位时溢出照旧 = 攻击力 - 目标防御（溅射路径没被动）"))

    # --- 溢出是给效果用的，变量读取那一头不能改 ---
    results.append(check('case "overflow":' in source
                         and "sb.Append(lastOverflowDamage);" in source,
                         "&overflow 仍然读 lastOverflowDamage"))

    # --- 用这个机制的三张卡都还在（防止有人改卡去绕代码） ---
    for card_id in ("伊尔2m", "乌拉", "i756"):
        section_start = cards.index(f"[{card_id}]")
        section = cards[section_start:cards.index("\n[", section_start + 1)]
        results.append(check("Attacking:enemyHq|damage(&overflow)" in section,
                             f"[{card_id}] 仍然用 Attacking:enemyHq|damage(&overflow)"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
