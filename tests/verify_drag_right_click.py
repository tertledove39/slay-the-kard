#!/usr/bin/env python3
"""拖拽卡牌期间点右键导致卡牌卡在场上的回归校验。

`battlefield_.cs` 的 `_Input` 把 InputEventMouseButton 分成「按下」「抬起」
两个兄弟分支，两条原本都**没有校验 ButtonIndex**，于是右键会完整走一遍左键流程：

1. 左键按住手牌 -> state = caught，cardNowChoose = card，_Process 每帧跟随鼠标
2. 右键**按下** -> 误入按下分支。CheckCardClick 返回正被拖起的卡，其状态是
   caught，不匹配 inHand / placed 任何分支，于是落到 else：
   **currentInputState 被冲成 nil**（原 P_InHandUnit 丢失）
3. 右键**抬起** -> 误入抬起分支，switch (InputState.nil) 无匹配分支，
   落位/归位逻辑整段跳过
4. 稍后松开左键 -> currentInputState 仍是 nil，依然无匹配分支，
   state 永久停在 caught
5. RefreshMyHand 对 isDragging 的卡 continue 跳过（"不让自动布局移动该卡"），
   卡牌再也不会被拉回手牌位 -> **卡在场上**

附带伤害：cardNowChoose 始终非 null，_Process 因此对全部手牌关闭悬停
（UpdateHover(-9999,-9999)）。

影响范围不止手牌：场上单位拖拽（P_InPlaceUnit / inplaceAndCaught）同样因状态
不匹配任何分支而落到 else，一样会卡住。

修法是给两条分支都加上左键限定。右键在本项目中无任何既定功能，
所以拖拽期间彻底忽略右键即可，不新增取消逻辑、不动状态机。
"""
import re
from pathlib import Path
import sys

# 与项目其他测试一致：Windows 控制台默认按本地代码页输出，中文会乱码
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
BATTLEFIELD = ROOT / "bin" / "battlefield_.cs"

# 只处理滚轮的 InputEventMouseButton 处理点：外层 Pressed 天然无需左键限定，
# 因为内层仅对 WheelUp / WheelDown 生效，其余按键进去后 isWheel 保持 false。
WHEEL_ONLY_HANDLERS = {"DisplayCard.cs"}


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def cs_files():
    """全部项目内 C# 源文件（排除 addons 第三方）"""
    return [p for p in ROOT.rglob("*.cs") if "addons" not in p.parts]


