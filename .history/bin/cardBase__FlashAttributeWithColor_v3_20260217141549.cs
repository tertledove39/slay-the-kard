    /// <summary>
    /// 属性数据变化时的闪烁和颜色变化效果
    /// 对于attack和defence（越大越好）：
    ///   - 等于初始值：黑色
    ///   - >= 极值：绿色
    ///   - < 极值：红色
    /// 对于cost（越小越好）：
    ///   - 等于初始值：黑色
    ///   - <= 极值：绿色
    ///   - > 极值：红色
    /// </summary>
    private async void FlashAttributeWithColor(string attributeName, int currentValue, int initialValue, int extremeValue, bool isInverted = false)
    {
        // 根据属性名获取对应的Label节点
        Label targetLabel = attributeName switch
        {
            "attack"  => GetNode<Label>("attack"),
            "defence" => GetNode<Label>("defence"),
            "cost"    => GetNode<Label>("cost"),
            _         => null
        };

        if (targetLabel == null)
            return;

        // 根据当前值、初始值和极值确定颜色
        Color targetColor;
        if (currentValue == initialValue)
        {
            targetColor = Colors.Black;  // 等于初始值 - 黑色
        }
        else if (!isInverted)
        {
            // 非反向：越大越好（attack, defence）
            if (currentValue >= extremeValue)
                targetColor = Colors.Green;  // >= 极值（最大值） - 绿色
            else
                targetColor = Colors.Red;    // < 极值 - 红色
        }
        else
        {
            // 反向：越小越好（cost）
            if (currentValue <= extremeValue)
                targetColor = Colors.Green;  // <= 极值（最小值） - 绿色
            else
                targetColor = Colors.Red;    // > 极值 - 红色
        }

        GD.Print($"闪烁 {attributeName}: 目标颜色 = {targetColor}, 当前值 = {currentValue}");

        // 保存原始字体颜色
        Color originalColor = targetLabel.GetThemeColor("font_color");

        // 先设置为目标颜色
        targetLabel.Modulate = targetColor;

        // 等待一小段时间让用户看到变化
        await Task.Delay(200);

        // 恢复原始颜色
        targetLabel.Modulate = Colors.White;

        // 再次闪烁
        await Task.Delay(200);
        targetLabel.Modulate = targetColor;
        await Task.Delay(200);
        targetLabel.Modulate = Colors.White;

        // 第三次闪烁
        await Task.Delay(200);
        targetLabel.Modulate = targetColor;
        await Task.Delay(200);
        targetLabel.Modulate = Colors.White;

        // 最后保持目标颜色
        await Task.Delay(200);
        targetLabel.Modulate = targetColor;

        GD.Print($"闪烁完成: {attributeName}");
    }
