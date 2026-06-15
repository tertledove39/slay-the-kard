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

    [Export] private float arrowLength = 15.0f; // 箭头长度
    [Export] private float minTriangleArea = 0.1f; // 最小三角形面积阈值

    public override void _Ready()
    {
        marker = GetNode<Marker2D>("Marker2D");
        p1 = marker.Position;
        ZIndex = 100;
    }

    public override void _Draw()
    {
        if (p1 == p2) return;

        // 计算主方向和垂直方向
        var mainDir = (p2 - p1).Normalized();
        var perpDir = new Vector2(-mainDir.Y, mainDir.X);
        
        // 计算箭头点
        var arrowLength = arrowWidth;  // 箭头长度是宽度的2倍
        var a1 = p2 - mainDir * arrowLength + perpDir * arrowWidth/2;
        var a2 = p2 - mainDir * arrowLength - perpDir * arrowWidth/2;
        var b1 = a1 + perpDir * arrowWidth/2;
        var b2 = a2 - perpDir * arrowWidth/2;
        var b3 = p2 + mainDir * arrowLength/3;

        // 计算控制点
        ctl_len = (p2 - p1).Length() / 2;
        
        // 获取贝塞尔曲线点
        var curvePoints1 = BezierCurve(p1, a1, ctl_1, ctl_2, 50);
        var curvePoints2 = BezierCurve(p1, a2, ctl_1, ctl_2, 50);
        
        // 绘制填充区域
        DrawSimpleCurvesFill(curvePoints1, curvePoints2, Colors.Black);
        
        // 绘制边框
        DrawBezierCurve(p1, a1, ctl_1, ctl_2, 20);
        DrawBezierCurve(p1, a2, ctl_1, ctl_2, 20);
        
        // 绘制箭头
        var arrowPoints = new Vector2[] {
            b3,
            b1,
            b2
        };
        
        // 验证箭头多边形
        if (IsValidPolygon(arrowPoints))
        {
            DrawColoredPolygon(arrowPoints, Colors.Black);
        }
    }

    // 改进的填充方法 - 添加三角形验证
    private void DrawSimpleCurvesFill(Vector2[] curve1, Vector2[] curve2, Color fillColor)
    {
        // 确保两条曲线的点数相同
        int pointCount = Mathf.Min(curve1.Length, curve2.Length);
        
        // 使用三角形带方法
        for (int i = 0; i < pointCount - 1; i++)
        {
            // 创建第一个三角形
            var triangle1 = new Vector2[] {
                curve1[i],      // 上边的点
                curve2[i],      // 下边的点
                curve1[i + 1]   // 上边的下一个点
            };
            
            // 验证并绘制三角形
            if (IsValidTriangle(triangle1))
            {
                DrawColoredPolygon(triangle1, fillColor);
            }
            
            // 创建第二个三角形
            var triangle2 = new Vector2[] {
                curve1[i + 1],  // 上边的下一个点
                curve2[i],      // 下边的点
                curve2[i + 1]   // 下边的下一个点
            };
            
            // 验证并绘制三角形
            if (IsValidTriangle(triangle2))
            {
                DrawColoredPolygon(triangle2, fillColor);
            }
        }
    }

    // 验证三角形是否有效（非退化）
    private bool IsValidTriangle(Vector2[] triangle)
    {
        if (triangle.Length != 3) return false;
        
        // 检查点是否过于接近
        float minDistance = 0.5f; // 最小距离阈值
        if (triangle[0].DistanceTo(triangle[1]) < minDistance ||
            triangle[1].DistanceTo(triangle[2]) < minDistance ||
            triangle[2].DistanceTo(triangle[0]) < minDistance)
        {
            return false;
        }
        
        // 计算三角形面积（使用叉积）
        float area = Mathf.Abs(GetTriangleArea(triangle[0], triangle[1], triangle[2]));
        
        // 如果面积太小，认为是退化三角形
        return area >= minTriangleArea;
    }

    // 验证多边形是否有效
    private bool IsValidPolygon(Vector2[] polygon)
    {
        if (polygon.Length < 3) return false;
        
        // 检查点是否过于接近
        for (int i = 0; i < polygon.Length; i++)
        {
            int next = (i + 1) % polygon.Length;
            if (polygon[i].DistanceTo(polygon[next]) < 0.5f)
            {
                return false;
            }
        }
        
        // 计算多边形面积
        float area = GetPolygonArea(polygon);
        return Mathf.Abs(area) >= minTriangleArea;
    }

    // 计算三角形面积
    private float GetTriangleArea(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return 0.5f * ((p2.X - p1.X) * (p3.Y - p1.Y) - (p3.X - p1.X) * (p2.Y - p1.Y));
    }

    // 计算多边形面积
    private float GetPolygonArea(Vector2[] polygon)
    {
        float area = 0;
        int n = polygon.Length;
        
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += polygon[i].X * polygon[j].Y;
            area -= polygon[j].X * polygon[i].Y;
        }
        
        return area / 2.0f;
    }

    // 处理复杂曲线填充 - 使用三角形带方法
    private void DrawComplexCurvesFill(Vector2[] curve1, Vector2[] curve2, Color fillColor)
    {
        int pointCount = Mathf.Min(curve1.Length, curve2.Length);
        
        for (int i = 0; i < pointCount - 1; i++)
        {
            var triangle1 = new Vector2[] { curve1[i], curve2[i], curve1[i + 1] };
            var triangle2 = new Vector2[] { curve1[i + 1], curve2[i], curve2[i + 1] };
            
            if (IsValidTriangle(triangle1))
            {
                DrawColoredPolygon(triangle1, fillColor);
            }
            
            if (IsValidTriangle(triangle2))
            {
                DrawColoredPolygon(triangle2, fillColor);
            }
        }
    }

    // 检查多边形是否自相交的简单方法
    private bool IsPolygonSelfIntersecting(Vector2[] polygon)
    {
        int n = polygon.Length;
        
        for (int i = 0; i < n; i++)
        {
            Vector2 a1 = polygon[i];
            Vector2 a2 = polygon[(i + 1) % n];
            
            for (int j = i + 2; j < n; j++)
            {
                if (j == (i + 1) % n) continue;
                
                Vector2 b1 = polygon[j];
                Vector2 b2 = polygon[(j + 1) % n];
                
                if (DoLinesIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    // 检查两条线段是否相交
    private bool DoLinesIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        Vector2 r = a2 - a1;
        Vector2 s = b2 - b1;
        
        float rxs = Cross(r, s);
        Vector2 qp = b1 - a1;
        
        if (Mathf.Abs(rxs) < 0.0001f)
        {
            return false;
        }
        
        float t = Cross(qp, s) / rxs;
        float u = Cross(qp, r) / rxs;
        
        return (t >= 0 && t <= 1 && u >= 0 && u <= 1);
    }

    // 计算二维向量的叉积
    private float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    public override void _Process(double delta)
    {
        p1 = marker.Position;
        p2 = ToLocal(GetGlobalMousePosition());
        var ang = (p2 - p1).Angle();
        ctl_len = (p2 - p1).Length() / 3.0f;
        ctl_1 = PVector2(-90, ctl_len);
        ctl_2 = PVector2(-90 + 2 * Mathf.RadToDeg(ang), ctl_len);
        QueueRedraw();
    }

    private Vector2 PVector2(float angle = 0f, float length = 0f)
    {
        var dir = Vector2.Right.Rotated(Mathf.DegToRad(angle));
        return dir * length;
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
        
        // 绘制控制点
        DrawArc(p1 + ctl_1, 2, 0, Mathf.Tau, 10, ctl_color);
        DrawArc(p2 + ctl_2, 2, 0, Mathf.Tau, 10, ctl_color);
        
        // 绘制贝塞尔曲线
        DrawPolyline(points, curve_color);
    }
}