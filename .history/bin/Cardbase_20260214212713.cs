using Godot;
using System;
using System.Collections.Generic;

public partial class Cardbase : Node2D
{
    private Marker2D marker;
    private Vector2 p1;       // 起点
    private Vector2 p2;       // 终点
    private float ctl_len = 0;
    private Vector2 ctl_1;
    private Vector2 ctl_2;
    private int steps = 500;
    private Color curve_color = Colors.Black;
    private Color ctl_color = Colors.Black;
    private Color curveColor = Colors.Black;
    private float curveWidth = 1.0f;
    private float arrowWidth = 60.0f;  // 固定的箭头宽度

    public override void _Ready()
    {
        marker = GetNode<Marker2D>("Marker2D");
        p1 = marker.Position;
        ZIndex = 100;
    }

    [Export] private float arrowLength = 15.0f; // 箭头长度

    public override void _Draw()
    {
        if (p1 == p2) return; // 避免起点和终点相同的情况

        // 计算主方向和垂直方向
        var mainDir = (p2 - p1).Normalized();
        var perpDir = new Vector2(-mainDir.Y, mainDir.X);
        
        // 计算箭头顶点
        var arrowTip = p2;
        var arrowBase = p2 - mainDir * arrowLength;
        var arrowLeft = arrowBase + perpDir * arrowWidth/2;
        var arrowRight = arrowBase - perpDir * arrowWidth/2;
        
        // 计算控制点
        ctl_len = (p2 - p1).Length() / 3;
        var midPoint = (p1 + p2) / 2;
        ctl_1 = midPoint + perpDir * ctl_len;
        ctl_2 = midPoint - perpDir * ctl_len;
        
        // 获取贝塞尔曲线点
        var curvePoints1 = BezierCurve(p1, arrowLeft, ctl_1, ctl_2, 20);
        var curvePoints2 = BezierCurve(p1, arrowRight, ctl_1, ctl_2, 20);
        
        // 绘制填充区域
        DrawSimpleCurvesFill(curvePoints1, curvePoints2, Colors.Black);
        
        // 绘制边框
        DrawBezierCurve(p1, arrowLeft, ctl_1, ctl_2, 20);
        DrawBezierCurve(p1, arrowRight, ctl_1, ctl_2, 20);
        
        // 绘制箭头
        var arrowPoints = new Vector2[] { 
            arrowTip,
            arrowLeft,
            arrowRight
        };
        DrawColoredPolygon(arrowPoints, Colors.Black);
    }

    // 使用更简单的四边形填充方法
    private void DrawSimpleCurvesFill(Vector2[] curve1, Vector2[] curve2, Color fillColor)
    {
        int segments = Mathf.Min(curve1.Length, curve2.Length) - 1;
        
        for (int i = 0; i < segments; i++)
        {
            // 创建四边形
            var quad = new Vector2[] {
                curve1[i],
                curve1[i + 1],
                curve2[i + 1],
                curve2[i]
            };
            
            // 绘制四边形
            DrawColoredPolygon(quad, fillColor);
        }
    }

    public override void _Process(double delta)
    {
        p1 = marker.Position;
        p2 = ToLocal(GetGlobalMousePosition());
        
        // 计算控制点
        var dir = (p2 - p1).Normalized();
        var perpDir = new Vector2(-dir.Y, dir.X);
        var midPoint = (p1 + p2) / 2;
        ctl_len = (p2 - p1).Length() / 3;
        ctl_1 = midPoint + perpDir * ctl_len;
        ctl_2 = midPoint - perpDir * ctl_len;
        
        QueueRedraw();
    }

    private Vector2[] BezierCurve(Vector2 p1, Vector2 p2, Vector2 ctl_1, Vector2 ctl_2, int pointsCount = 10)
    {
        var points = new Vector2[pointsCount + 1];
        for (int i = 0; i <= pointsCount; i++)
        {
            var t = (float)i / pointsCount;
            points[i] = p1.BezierInterpolate(p1 + ctl_1, p2 + ctl_2, p2, t);
        }
        return points;
    }

    private void DrawBezierCurve(Vector2 p1, Vector2 p2, Vector2 ctl_1, Vector2 ctl_2, int pointsCount)
    {
        var points = BezierCurve(p1, p2, ctl_1, ctl_2, pointsCount);
        DrawPolyline(points, curve_color);
    }
}
