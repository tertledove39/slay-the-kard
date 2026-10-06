#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    card_ini = (ROOT / "cards" / "card.ini").read_text(encoding="utf-8")
    card = (ROOT / "bin" / "cardBase_.cs").read_text(encoding="utf-8")
    world = (ROOT / "bin" / "WorldMap.cs").read_text(encoding="utf-8")
    battle = (ROOT / "bin" / "battlefield_.cs").read_text(encoding="utf-8")
    effect = (ROOT / "core_logic" / "Effect.cs").read_text(encoding="utf-8")
    bullet = (ROOT / "bin" / "Bullet.cs").read_text(encoding="utf-8")
    controller = (ROOT / "core_logic" / "BulletEffect.cs").read_text(encoding="utf-8")
    smoke = (ROOT / "core_logic" / "SmokeEffect.cs").read_text(encoding="utf-8")
    pool = (ROOT / "core_logic" / "BattleEffectPool.cs").read_text(encoding="utf-8")
    smoke_scene = (ROOT / "effects" / "smoke_effect.tscn").read_text(encoding="utf-8")
    sections = [part for part in re.split(r"(?=^\[)", card_ini, flags=re.MULTILINE) if part.startswith("[")]
    units = [part for part in sections if re.search(r"^cardType\s*=\s*(?!Command\s*$)\w+", part, re.MULTILINE)]
    commands = [part for part in sections if re.search(r"^cardType\s*=\s*Command\s*$", part, re.MULTILINE)]
    results = [
        check("PlayEffect" in card and "AttackEffect" in card and "playEffect" in card and "attackEffect" in card, "card models contain both optional effect fields"),
        check(all(key in world and key in battle for key in ('"playEffect"', '"attackEffect"')), "both card loaders read effect fields"),
        check("TryGetValue" in world and "TryGetValue" in battle, "missing effect keys are read safely"),
        check("abstract partial class Effect" in effect and "IReadOnlyList<Vector2> positions = null" in effect and "float? time = null" in effect, "Effect exposes optional positions and time"),
        check('["bullet"] = "res://effects/bullet_effect.tscn"' in effect, "bullet short name is registered"),
        check('["smoke"] = "res://effects/smoke_effect.tscn"' in effect, "smoke short name is registered"),
        check("class Bullet : Effect" in bullet and "class BulletEffect : Effect" in controller, "existing bullet visuals derive from Effect"),
        check("class SmokeEffect : Effect" in smoke and "FrameCount = 16" in smoke, "smoke effect plays all 16 frames"),
        check("TweenMethod" in smoke and "UpdateProgress" in smoke and "CreateTimer" not in smoke, "smoke frames and fading use one tween without frame timers"),
        check('path="res://assest/Smoke_006.png"' in smoke_scene and "hframes = 4" in smoke_scene and "vframes = 4" in smoke_scene, "smoke scene slices Smoke_006 as a 4x4 sprite sheet"),
        check("PlayCardEffect(card)" in battle and "PlayCardEffect(commandCard)" in battle, "unit and command play paths invoke play effects"),
        check(battle.count("PlayAttackEffect(to, from)") == 2 and "PlayAttackEffect(from, to)" in battle, "attack, counterattack, and ambush invoke directional effects"),
        check("if (from.ReadAttack() <= 0) return false;" in battle, "zero-attack units skip attack and counterattack visuals"),
        check("Vector2 deathCenter = GetCardCenter(deadUnit);" in battle and 'StartEffect("smoke", new List<Vector2> { deathCenter });' in battle, "destroyed units emit smoke at their captured center"),
        check("effectPool.Prewarm(resourceManager)" in battle and all(path in pool for path in ("bullet_effect.tscn", "smoke_effect.tscn", "bullet.tscn")), "battle initialization preloads and prewarms effect resources"),
        check("BulletEffectCapacity = 4" in pool and "SmokeEffectCapacity = 8" in pool and "BulletCapacity = 40" in pool, "battle pool retains effect roots and projectiles"),
        check("AcquireEffect" in effect and "Release(Effect effect)" in effect and "EffectRegistry.Release(effect)" in battle, "effect registry transparently acquires and releases pooled effects"),
        check("Random.Shared" in bullet and "Random.Shared" in controller and "new Random()" not in bullet and "new Random()" not in controller, "bullet effects use shared random generation"),
        # 单位卡分三种：卡上自己写了特效的（喀秋莎的 `bullet,sfx(katyusha_fire)`）、
        # **有兵种默认的**（坦克/火炮 → `TankAttack`，战斗机 → `strafe`，轰炸机 → `airstrike`），
        # 以及**还没配完的占位卡**（雷泽诺夫/库可夫/沃尔科夫，图标至今还是 test.png）。
        # 第三种是既有欠账、不算到本次头上，所以放行；等它们换上真图标，
        # 这条会立刻红——那正是该给它们补特效的时候。
        check(bool(units) and all(
            re.search(r"^attackEffect[ \t]*=[ \t]*\S", part, re.MULTILINE)
            or re.search(r"^cardType[ \t]*=[ \t]*(Tank|Artillery|Plane|Bomber)[ \t]*\r?$", part, re.MULTILINE)
            or "test.png" in part
            for part in units
        ), "every unit card names its own attack effect, or has a per-type default, or is a test.png placeholder"),
        # 有兵种默认的这几类**不许**再留一个光秃秃的 `bullet`：那会让它们退回机枪弹道，
        # 等于把默认动画整段绕过去。
        # 判的是 `fullmatch` 而不是 `startswith` —— 喀秋莎写的是
        # `bullet,sfx(katyusha_fire)`，那是**刻意保留**的火箭炮组合，不该被这条打中。
        check(all(not re.fullmatch(r"bullet", match.group(1).strip())
                  for part in units
                  if re.search(r"^cardType[ \t]*=[ \t]*(Tank|Artillery|Plane|Bomber)[ \t]*\r?$", part, re.MULTILINE)
                  for match in [re.search(r"^attackEffect[ \t]*=[ \t]*(.*)$", part, re.MULTILINE)] if match),
              "no Tank/Artillery/Plane/Bomber card still fires a bare machine-gun bullet effect"),
        # 判「有没有值」而不是「有没有这个键」：空的 `attackEffect =` 是无操作
        # （`ResolveAttackEffect` 拿到的还是空串，走兵种默认；指令卡没有兵种默认，等于没有）。
        # 写成「键都不许出现」的话，任何一次从别的卡复制模板都会把它弄红——那不是缺陷。
        check(bool(commands) and all(
            not re.search(r"^attackEffect[ \t]*=[ \t]*\S", part, re.MULTILINE) for part in commands
        ), "command cards have no actual attack effect"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
