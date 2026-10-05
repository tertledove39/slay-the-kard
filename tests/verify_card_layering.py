#!/usr/bin/env python3
"""卡牌层级（CanvasItem.ZIndex）约定。

实机反馈「起飞的单位不应该挡在手牌上面」时，查出来的东西比预想多：
拾取用的数字（10 / 15 / 20 / 30 / 40 / 100）散在四个文件里各写各的，
其中 `_discardZCounter = 50` 是**确实**会压住手牌的——弃牌动画整段盖在玩家
正要点的牌上面。这个脚本把整条链子钉住，让下一个改层级的人知道上下界在哪。

约定的完整顺序（数字小的在下）：

    场上卡 10 < 阵亡/弃置动画 11..19 < 抬起的卡 12 < 手牌 20 < 手牌悬停 30
        < 选项 UI 40 < 拖拽中的卡 100 < 控制台/顶层 UI 1000

不变式只有一条：**除了拖拽与选项 UI，任何"临时浮起来"的卡都必须低于手牌。**
手牌是玩家随时要点的东西，被盖住就没法操作了。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"
FLYING = ROOT / "core_logic" / "FlyingEffect.cs"
NOTICE = ROOT / "docs" / "NOTICE.md"

HAND_Z = 20          # 手牌
FIELD_Z = 10         # 场上的普通卡


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def code_only(cs_text):
    """去掉整行注释。搜「某个写法已经不存在」之前必须剥掉注释——
    说明它被改掉的那段文档本身就会把旧写法写出来。"""
    return "\n".join(line for line in cs_text.split("\n") if not line.strip().startswith("//"))


def main():
    battle = BATTLE.read_text(encoding="utf-8")
    battle_code = code_only(battle)
    flying = FLYING.read_text(encoding="utf-8")
    results = []

    # ==================== 冒烟：层级数字确实存在 ====================
    print("--- 冒烟：层级基数 ---")
    results.append(check("handCards[i].ZIndex = (i == hoveredIdx) ? 30 : 20;".replace("  ", " ")
                         in battle.replace("  ", " "),
                         "手牌是 20、悬停的 30（这条是整条链子的锚点）"))

    # ==================== ① 抬起的卡必须低于手牌 ====================
    print("\n--- ① 飞掠抬起的卡 ---")
    match = re.search(r"\[Export\] public int TopZIndex = (\d+);", flying)
    results.append(check(match is not None, "TopZIndex 是 Export（调手感不必改代码）"))
    if match:
        top = int(match.group(1))
        results.append(check(top < HAND_Z,
                             f"抬起卡的层级 {top} < 手牌 {HAND_Z}（**绝不能盖住手牌**）"))
        results.append(check(top > FIELD_Z,
                             f"抬起卡的层级 {top} > 场上卡 {FIELD_Z}（要能压住旁边的卡）"))
    results.append(check("source.ZIndex = TopZIndex;" in flying, "播放期间真的应用了这个层级"))
    results.append(check("source.ZIndex = baseZIndex;" in flying, "播完还原回原来的层级"))

    # ==================== ② 阵亡/弃置动画必须低于手牌 ====================
    print("\n--- ② 阵亡/弃置动画的层级 ---")
    results.append(check("_discardZCounter = 50" not in battle_code,
                         "弃置层级不再从 50 起（50 是压在手牌 20 上面的）"))
    base = re.search(r"private const int DiscardZBase = (\d+);", battle_code)
    cap = re.search(r"private const int DiscardZMax = (\d+);", battle_code)
    results.append(check(base is not None and cap is not None,
                         "弃置层级区间用常量写出来，不再散落成魔数"))
    if base and cap:
        lo, hi = int(base.group(1)), int(cap.group(1))
        results.append(check(lo > FIELD_Z, f"弃置层级下限 {lo} > 场上卡 {FIELD_Z}"))
        results.append(check(hi < HAND_Z, f"弃置层级上限 {hi} < 手牌 {HAND_Z}（关键那条）"))

    # 递增必须封顶，否则弃到第 10 张就又爬到手牌上面去了
    results.append(check("private int NextDiscardZIndex() => Math.Min(_discardZCounter++, DiscardZMax);" in battle,
                         "取层级时封顶（弃到第 10 张也不会爬到手牌上面）"))
    results.append(check("private void ResetDiscardZCounter() => _discardZCounter = DiscardZBase;" in battle,
                         "每批弃置前重置，批内顺序从下往上"))
    results.append(check(battle.count("NextDiscardZIndex()") >= 3,
                         "两条弃置路径都走同一个取号函数（不各写各的）"))
    results.append(check("card.ZIndex = _discardZCounter++;" not in battle,
                         "没有残留的直接自增（那样会绕过封顶）"))
    results.append(check("ResetDiscardZCounter();" in battle,
                         "DiscardUnitsWithStagger 每批重置一次"))

    # ==================== ③ 约定写进了文档 ====================
    print("\n--- ③ 约定有文档可查 ---")
    notice = NOTICE.read_text(encoding="utf-8")
    results.append(check("卡牌层级" in notice and "卡牌层级（`ZIndex`）约定" in notice,
                         "NOTICE.md 里记着整条层级链（下一个改层级的人能查到上下界）"))
    if "卡牌层级（`ZIndex`）约定" in notice:
        section = notice.split("卡牌层级（`ZIndex`）约定", 1)[1]
        results.append(check("都必须低于手牌" in section, "文档里写明了那条不变式"))
        results.append(check("TopZIndex = 200" in section,
                             "文档提醒「场景值优先于 C# 默认值」这个坑"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
