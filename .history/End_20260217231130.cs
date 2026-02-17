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
        GetNode<Sprite2D>("img").Texture = GD.Load<Texture2D>("res://assest/苏联国徽G.png");
    }

    /// <summary>
    /// 恢复明亮
    /// </summary>
    /// <param name="duration">过渡时间 (秒)</param>
    public void Brighten(float duration = 0.5f)
    {
        if (_tween != null) _tween.Kill();

        _tween = CreateTween();
        // 目标 Alpha 为 0 (完全透明)
        _tween.TweenProperty(_overlay, "color", new Color(0, 0, 0, 0), duration);
    }
}