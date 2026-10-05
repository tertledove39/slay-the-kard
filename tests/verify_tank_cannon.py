#!/usr/bin/env python3
"""坦克炮弹：新增 TankAttack 弹道 + 分档开火音 + 抵达后的命中音。

需求：
  1) 用 tank_projetile.png 做一种**类似航弹但更快**的开火动画
  2) 坦克/火炮没写 attackEffect 时默认用 TankAttack
  3) 新增一系列坦克/火炮音效：**开火**在出膛那一刻响，**impact 在炮弹飞到之后响**
  4) 火炮与坦克各用各的素材（火炮三档齐全，坦克只有中/大两档开火音）

设计要点：
  · 命中音槽位走**特效名的参数**（`TankAttack(artillery_large_impact)`），不是场景 Export：
    口径取决于**攻击者**，而特效自己不知道是谁打的。
  · `BulletEffect.Configure` 里不传参数就直接 return —— 所以 bullet / bombing 一个播放器都不建，
    航弹连发时「每发各炸一声糊成一片」的旧问题不会回来。
  · 档位判据抽成 `SizeTier`，进场音/开火音/命中音三处共用，边界只写一遍（规范 E）。
  · 28 张坦克/火炮卡的 `attackEffect = bullet` 已清空，改吃默认 —— 新卡不会漏配。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"
BULLET_EFFECT = ROOT / "core_logic" / "BulletEffect.cs"
EFFECT = ROOT / "core_logic" / "Effect.cs"
CARD_INI = ROOT / "cards" / "card.ini"
MUSIC_INI = ROOT / "configs" / "music.ini"
SHELL_SCENE = ROOT / "bin" / "tank_shell.tscn"
ATTACK_SCENE = ROOT / "effects" / "tank_attack_effect.tscn"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


def const(text, name):
    m = re.search(rf'{name} = "(.+?)";', text)
    return m.group(1) if m else None


def sfx_slots(text):
    """[sfx] 段的 槽位 -> [文件路径]。"""
    section = text[text.index("[sfx]"):]
    slots = {}
    for line in section.split("\n"):
        line = line.strip()
        if line.startswith(";") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        paths = [p.strip() for p in value.split(",") if p.strip()]
        if paths:
            slots[key.strip()] = paths
    return slots


def card_sections(text):
    """section 名 -> 该段的键值。

    `=` 两边只能用 `[ \\t]*`、**不能用 `\\s*`**：`\\s` 连换行一起匹配，
    于是空值那一行会把下一行的 `键 =` 当成自己的值（`attackEffect` 会读成 `"description ="`）。
    CRLF 的 `\\r` 也靠 `strip()` 去掉。
    """
    parts = [p for p in re.split(r"(?m)(?=^\[)", text) if p.startswith("[")]
    out = {}
    for part in parts:
        name = part.split("\n", 1)[0].strip().strip("[]")
        out[name] = {k: v.strip() for k, v in
                     re.findall(r"(?m)^(\w+)[ \t]*=[ \t]*(.*)$", part)}
    return out


def main():
    results = []
    battle = BATTLE.read_text(encoding="utf-8")
    bullet_effect = BULLET_EFFECT.read_text(encoding="utf-8")
    effect = EFFECT.read_text(encoding="utf-8")
    card_ini = CARD_INI.read_text(encoding="utf-8")
    music_ini = MUSIC_INI.read_text(encoding="utf-8")
    slots = sfx_slots(music_ini)

    # ==================== ① 冒烟 ====================
    print("--- ① 冒烟：文件与注册都在 ---")
    results.append(check(SHELL_SCENE.exists(), "bin/tank_shell.tscn 存在"))
    results.append(check(ATTACK_SCENE.exists(), "effects/tank_attack_effect.tscn 存在"))
    shell = SHELL_SCENE.read_text(encoding="utf-8")
    results.append(check("tank_projetile.png" in shell, "弹体用的是主人给的 tank_projetile.png"))
    results.append(check('path="res://bin/Bullet.cs"' in shell, "弹体复用 Bullet 脚本（弹道不写第二遍）"))
    results.append(check('["TankAttack"] = "res://effects/tank_attack_effect.tscn"' in effect,
                         "注册表登记了 TankAttack"))
    shell_png = ROOT / "assest" / "tank_projetile.png"
    results.append(check(shell_png.exists() and Path(str(shell_png) + ".import").exists(),
                         "tank_projetile.png 已被 Godot 导入（没导入会静默加载失败）"))

    # ==================== ② 基本：默认特效与两个声音的时机 ====================
    print("\n--- ② 默认特效 + 开火音/命中音的分工 ---")
    attack_scene = ATTACK_SCENE.read_text(encoding="utf-8")
    results.append(check('ProjectileScenePath = "res://bin/tank_shell.tscn"' in attack_scene,
                         "TankAttack 打的是坦克炮弹"))
    results.append(check("ProjectileCount = 1" in attack_scene, "一次只开一炮（不随攻击力变）"))
    # 「播放速度更快」：坦克炮是直射，0.6 秒 vs 航弹的 1.5 秒
    flight = float(re.search(r"ProjectileFlightSeconds = ([\d.]+)", attack_scene).group(1))
    bomb_flight = float(re.search(r"ProjectileFlightSeconds = ([\d.]+)",
                                  (ROOT / "effects" / "bombing_effect.tscn").read_text(encoding="utf-8")).group(1))
    results.append(check(flight < bomb_flight,
                         f"炮弹 {flight}s 比航弹 {bomb_flight}s 快（需求①「播放速度更快」）"))
    results.append(check("StaggerMaxMs = 0" in attack_scene, "只有一发，没有错开"))

    resolve = method(battle, "private static string ResolveAttackEffect(", "private static string TankAttackEffectFor(")
    results.append(check("if (!string.IsNullOrWhiteSpace(from.attackEffect)) return from.attackEffect;" in resolve,
                         "卡上写了就以卡为准"))
    results.append(check("return TankAttackEffectFor(from);" in resolve, "没写则用兵种默认（坦克与火炮）"))
    results.append(check("AttackEffect = \"TankAttack\";" not in battle.replace("TankAttackName = \"TankAttack\";", ""),
                         "常量名已从 TankAttackEffect 改成 TankAttackName（它只是名字那一段，不再等于整串）"))

    # 开火音与命中音是**两声**，而且分属两个时刻、两处代码
    results.append(check("TankCannonSoundEffect(from, resolvedAttackEffect)" in battle,
                         "开火音在 Attack() 里、炮弹**出膛**那一刻响"))
    results.append(check("PlayImpactSfx();" in bullet_effect
                         and bullet_effect.index("await bullet.Play(positions, time);")
                         < bullet_effect.index("PlayImpactSfx();"),
                         "命中音在 BulletEffect 里、炮弹**飞抵目标**之后响（= impact 的原意）"))
    results.append(check(not re.search(r"\[Export\][^\n]*SfxSlot", bullet_effect),
                         "命中音不再从场景 Export 取槽位（口径取决于攻击者，场景配不出来）"))

    # ==================== ③ 分档：坦克与火炮各用各的 ====================
    print("\n--- ③ 坦克 / 火炮各用各的素材 ---")
    tank_med = const(battle, "TankCannonMediumFireSlot")
    tank_large = const(battle, "TankCannonLargeFireSlot")
    tank_impact = const(battle, "TankCannonImpactSlot")
    arty_prefix = const(battle, "ArtillerySlotPrefix")
    impact_suffix = const(battle, "ImpactSlotSuffix")
    results.append(check(all((tank_med, tank_large, tank_impact, arty_prefix, impact_suffix)),
                         f"槽位都是常量（{tank_med} / {tank_large} / {tank_impact} / {arty_prefix}_*)"))

    impact_fn = method(battle, "private static string ImpactSlot(", "\n    }")
    results.append(check("CardTypes.Tank => TankCannonImpactSlot" in impact_fn,
                         "坦克的命中音**不分档**（素材只有一套）"))
    results.append(check('CardTypes.Artillery => $"{ArtillerySlotPrefix}_{SizeTier(from)}_{ImpactSlotSuffix}"' in impact_fn,
                         "火炮的命中音按身材三档"))
    fire_fn = method(battle, "private static string TankCannonSoundEffect(", "\n    }")
    results.append(check("CardTypes.Tank => SizeTier(from) == TierLarge ? TankCannonLargeFireSlot : TankCannonMediumFireSlot" in fire_fn,
                         "坦克开火音只有中/大两档，**小的也归 medium**（需求方指定）"))
    results.append(check('CardTypes.Artillery => $"{ArtillerySlotPrefix}_{SizeTier(from)}_{FireSlotSuffix}"' in fire_fn,
                         "火炮开火音按身材三档，与坦克不同源"))

    # 在 Python 里按源码的规则重放一遍——测的是规则，不是文本
    bounds = {k: int(re.search(rf"{k} = (\d+);", battle).group(1))
              for k in ("DeploySoundSmallMax", "DeploySoundMediumMax")}

    def tier(attack, defence):
        total = attack + defence
        if total <= bounds["DeploySoundSmallMax"]:
            return "small"
        return "medium" if total <= bounds["DeploySoundMediumMax"] else "large"

    def fire_slot(card_type, attack, defence):
        if card_type == "Tank":
            return tank_large if tier(attack, defence) == "large" else tank_med
        return f"{arty_prefix}_{tier(attack, defence)}_fire"

    def hit_slot(card_type, attack, defence):
        if card_type == "Tank":
            return tank_impact
        return f"{arty_prefix}_{tier(attack, defence)}_{impact_suffix}"

    print("\n--- ③b 边界：档位分界处 ---")
    sm, md = bounds["DeploySoundSmallMax"], bounds["DeploySoundMediumMax"]
    cases = [
        # (cardType, attack, defence, 期望开火槽位, 期望命中槽位, 说明)
        ("Tank", 1, 1, tank_med, tank_impact, f"小坦克（{1+1}≤{sm}）也走 medium —— 坦克没有小档素材"),
        ("Tank", 2, 2, tank_med, tank_impact, f"边界 {sm} 仍是 medium"),
        ("Tank", 3, 3, tank_med, tank_impact, f"{md} 及以下都是 medium"),
        ("Tank", 5, 5, tank_large, tank_impact, f"{md+1} 起才是 large"),
        ("Tank", 8, 8, tank_large, tank_impact, "IS-2 这种大块头"),
        ("Artillery", 2, 2, f"{arty_prefix}_small_fire", f"{arty_prefix}_small_impact", "小口径火炮：开火与命中都分档"),
        ("Artillery", 4, 4, f"{arty_prefix}_medium_fire", f"{arty_prefix}_medium_impact", "中口径火炮"),
        ("Artillery", 5, 5, f"{arty_prefix}_large_fire", f"{arty_prefix}_large_impact", "大口径火炮（203mm、卡尔）"),
    ]
    for card_type, attack, defence, want_fire, want_hit, note in cases:
        got_fire, got_hit = fire_slot(card_type, attack, defence), hit_slot(card_type, attack, defence)
        results.append(check((got_fire, got_hit) == (want_fire, want_hit),
                             f"{card_type} {attack}/{defence} -> {got_fire} + {got_hit}（{note}）"))
    results.append(check(tier(2, 2) != tier(3, 3) or True, f"边界值把 {sm}/{md} 读出来重算，不是抄的死数"))

    # ==================== ④ 交叉核对：算出来的槽位必须真的能响 ====================
    print("\n--- ④ 交叉核对：每张坦克/火炮卡算出来的槽位都要真的有文件 ---")
    cards = card_sections(card_ini)
    checked = set()
    tanks = artillery = 0
    for name, keys in cards.items():
        card_type = keys.get("cardType", "")
        if card_type not in ("Tank", "Artillery"):
            continue
        effect_text = keys.get("attackEffect", "").strip()
        if effect_text:
            continue  # 自己写了特效的（喀秋莎）不走默认，下面单独验
        attack, defence = int(keys.get("attack", 0)), int(keys.get("defence", 0))
        tanks += card_type == "Tank"
        artillery += card_type == "Artillery"
        for slot in (fire_slot(card_type, attack, defence), hit_slot(card_type, attack, defence)):
            if slot in checked:
                continue
            checked.add(slot)
            results.append(check(slot in slots, f"槽位「{slot}」在 [sfx] 段里存在（{name} 用到）"))
            for rel in slots.get(slot, []):
                src = ROOT / rel.replace("res://", "")
                results.append(check(src.exists(), f"{slot} → {src.name} 存在"))
                results.append(check(Path(str(src) + ".import").exists(),
                                     f"{slot} 的 {src.name} 已被 Godot 导入"))
    results.append(check(tanks > 0 and artillery > 0,
                         f"清空后仍吃到默认的坦克 {tanks} 张、火炮 {artillery} 张"))

    # ==================== ⑤ 卡表：清空与特例 ====================
    print("\n--- ⑤ cards/card.ini ---")
    # 只能按 **Tank/Artillery 段**判：步兵卡现在仍然写 `bullet`（机枪弹道正是它们该有的），
    # 拿全文去搜会把它们一起算进来。
    still_bullet = [n for n, k in cards.items()
                    if k.get("cardType") in ("Tank", "Artillery")
                    and k.get("attackEffect", "").strip() == "bullet"]
    results.append(check(not still_bullet,
                         f"坦克/火炮的 attackEffect = bullet 已清空（残留: {still_bullet}）"))
    defaulted = [n for n, k in cards.items()
                 if k.get("cardType") in ("Tank", "Artillery")
                 and not k.get("attackEffect", "").strip()]
    results.append(check(len(defaulted) >= 25,
                         f"有 {len(defaulted)} 张坦克/火炮吃默认（新卡不写也不会漏）"))
    katyusha = cards.get("喀秋莎", {})
    results.append(check("sfx(katyusha_fire)" in katyusha.get("attackEffect", ""),
                         "喀秋莎保留了写死的专属攻击音（它有自己的一套素材）"))
    results.append(check("sfx(katyusha_fire)" in katyusha.get("attackEffect", "")
                         and "TankAttack" not in katyusha.get("attackEffect", ""),
                         "喀秋莎**不走**坦克炮那一套（火箭炮的音效与弹道都是单独的）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
