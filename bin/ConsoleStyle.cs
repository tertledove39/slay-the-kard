using Godot;

/// <summary>
/// 调试控制台的外观，**战斗场景与世界地图共用这一份**（原先两个文件各写了一遍同样的样式）。
///
/// 需求：浅黑色、非圆角、无边框的纯色矩形。
///
/// 注意：面板与输入框**都要显式设成无边框无圆角**。Godot 默认主题给 `LineEdit` 的
/// `normal` / `focus` 样式自带圆角与描边，只把外层 `Panel` 的 StyleBox 换掉，
/// 输入框那一圈边框照样画得出来。
/// </summary>
public static class ConsoleStyle
{
    /// <summary>面板底色。浅黑色＝带一点透明的深灰，既压得住底下的战场又能看清字。</summary>
    public static readonly Color Background = new Color(0.10f, 0.10f, 0.10f, 0.72f);

    /// <summary>控制台文字颜色（输入框与输出共用）。</summary>
    public static readonly Color TextColor = Colors.LimeGreen;

    private const int InputPadding = 6;

    /// <summary>面板样式：纯色、无边框、无圆角。</summary>
    public static StyleBoxFlat CreatePanelBox()
    {
        var style = new StyleBoxFlat { BgColor = Background };
        ClearBorderAndRadius(style);
        return style;
    }

    /// <summary>输入框样式：透明底、无边框、无圆角，边框交给外层 Panel 统一画。</summary>
    public static StyleBoxFlat CreateInputBox()
    {
        var style = new StyleBoxFlat { BgColor = Colors.Transparent };
        ClearBorderAndRadius(style);
        style.ContentMarginLeft = InputPadding;
        style.ContentMarginRight = InputPadding;
        return style;
    }

    /// <summary>把统一外观套到面板与输入框上。两者可以为 null（调用方未必都建了）。</summary>
    public static void Apply(Panel panel, LineEdit input)
    {
        if (panel != null)
            panel.AddThemeStyleboxOverride("panel", CreatePanelBox());

        if (input == null)
            return;

        // normal / focus / read_only 三个状态都要覆盖，否则聚焦时默认主题的样式又会冒出来
        input.AddThemeStyleboxOverride("normal", CreateInputBox());
        input.AddThemeStyleboxOverride("focus", CreateInputBox());
        input.AddThemeStyleboxOverride("read_only", CreateInputBox());
    }

    /// <summary>边框宽度与圆角半径全部归零。</summary>
    private static void ClearBorderAndRadius(StyleBoxFlat style)
    {
        style.BorderWidthLeft = 0;
        style.BorderWidthTop = 0;
        style.BorderWidthRight = 0;
        style.BorderWidthBottom = 0;

        style.CornerRadiusTopLeft = 0;
        style.CornerRadiusTopRight = 0;
        style.CornerRadiusBottomRight = 0;
        style.CornerRadiusBottomLeft = 0;
    }
}
