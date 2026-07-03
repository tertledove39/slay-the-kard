using Godot;
using System;

public partial class End : CanvasLayer
{
    private ColorRect _overlay;
    private Tween _tween;

    // 单例实例（方便全局调用）
    private static End _instance;
    public static End Instance => _instance;

    public override void _Ready()
    {
        _instance = this;

        // 1. 创建黑色矩形
        _overlay = new ColorRect();
        
        // 2. 设置矩形铺满整个屏幕 (Godot 4 写法)
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.GrowHorizontal = Control.GrowDirection.Both;
        _overlay.GrowVertical = Control.GrowDirection.Both;
        
        // 3. 设置初始颜色为黑色，透明度为 0 (完全透明)
        _overlay.Color = new Color(0, 0, 0, 0);
        
        // 4. 设置鼠标过滤：
        // MouseFilterEnum.Stop = 阻挡点击 (变暗时通常不希望玩家操作)
        // MouseFilterEnum.Ignore = 穿透点击 (仅视觉变暗)
        _overlay.MouseFilter = Control.MouseFilterEnum.Stop; 
        GetNode<Sprite2D>("img").Visible = false;
        AddChild(_overlay);
    }

    /// <summary>
    /// 变暗效果
    /// </summary>
    /// <param name="duration">过渡时间 (秒)</param>
    /// <param name="alpha">最终透明度 (0-1)</param>
    public void Dim(float duration = 0.5f, float alpha = 0.7f)
    {
        this.Visible = true;
        if (_tween != null) _tween.Kill(); // 杀死旧的动画防止冲突

        _tween = CreateTween();
        // 补间动画：修改 _overlay 的 color 属性，目标 Alpha 为指定值
        _tween.TweenProperty(_overlay, "color", new Color(0, 0, 0, alpha), duration);

        var img = GetNode<Sprite2D>("img");
        img.Visible = true;
            img.Texture        = GD.Load<Texture2D>("res://assest/苏联国徽G.png");
            img.GlobalPosition = new Vector2(800, 300);
        img.Scale = new Vector2(4f, 4f);
    }

    /// <summary>
    /// 恢复明亮
    /// </summary>
    /// <param name="duration">过渡时间 (秒)</param>
    public void Brighten(float duration = 0.5f)
    {
        if (_tween != null) _tween.Kill();

        _tween = CreateTween();
        _tween.TweenProperty(_overlay, "color", new Color(0, 0, 0, 0), duration);
    }

    public void ShowSettlement(int landKilled, int airKilled, int friendlyDead, int hqDefenceLost, int pointsGained)
    {
        var viewSize = GetViewport().GetVisibleRect().Size;

        var panel = new Control();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(viewSize.X / 2 - 200, 120);
        panel.Size = new Vector2(400, 300);
        panel.ZIndex = 200;
        AddChild(panel);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(bg);

        var title = new Label();
        title.Text = "战斗结算";
        title.Position = new Vector2(0, 10);
        title.Size = new Vector2(400, 40);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", Colors.Gold);
        panel.AddChild(title);

        int y = 60;
        var lines = new[]
        {
            $"消灭敌方陆军 x{landKilled}    +{landKilled * 4}",
            $"消灭敌方空军 x{airKilled}    +{airKilled * 5}",
            $"己方单位损失 x{friendlyDead}    -{friendlyDead}",
            $"总部防御损失 {hqDefenceLost}    -{hqDefenceLost / 3}",
        };
        foreach (var line in lines)
        {
            var lbl = new Label();
            lbl.Text = line;
            lbl.Position = new Vector2(30, y);
            lbl.Size = new Vector2(340, 30);
            lbl.AddThemeFontSizeOverride("font_size", 18);
            lbl.AddThemeColorOverride("font_color", Colors.White);
            panel.AddChild(lbl);
            y += 35;
        }

        var totalLabel = new Label();
        totalLabel.Text = $"获得物资点: {pointsGained}";
        totalLabel.Position = new Vector2(30, y + 10);
        totalLabel.Size = new Vector2(340, 40);
        totalLabel.HorizontalAlignment = HorizontalAlignment.Center;
        totalLabel.AddThemeFontSizeOverride("font_size", 24);
        totalLabel.AddThemeColorOverride("font_color", Colors.Gold);
        panel.AddChild(totalLabel);
    }
}