def main():
    results = []

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    results.append(check(BATTLEFIELD.exists(), "bin/battlefield_.cs 存在"))
    src = BATTLEFIELD.read_text(encoding="utf-8")
    results.append(check("public override void _Input" in src,
                         "battlefield_.cs 中存在 _Input 入口"))
    results.append(check("InputEventMouseButton" in src,
                         "battlefield_.cs 中处理 InputEventMouseButton"))

    # --------------------------- 左键限定是否到位 ---------------------------
    # 注意：选择界面守卫（isShowingChoiceUI）与拖拽按下分支的守卫文本完全相同，
    # 因此不能靠字符串计数判定，必须以各自分支内部的语句为锚点向前回溯。
    print("\n--- 需求基本验证：两条分支都限定左键 ---")
    lines = src.splitlines()

    def nearest_press_guard(idx):
        """从 idx 向前找最近一条测试 mouseButton.Pressed 的行"""
        for i in range(idx, -1, -1):
            if "mouseButton.Pressed" in lines[i]:
                return i, lines[i]
        return -1, ""

    def guard_of(anchor_text):
        idx = next((i for i, l in enumerate(lines) if anchor_text in l), None)
        if idx is None:
            return None, None, None
        gi, gline = nearest_press_guard(idx)
        return idx, gi, gline

    press_anchor = "var card = CheckCardClick(mousePosition);"
    release_anchor = "没有卡牌被拖动，不处理"

    ai, gi, gline = guard_of(press_anchor)
    results.append(check(ai is not None, f"可定位拖拽按下分支锚点：{press_anchor}"))
    if ai is not None:
        results.append(check("ButtonIndex == MouseButton.Left" in gline,
                             f"拖拽按下分支限定左键（第 {gi + 1} 行）"))

    ai, gi, gline = guard_of(release_anchor)
    results.append(check(ai is not None, f"可定位拖拽抬起分支锚点：{release_anchor}"))
    if ai is not None:
        results.append(check("ButtonIndex == MouseButton.Left" in gline,
                             f"拖拽抬起分支限定左键（第 {gi + 1} 行）"))

    # --------------------------- 旧写法已绝迹 ---------------------------
    print("\n--- 定点回归：不得残留不带 ButtonIndex 的 Pressed 分支 ---")
    bare_press = re.findall(r"if\s*\(\s*mouseButton\.Pressed\s*\)", src)
    bare_release = re.findall(r"if\s*\(\s*mouseButton\.Pressed\s*==\s*false\s*\)", src)
    for m in bare_press:
        print(f"        残留无左键限定的按下分支：{m}")
    for m in bare_release:
        print(f"        残留无左键限定的抬起分支：{m}")
    results.append(check(not bare_press, "没有裸 if (mouseButton.Pressed) 写法"))
    results.append(check(not bare_release,
                         "没有裸 if (mouseButton.Pressed==false) 写法"))

    # ------------------ 边界白盒：全项目同类处理点一致性 ------------------
    print("\n--- 边界白盒：全项目 InputEventMouseButton 处理点一致性 ---")
    unguarded = []
    wheel_ok = []
    for path in cs_files():
        text = path.read_text(encoding="utf-8", errors="ignore")
        # 绑定变量名的写法：is InputEventMouseButton <var> / is not ... <var>
        for m in re.finditer(
                r"is\s+(?:not\s+)?InputEventMouseButton\s+(\w+)", text):
            var = m.group(1)
            tail = text[m.start():m.start() + 3000]
            for pm in re.finditer(rf"\b{re.escape(var)}\.Pressed\b", tail):
                line_start = tail.rfind("\n", 0, pm.start()) + 1
                line_end = tail.find("\n", pm.start())
                line = tail[line_start:line_end if line_end != -1 else len(tail)]
                if "ButtonIndex" in line:
                    continue
                if path.name in WHEEL_ONLY_HANDLERS:
                    wheel_ok.append((path.name, line.strip()))
                    continue
                unguarded.append((path.name, line.strip()))

    for name, line in unguarded:
        print(f"        未限定按键的 Pressed 判断 {name}: {line}")
    results.append(check(not unguarded,
                         "除滚轮专用处理点外，Pressed 判断都限定了 ButtonIndex"))

    # 豁免名单不是空转：必须真的是滚轮专用
    for name, line in wheel_ok:
        text = (ROOT / name).read_text(encoding="utf-8", errors="ignore")
        has_wheel = "WheelUp" in text and "WheelDown" in text
        results.append(check(has_wheel,
                             f"豁免的 {name} 确实只处理滚轮（含 WheelUp/WheelDown）"))

    # ---------------------- 忽略右键的安全性（非空转） ----------------------
    print("\n--- 前提校验：右键无既定功能，忽略它不会破坏任何东西 ---")
    right_users = []
    for path in cs_files():
        text = path.read_text(encoding="utf-8", errors="ignore")
        if "MouseButton.Right" in text:
            right_users.append(path.name)
    for name in right_users:
        print(f"        出现了右键处理：{name}")
    results.append(check(not right_users,
                         "全项目零处 MouseButton.Right，右键确实无既定功能"))

    # -------------------- 成因链仍在（证明本测试守的是真风险） --------------------
    print("\n--- 成因链校验：卡在 caught 的卡不会被布局拉回 ---")
    results.append(check("CardState.caught" in src,
                         "拖拽态 CardState.caught 仍在使用"))
    rfh = re.search(r"public void RefreshMyHand\(\)(.*?)\n    \}", src, re.S)
    results.append(check(rfh is not None, "RefreshMyHand 可定位"))
    if rfh:
        body = rfh.group(1)
        results.append(check("isDragging" in body and "continue" in body,
                             "RefreshMyHand 仍对拖拽中的卡 continue 跳过"
                             "（故 caught 卡不会自动归位）"))
    results.append(check("cardNowChoose" in src,
                         "cardNowChoose 状态机仍在（本 bug 的作用对象）"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
