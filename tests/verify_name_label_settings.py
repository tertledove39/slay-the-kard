#!/usr/bin/env python3
"""卡名的 LabelSettings 不能在外观上被"重建"（BUGS #67）。

`AdjustFontSizeToFit` 为了让每张卡持有独立的 `LabelSettings`（`.tscn` 里那份是
sub_resource，多张卡共享同一个实例，改字号会串到别的卡上），原先写的是：

    label.LabelSettings = new LabelSettings();   // ← 只设 Font + FontSize
    label.LabelSettings.Font = font;
    label.LabelSettings.FontSize = bestSize;

`new LabelSettings()` 的其余属性全是**默认值**，于是场景里配好的外观被抹掉。
实测（真起 battleField，读 name 标签的 LabelSettings）：

    场景原生    size=23 color=白 outline_size=2 outline_color=黑 line_spacing=0
    new 之后    size=2x color=白 outline_size=0 outline_color=白 line_spacing=3
    Duplicate() 之后 外观与场景原生一致，只有 FontSize 变

卡名是「白字 + 黑描边」，描边一丢，白字压在卡面深色条上就糊了。

修法是 `Duplicate()` —— 既拿到独立实例（原来的意图），又保住外观。
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def body_of(source, signature):
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


def strip_comments(text):
    return "\n".join(line.split("//")[0] for line in text.splitlines())


def main():
    code = (ROOT / "bin" / "cardBase_.cs").read_text(encoding="utf-8")
    scene = (ROOT / "bin" / "cardbase.tscn").read_text(encoding="utf-8")

    adjust = strip_comments(body_of(code, "private void AdjustFontSizeToFit(Label label)"))
    results = []

    # --- 核心：复制而不是重建 ---
    results.append(check("label.LabelSettings.Duplicate()" in adjust,
                         "用 Duplicate() 复制原有的 LabelSettings"))
    results.append(check("label.LabelSettings = new LabelSettings();" not in adjust,
                         "不再对**真正的** label 直接 new 一份（那会抹掉外观）"))
    results.append(check("settings.FontSize = bestSize;" in adjust
                         and "settings.Font = font;" in adjust,
                         "复制之后仍然设置字体与算出来的字号（自适应没被改坏）"))
    results.append(check("label.LabelSettings = settings;" in adjust,
                         "最后把副本装回标签（每卡独立，不会串到别的卡上）"))

    # --- 被保住的外观：场景里那份必须还是「白字 + 黑描边」 ---
    name_block = scene[scene.index('id="LabelSettings_dte16"'):
                       scene.index('id="LabelSettings_spis8"')]
    results.append(check("outline_size = 2" in name_block
                         and "outline_color = Color(0, 0, 0, 1)" in name_block,
                         "场景里卡名仍然配着 outline_size=2 + 黑色描边（就是要保住的东西）"))

    # --- 别修过头：一次性测量用的临时标签仍然可以新建 ---
    measure = strip_comments(body_of(code, "private int FindBestFontSizeForLabel"))
    results.append(check("tempLabel.LabelSettings = new LabelSettings();" in measure,
                         "测量的临时标签照旧 new（它只要字号，不需要外观）"))
    results.append(check("tempLabel.QueueFree()" in measure,
                         "临时标签用完即销毁，不会留在场上"))

    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
