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
        check(bool(units) and all("attackEffect = bullet" in part for part in units), "existing unit cards use the bullet attack effect"),
        check(bool(commands) and all("attackEffect" not in part for part in commands), "cards without attack effects remain effect-free"),
    ]
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
