#!/usr/bin/env python3
"""敌人意图面板验证：图标解析、缺失回退，以及一行多个行动各自成行。"""
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def split_top_level(value, delimiter=","):
    """按顶层分隔符切分，忽略 () 与 [] 内的分隔符——与 SplitEffectString 同规则。"""
    out, current, paren, bracket = [], "", 0, 0
    for ch in value:
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


def parse_metadata(action):
    """与 battlefield_.cs 的 ParseActionMetadata 同规则：只认最后一个 [...] 块。"""
    begin = action.rfind("[")
    if begin == -1:
        return ("boss", "")
    end = action.rfind("]")
    if end <= begin:
        return ("boss", "")
    icon, description = "boss", ""
    for field in action[begin + 1:end].split(","):
        separator = field.find("=")
        if separator <= 0:
            continue
        key, value = field[:separator].strip(), field[separator + 1:].strip()
        if key == "icon" and value:
            icon = value
        elif key == "description":
            description = value
    return (icon, description)


def intent_rows(action):
    """模拟 RefreshEnemyIntentPanel：按顶层逗号拆段，每个带描述的段出一行。"""
    rows = []
    for segment in split_top_level(action):
        icon, description = parse_metadata(segment)
        if description:
            rows.append((icon, description))
    return rows


def load_battles():
    """解析 enemyTurn.ini 的每个 section，只保留行动键。"""
    text = read("cards/enemyTurn.ini")
    battles, section = {}, None
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]"):
            section = stripped[1:-1]
            battles[section] = {}
            continue
        if "=" not in stripped or stripped.startswith("#") or section is None:
            continue
        key, value = stripped.split("=", 1)
        key = key.strip()
        if key == "name":
            continue
        if re.fullmatch(r"t\d+", key) or re.fullmatch(r"every\d+t", key) or key in ("default", "ADD"):
            battles[section][key] = value.strip()
    return battles


def main():
    battle = read("bin/battlefield_.cs")
    card = read("bin/cardBase_.cs")
    names = ("normalUnit", "bigUnit", "heal", "damage")
    results = [
        check(all((ROOT / "assest" / f"{name}.png").exists() for name in names), "all four intent icon assets exist"),
        check(all(f'"{name}"' in card for name in ("boss",) + names), "intent icon names are registered in IconCache"),
        check("ParseActionMetadata(segment)" in battle, "intent rows parse action metadata"),
        check('icon.Texture = IconCache.GetIcon(iconName) ?? IconCache.GetIcon("boss")' in battle, "intent rows resolve per-action icons with boss fallback"),
        check('string icon = "boss"' in battle and 'key == "icon"' in battle, "missing icons default to boss"),
        check('key == "description"' in battle and "meta.Split(',')" in battle, "icon and description metadata are parsed independently"),
        check('ResourceLoader.Load<Texture2D>("res://assest/boss.png")' not in battle, "intent UI no longer hard-codes boss texture"),
    ]

    # ---- 回归：一行多个行动必须各自成行 ----
    # 旧实现在整行上做 LastIndexOf('[')，只认最后一个元数据块，
    # 一行里前几个行动的描述被静默吞掉，界面上看不出敌人还部署了单位。
    refresh = battle.split("void RefreshEnemyIntentPanel()")[1].split("/// <summary>往意图面板追加一行")[0]
    # 剥掉注释再断言：注释里会提到旧写法，否则会误判
    refresh_code = "\n".join(
        line for line in refresh.splitlines() if not line.strip().startswith("//"))
    results += [
        check("SplitEffectString(action, ',')" in refresh,
              "意图面板按顶层逗号拆开行动行，而不是整行只取一个元数据块"),
        check("LastIndexOf" not in refresh_code,
              "渲染路径不再对整行做 LastIndexOf，避免只认最后一个元数据块"),
        check("void AddEnemyIntentRow(string iconName, string description)" in battle,
              "每行渲染抽成 AddEnemyIntentRow"),
        check(refresh.count("AddEnemyIntentRow(") == 1 and "foreach (string segment in" in refresh,
              "拆段循环内对每个带描述的段各调一次 AddEnemyIntentRow"),
    ]

    # ---- 行为验证：对每个战斗的每一行行动，新写法不得少显示任何一条描述 ----
    battles = load_battles()
    lost = []
    multi = []
    for name, actions in battles.items():
        for key, value in actions.items():
            segments = split_top_level(value)
            described = [s for s in segments if parse_metadata(s)[1]]
            if len(described) > 1:
                multi.append((name, key, [parse_metadata(s)[1] for s in described]))
            # 旧写法在整行上只解析出一个描述，若该行有两个以上带描述的段即会丢
            if len(described) > 1 and len(intent_rows(value)) < len(described):
                lost.append((name, key))
    results += [
        check(not lost, f"没有任何行动行的描述会被新写法丢掉（异常行：{lost}）"),
    ]

    if multi:
        for name, key, descriptions in multi:
            print(f"[INFO] [{name}] {key} 一行含 {len(descriptions)} 个行动，各出一行：{' / '.join(descriptions)}")
    else:
        print("[INFO] 当前没有任何行动行使用「一行多行动各自带元数据」的写法")

    # 新写法对只带一个元数据块的行必须仍然只出一行（否则会重复显示）
    dup = []
    for name, actions in battles.items():
        for key, value in actions.items():
            described = [s for s in split_top_level(value) if parse_metadata(s)[1]]
            if len(described) == 1 and len(intent_rows(value)) != 1:
                dup.append((name, key))
    results.append(check(not dup, f"单块行仍然只出一行，不会重复（异常行：{dup}）"))

    # 每行至少要有元数据，否则该行在面板上完全不可见
    bare = [(n, k) for n, a in battles.items() for k, v in a.items()
            if not any(parse_metadata(s)[1] for s in split_top_level(v))]
    results.append(check(not bare, f"每条行动都带 description（缺失：{bare}）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
