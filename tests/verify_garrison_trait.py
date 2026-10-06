#!/usr/bin/env python3
"""驻守（Garrison）特性校验。

需求：具有此 trait 的单位**无法移动或攻击，但不影响反击**。

实现要点（每一处写错都静默失效，故逐条钉死）：

  1. 新增特性必须同步 `docs/NOTICE.md` 列的 8 个点。其中
     `GetAllAttributes()` 是 `Enum.GetValues` 遍历，`GetUnitTraits()` / `CardParser`
     是 `Enum.TryParse`/`Enum.Parse`，**自动生效**；其余 5 处要手动加。
     `assest/protect.png` 还必须进 `IconCache.KnownIcons`——`GetIcon()` 只查预加载好的
     缓存，漏了会直接返回 null，图标静默不显示。
  2. **`RefreshUnit()` 必须重新钳制**。它在回合开始时把 `moveAble/attackAble` 恢复成 1/2，
     驻守是常驻的，不重钳的话每个回合开始都会被放行，特性形同失效。
  3. **反击不受影响**是天然成立的：`Attack()` 里判定反击只看兵种（非轰炸机）、冲击与
     伏击，从不读 `attackAble`。本测试把这一点钉成断言，防止以后有人往反击判定里加
     `attackAble` 检查而破坏需求。
  4. `Fight` / `FightRandomEnemy` 会**临时把 `attackAble` 抬到 1** 再调 `Attack()`
     （注释写明「不计入攻击次数」）。不显式挡一下，驻守单位会被「强制参战」绕过。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
CARDBASE = ROOT / "bin" / "cardBase_.cs"
BATTLE = ROOT / "bin" / "battlefield_.cs"
ICON = ROOT / "assest" / "protect.png"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    cardbase = CARDBASE.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    results.append(check(CARDBASE.exists() and BATTLE.exists(), "两个源文件均存在"))
    results.append(check(ICON.exists(), "assest/protect.png 存在（需求方已放入）"))
    enum = re.search(r"public enum UnitTraits\s*\{(.*?)\n\}", cardbase, re.S)
    results.append(check(enum is not None, "UnitTraits 枚举可定位"))
    enum_body = enum.group(1) if enum else ""
    results.append(check(re.search(r"Garrison\s*=\s*1\s*<<\s*11", enum_body) is not None,
                         "枚举新增 Garrison = 1 << 11（与已有的 10 个位不冲突）"))
    # 位不能撞车
    bits = re.findall(r"=\s*1\s*<<\s*(\d+)", enum_body)
    results.append(check(len(bits) == len(set(bits)), f"位标记无重复（共 {len(bits)} 个）"))

    # --------------------- 1) NOTICE.md 列的同步点 ---------------------
    print("\n--- 1) 特性同步点（NOTICE.md 第三节） ---")
    add = re.search(r"public void AddTrait\(UnitTraits trait\)(.*?)\n    \}", cardbase, re.S)
    rem = re.search(r"public void RemoveTrait\(UnitTraits trait\)(.*?)\n    \}", cardbase, re.S)
    add_body, rem_body = (add.group(1) if add else ""), (rem.group(1) if rem else "")
    results.append(check("ActionForbiddingTraits" in add_body, "AddTrait 处理了禁止行动类特性"))
    results.append(check("moveAble = 0" in add_body and "attackAble = 0" in add_body,
                         "AddTrait 关掉移动与攻击"))
    results.append(check("UpdateMoveableLight()" in add_body, "AddTrait 刷新了指示灯"))
    results.append(check("ActionForbiddingTraits" in rem_body, "RemoveTrait 处理了禁止行动类特性"))
    results.append(check("驻守" in cardbase and 'names.Append("驻守 ")' in cardbase,
                         "BuildTraitPrefix 输出「驻守」前缀"))
    results.append(check('UnitTraits.Garrison => "驻守：' in cardbase, "GetTraitDescription 有条目"))
    results.append(check("{ UnitTraits.Garrison, \"protect\" }," in cardbase,
                         "IconCache.TraitIcons 映射到 protect"))
    results.append(check('"suppress", "protect",' in cardbase,
                         "IconCache.KnownIcons 含 protect（漏了图标会静默不显示）"))
    results.append(check("Enum.GetValues(typeof(UnitTraits))" in cardbase,
                         "GetAllAttributes 按枚举遍历，自动覆盖新特性（无需单独加）"))
    results.append(check("Enum.TryParse<UnitTraits>" in battle,
                         "GetUnitTraits 用 TryParse，AddTrait(Garrison) 自动可用"))

    # --------------------- 2) RefreshUnit 必须重新钳制（回归核心） ---------------------
    print("\n--- 2) RefreshUnit 重新钳制（回归核心） ---")
    ru = re.search(r"public void RefreshUnit\(\)(.*?)\n    \}", cardbase, re.S)
    ru_body = ru.group(1) if ru else ""
    results.append(check(ru is not None, "RefreshUnit 可定位"))
    idx_restore = ru_body.find("attackAble = 1;")
    idx_clamp = ru_body.find("IsActionForbidden()")
    results.append(check(idx_restore != -1 and idx_clamp != -1 and idx_restore < idx_clamp,
                         "钳制位于「恢复行动」之后——否则常驻的驻守每回合被放行"))
    results.append(check("moveAble = 0" in ru_body[idx_clamp:] if idx_clamp != -1 else False,
                         "钳制确实把 moveAble 归零"))

    # --------------------- 3) 反击不受影响 ---------------------
    print("\n--- 3) 反击不受影响（需求原文：但可以不影响反击） ---")
    cidx = battle.find("receivesCounterAttack")
    window = battle[cidx:cidx + 3000] if cidx != -1 else ""
    results.append(check(cidx != -1, "找到反击判定区"))
    results.append(check("attackAble" not in window,
                         "反击判定全程不读 attackAble——故置 0 不影响反击（rule 未被破坏）"))
    results.append(check("canCounterAttack" in window and "ambushTriggered" in window,
                         "反击只看兵种 / 冲击 / 伏击（断言非空转）"))

    # --------------------- 4) 强制参战路径被挡住 ---------------------
    print("\n--- 4) Fight / FightRandomEnemy 不得绕过 ---")
    results.append(check(battle.count("IsActionForbidden()") == 2,
                         "两条强制参战指令各挡了一次（共 2 处调用）"))
    fight = re.search(r'else if \(ins == "fight"\)(.*?)\n                \}', battle, re.S)
    results.append(check(fight is not None and "IsActionForbidden()" in fight.group(1),
                         "Fight 里挡了（它会临时把 attackAble 抬到 1）"))
    freq = re.search(r'instruction\.StartsWith\("FightRandomEnemy".*?\n                \}', battle, re.S)
    results.append(check(freq is not None and "IsActionForbidden()" in freq.group(0),
                         "FightRandomEnemy 里挡了"))

    # --------------------- 5) 单一实现 ---------------------
    print("\n--- 5) 「哪些特性禁止行动」只定义一处 ---")
    results.append(check(
        cardbase.count("UnitTraits.Garrison | UnitTraits.Suppressed") == 1,
        "Garrison|Suppressed 的组合只在 ActionForbiddingTraits 常量里出现一次"))
    results.append(check("private const UnitTraits ActionForbiddingTraits" in cardbase,
                         "ActionForbiddingTraits 是 const（编译期掩码）"))
    results.append(check(
        cardbase.count("ActionForbiddingTraits") >= 4,
        "AddTrait / RemoveTrait / IsActionForbidden 共用同一掩码（非各写一份）"))

    # --------------------- 6) 与压制共用一套机制（防空转） ---------------------
    print("\n--- 6) 与既有 Suppressed 共用一套机制 ---")
    results.append(check("UnitTraits.Suppressed" in cardbase,
                         "Suppressed 仍在（掩码覆盖它，同时补上了它被 Fight 绕过的洞）"))
    results.append(check("IsActionForbidden" in cardbase and "public bool IsActionForbidden()" in cardbase,
                         "对外暴露 IsActionForbidden() 供 battlefield_ 调用"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
