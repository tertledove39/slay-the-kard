#!/usr/bin/env python3
"""`convert(id)` 效果指令：把目标原地转换成 id 所指代的单位，并播放翻面动画。

需求：
  · 新增一个 `convert(id)` key，目标被转换后变成 id 指代的单位
  · 转换会播放动画：1) 轻微浮起（类似战斗机起飞）2) 翻面（露出卡背）
    3) 再次翻面回来时已经是新单位
  · 过程会露出卡背，用新加的两张卡背素材

几条**结构性**的约定，测试守的就是它们：
  · 动画继承 `FlyingEffect`、**只覆写悬停那一段** —— 浮起/落回/还原/标脏全部沿用父类，
    与 `AirStrikeEffect` 覆写起飞阶段是同一个套路（规范 A）
  · **翻面不引入 shader**：把 `Scale.X` 走 1→0→1 就是绕竖轴翻转，`X=0` 那一刻换内容
  · 新单位的 id 走**特效名的参数**（`convert(panzer4)`），与 `sfx(严冬)` 同一套，
    不额外造一种传参方式
  · 指令**必须 await 动画**：换卡发生在第二次翻面那一刻，不 await 的话
    `convert(x)|GetAttack(2)` 会把 +2 加在旧卡上，静默错
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
CONVERT = ROOT / "core_logic" / "ConvertEffect.cs"
CONVERT_SCENE = ROOT / "effects" / "convert_effect.tscn"
EFFECT = ROOT / "core_logic" / "Effect.cs"
FLYING = ROOT / "core_logic" / "FlyingEffect.cs"
CARD_BASE = ROOT / "bin" / "cardBase_.cs"
CARD_SCENE = ROOT / "bin" / "cardbase.tscn"
BATTLE = ROOT / "bin" / "battlefield_.cs"
TIMES = ROOT / "bin" / "timesList.ini"

SOVIET = ROOT / "assest" / "苏联卡背.png"
GERMAN = ROOT / "assest" / "德国卡背.png"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def method(text, start, end):
    i = text.index(start)
    return text[i:text.index(end, i)]


def main():
    results = []
    convert = CONVERT.read_text(encoding="utf-8")
    scene = CONVERT_SCENE.read_text(encoding="utf-8")
    effect = EFFECT.read_text(encoding="utf-8")
    card_base = CARD_BASE.read_text(encoding="utf-8")
    card_scene = CARD_SCENE.read_text(encoding="utf-8")
    battle = BATTLE.read_text(encoding="utf-8")
    times = TIMES.read_text(encoding="utf-8")

    # ==================== ① 冒烟 ====================
    print("--- ① 冒烟：文件与注册 ---")
    results.append(check(CONVERT.exists(), "core_logic/ConvertEffect.cs 存在"))
    results.append(check(CONVERT_SCENE.exists(), "effects/convert_effect.tscn 存在"))
    results.append(check('["convert"] = "res://effects/convert_effect.tscn"' in effect,
                         "注册表登记了 convert"))
    for path, name in ((SOVIET, "苏联卡背"), (GERMAN, "德国卡背")):
        results.append(check(path.exists() and Path(str(path) + ".import").exists(),
                             f"{name} 存在且已被 Godot 导入"))

    # ==================== ② 动画：继承 + 只覆写悬停 ====================
    print("\n--- ② 动画：继承 FlyingEffect，只覆写悬停 ---")
    results.append(check("class ConvertEffect : FlyingEffect" in convert,
                         "继承 FlyingEffect（浮起/落回/还原只有一份实现）"))
    results.append(check("protected override async Task StayAsync(" in convert,
                         "只覆写悬停那一段"))
    code = "\n".join(l for l in convert.split("\n") if not l.strip().startswith("//"))
    for gone in ("private async Task RiseAsync(", "private async Task LandAsync(",
                 "private async Task SwayAsync("):
        results.append(check(gone not in code,
                             f"没有把 {gone.split()[-1].rstrip('(')} 抄一份过来"))
    results.append(check("base.StayAsync" not in code,
                         "悬停整段换成翻面，没有再去调父类的摆动实现"))
    results.append(check('RiseHeight = 30.0' in scene and 'RiseScale = 1.04' in scene,
                         "浮起高度/放大在场景里配（「轻微浮起」比攻击用的飞掠矮）"))

    # ==================== ③ 翻面：压扁 X 轴，不引 shader ====================
    print("\n--- ③ 翻面用 Scale.X，不引 shader ---")
    results.append(check("card.Scale = new Vector2(x, card.Scale.Y)" in convert,
                         "翻面只动 Scale.X，Y 保持飞掠抬起后的值"))
    results.append(check("TweenMethod" in convert and "HalfFlipSeconds" in convert,
                         "半翻是逐帧插值出来的连续过程（不是瞬间跳变）"))
    # 先剥注释：本文件的 `///` 里写着「**翻面不用 shader**」——那是说明，
    # 不剥掉的话这条断言会被自己的文档判成违规（同 verify_attack_effects 的 code_only）。
    convert_code = "\n".join(l for l in convert.split("\n") if not l.strip().startswith("//"))
    results.append(check(not re.search(r"ShaderMaterial|\.gdshader", convert_code, re.IGNORECASE)
                         and not re.search(r"\.gdshader", scene, re.IGNORECASE),
                         "没有引入着色器资源（翻面靠压扁 Scale.X）"))
    # 那两个「在压扁到 0 的那一刻」的钩子必须真的挂在压扁之后
    stay = method(convert, "protected override async Task StayAsync(", "\n    /// <summary>")
    results.append(check(stay.index("toZero: true, onEdge: () => card.SetConvertBackVisible(true)")
                         < stay.index("toZero: true, onEdge: () => ApplyNewUnit(card)"),
                         "顺序：先翻面露卡背 → 停一拍 → 再翻面换卡"))
    results.append(check("HalfFlipSeconds" in scene and "BackHoldSeconds" in scene,
                         "半翻时长与停顿时长都在场景里配"))

    # ==================== ④ 卡背：盖住整张卡，按阵营选 ====================
    print("\n--- ④ 卡背 ---")
    results.append(check('name="convertBack" type="TextureRect"' in card_scene,
                         "cardbase.tscn 里有卡背层（UI 写在场景里）"))
    results.append(check("visible = false" in card_scene.split('name="convertBack"')[1][:200],
                         "默认隐藏"))
    results.append(check("stretch_mode = 6" in card_scene.split('name="convertBack"')[1][:200]
                         and "expand_mode = 1" in card_scene.split('name="convertBack"')[1][:200],
                         "KeepAspectCovered + IgnoreSize（铺满卡牌、不变形；枚举值已用 Godot 实测过）"))
    results.append(check("offset_right = 180.0" in card_scene.split('name="convertBack"')[1][:260]
                         and "offset_bottom = 240.0" in card_scene.split('name="convertBack"')[1][:260],
                         "尺寸正好是卡牌的 180x240"))
    results.append(check('SovietCardBackPath = "res://assest/苏联卡背.png"' in card_base
                         and 'GermanCardBackPath = "res://assest/德国卡背.png"' in card_base,
                         "两张卡背的路径是常量"))
    set_back = method(card_base, "public void SetConvertBackVisible(bool visible)", "\n    }")
    results.append(check("GetIsFriend() == IsFriend.friend ? SovietCardBackPath : GermanCardBackPath"
                         in set_back,
                         "按**这张卡属于哪一方**选卡背（我方苏联、敌方德国）"))
    results.append(check("back.Texture != texture" in set_back,
                         "每次都重新选图 —— 卡是从对象池复用的，上一轮可能属于另一方"))
    results.append(check("GetNodeOrNull<TextureRect>(\"convertBack\")" in set_back,
                         "取节点用的是 GetNodeOrNull（老场景没有这层时不炸）"))

    # ==================== ⑤ 指令：convert(id) ====================
    print("\n--- ⑤ convert(id) 指令 ---")
    results.append(check("convert(id)" in times and "原地转换" in times,
                         "timesList.ini 的 [keys] 里记了 convert(id)"))
    results.append(check('"convert()"' in battle, "ConsoleCommands 里加了它（控制台 Tab 补全）"))
    branch = method(battle, 'if (instruction.StartsWith("convert"', "if (instruction.StartsWith(\"GetAttack\"")
    results.append(check("Regex.Match(instruction" in branch and r"\(([^)]*)\)" in branch,
                         "按 Heal(n) 的同一套写法取括号里的参数"))
    results.append(check("foreach (var target in targets)" in branch,
                         "对 targets 批量作用（与 Heal/damage 一致，不是只认单一目标）"))
    results.append(check("await ConvertCardAsync(target, newCardId);" in branch,
                         "**await** 动画（不 await 会让后续指令作用在旧卡上）"))
    convert_fn = method(battle, "private Task ConvertCardAsync(", "\n    }")
    results.append(check("EffectRegistry.PlayOnceAsync(this, ConvertEffectName, newCardId," in convert_fn,
                         "走共用的 PlayOnceAsync（不是自己再写一遍取/挂/播/还）"))
    results.append(check("StartEffect(" not in convert_fn,
                         "没有用 fire-and-forget 的 StartEffect"))
    results.append(check("if (card.isHq == HQ.hq)" in convert_fn,
                         "总部不能被转换（当场拒绝并报警）"))

    # ==================== ⑥ 换卡本身 ====================
    print("\n--- ⑥ 换的是什么 ---")
    apply_fn = method(convert, "private void ApplyNewUnit(cardBase_ card)", "\n    }")
    results.append(check("BattleStateManager.GetCachedCard(newCardId)" in apply_fn,
                         "按 id 取卡数据"))
    results.append(check("card.SetCardInformation(data);" in apply_fn,
                         "换的是**同一张卡**（同一节点/格子/阵营，只换数据）"))
    results.append(check("data.IsHq == HQ.hq" in apply_fn,
                         "新卡是总部时拒绝"))
    results.append(check("data == null" in apply_fn, "id 不存在时只报警、卡保持原样"))
    results.append(check("card.SetConvertBackVisible(false);" in apply_fn
                         and apply_fn.index("SetConvertBackVisible(false)") < apply_fn.index("return;"),
                         "换卡那一刻先收起卡背（收在 return 之前，坏输入也不会把卡背留在场上）"))
    results.append(check("HaveAttacked\|RefreshUnit\|moveAble\|attackAble" not in apply_fn.replace("HaveAttacked", ""),
                         "不动行动次数（转换不是部署，不白送一次攻击）"))

    # ==================== ⑦ 共用件没有被clone ====================
    print("\n--- ⑦ 只有一份「播一个特效」 ---")
    results.append(check("public static async Task PlayOnceAsync(Node host" in effect,
                         "PlayOnceAsync 在 EffectRegistry 上"))
    results.append(check("=> EffectRegistry.PlayOnceAsync(this, effectName, null, positions, null, count);"
                         in effect,
                         "Effect.PlayChildEffectAsync 转调它（不再自己写一遍）"))
    results.append(check("EffectRegistry.Create(effectName)" not in method(
        effect, "protected Task PlayChildEffectAsync(", "\n    }"),
        "PlayChildEffectAsync 里没有第二份取/播/还"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
