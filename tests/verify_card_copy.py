#!/usr/bin/env python3
"""卡牌复制（Develop 用）的校验。

Develop 的候选卡来自牌堆/手牌/场上的真实对象，不能改动它们，也不能把原对象
直接交给选择界面与手牌，故一律先复制。复制出的卡必须满足两件事：

  1. **数值搬运完整**——cost / attack / defence / effect / traits 都要跟着走，
     不能按 id 重新读 CardData 了事（那些值在运行时被改过）。
  2. **尺寸钉死**——cardbase.tscn 的根 Control 是锚点布局，尺寸由父节点算出来。
     ShowCardChoice 会把候选卡 Reparent 到 choiceLayer（CanvasLayer）下，尺寸不钉
     死就会跟着换父节点重算，而判定「点到哪张卡」用的正是 GetGlobalRect()。

第 2 条是「复制出来的卡点不中」的直接原因，故单独立一条回归断言。
"""
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "bin" / "battlefield_.cs"
CARDBASE = ROOT / "bin" / "cardBase_.cs"


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def main():
    battle = BATTLE.read_text(encoding="utf-8")
    cardbase = CARDBASE.read_text(encoding="utf-8")
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    results.append(check(BATTLE.exists() and CARDBASE.exists(), "两个源文件均存在"))

    m = re.search(r"private cardBase_ Copy\(cardBase_ source\)(.*?)\n    \}", battle, re.S)
    results.append(check(m is not None, "Copy(cardBase_) 可定位"))
    body = m.group(1) if m else ""

    # --------------------------- 1) 数值搬运完整 ---------------------------
    print("\n--- 1) 数值搬运完整 ---")
    for field, why in [
        ("cost", "费用（可能被 setCost/subCost 改过）"),
        ("attack", "攻击（可能被 GetAttack 改过）"),
        ("defence", "防御（可能被伤害/治疗改过）"),
        ("effect", "效果串（GetEffect 会在运行时往上面追加）"),
    ]:
        results.append(check(re.search(rf"copy\.{field}\s*=\s*source\.{field}\s*;", body) is not None,
                             f"搬运 {field} —— {why}"))
    results.append(check("copy.AddTrait(source.traits)" in body,
                         "搬运 traits 用 AddTrait（同时初始化 hasAmbushActive 等运行时标志）"))
    results.append(check("copy.traits = source.traits" not in body,
                         "不直接写 traits 字段（那样会漏掉配套的运行时状态标志）"))

    # --------------------------- 2) 尺寸钉死（回归） ---------------------------
    print("\n--- 2) 尺寸钉死（本次 bug 的回归） ---")
    results.append(check("public void PinDesignSize()" in cardbase, "cardBase_ 暴露 PinDesignSize()"))
    results.append(check("SetAnchorsPreset(Control.LayoutPreset.TopLeft)" in cardbase,
                         "PinDesignSize 把锚点钉成左上角"))
    results.append(check("Size = DesignSize" in cardbase, "PinDesignSize 钉死设计尺寸"))
    results.append(check("copy.PinDesignSize();" in body,
                         "Copy() 里调了 PinDesignSize()——否则 Reparent 到 choiceLayer 后命中框会变"))
    # 尺寸常量只允许有一处配置
    sizes = [f for f in (BATTLE, CARDBASE) if re.search(r"new Vector2\(180,\s*240\)", f.read_text(encoding="utf-8"))]
    results.append(check(len(sizes) == 1 and sizes[0] == CARDBASE,
                         "180x240 只在 cardBase_.cs 配置一次（配置项单一原则）"))

    # --------------------------- 3) 建卡路径一致 ---------------------------
    print("\n--- 3) 所有建卡路径都用同一个尺寸入口 ---")
    init = re.search(r"private void InitializeDeckFromIni\(\)(.*?)\n    \}", battle, re.S)
    init_body = init.group(1) if init else ""
    results.append(check(init_body.count("PinDesignSize()") == 2,
                         "InitializeDeckFromIni 的两条分支（持久化重建 / 首次读 deck.ini）都钉了尺寸"))
    results.append(check("HandleChoiceCardClick" in battle and "GetGlobalRect().HasPoint" in battle,
                         "选择界面的命中判定确实依赖 GetGlobalRect()"))

    # --------------------------- 4) 显示同步 ---------------------------
    print("\n--- 4) 绕开 setter 后补齐显示 ---")
    results.append(check("copy.RefreshState();" in body,
                         "直接写字段后调 RefreshState()，把数值同步到三个 Label"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
