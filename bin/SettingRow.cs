using Godot;

/// <summary>
/// 设置项的**行**——一个名字 + 一根滑条 + 右侧数值。
///
/// 抽出来是因为**两个界面都要用**：设置界面（`SettingsMenu`）与暂停菜单（`PauseMenu`）。
/// 原先这套构建写在 `SettingsMenu.AddSlider` 里而且是 private，暂停菜单要用就只能再抄一遍
/// ——那就是第二份滑块实现，改一处漏一处（规范 A / H）。
///
/// 只处理 `float` 类型的设置项：`bool` 是 `CheckButton`，目前只有设置界面用得上，
/// 留在 `SettingsMenu` 里。改完走 `SettingsManager.SetFloat`——它**立即应用并落盘**，
/// 所以这里不用再管「保存设置」这件事。
/// </summary>
public static class SettingRow
{
    /// <summary>行高。与设置界面一致。</summary>
    public const int RowHeight = 48;

    /// <summary>往 `list` 里追加一行：`名称 | 滑条 | 数值`。</summary>
    public static void AddFloatSlider(VBoxContainer list, SettingItem item)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(540, RowHeight) };

        var name = new Label { Text = item.Name, CustomMinimumSize = new Vector2(130, 0) };
        name.AddThemeFontSizeOverride("font_size", 24);

        var slider = new HSlider
        {
            MinValue = item.MinValue,
            MaxValue = item.MaxValue,
            Step = item.Step,
            Value = SettingsManager.GetFloat(item.Key),
            CustomMinimumSize = new Vector2(330, RowHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var valueLabel = new Label { Text = FormatValue(slider.Value), CustomMinimumSize = new Vector2(70, 0) };
        valueLabel.AddThemeFontSizeOverride("font_size", 22);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;

        slider.ValueChanged += value =>
        {
            valueLabel.Text = FormatValue(value);
            SettingsManager.SetFloat(item.Key, (float)value);
        };

        row.AddChild(name);
        row.AddChild(slider);
        row.AddChild(valueLabel);
        list.AddChild(row);
    }

    /// <summary>整数就不显示小数点，否则最多两位。</summary>
    public static string FormatValue(double value) =>
        Mathf.IsEqualApprox((float)value, Mathf.Round((float)value))
            ? Mathf.RoundToInt((float)value).ToString()
            : value.ToString("0.##");
}
