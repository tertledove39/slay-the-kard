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
    }

    // 添加固定宽度的箭头参数

[Export] private float arrowLength = 15.0f; // 箭头长度

// 添加固定宽度的箭头参数


public override void _Draw()
{
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
    DrawCurvesFill(curvePoints1, curvePoints2, Colors.Black);
    
    // 绘制边框
    DrawBezierCurve(p1, a1, ctl_1, ctl_2, 50);
    DrawBezierCurve(p1, a2, ctl_1, ctl_2, 50);
    
    // 绘制箭头
    var arrowPoints = new Vector2[] {
        b3,
        b1,
        b2
        
        
    };
    DrawColoredPolygon(arrowPoints, Colors.Black);
}
// 改进的填充方法 - 使用三角剖分处理复杂多边形
private void DrawCurvesFill(Vector2[] curve1, Vector2[] curve2, Color fillColor)
{
    // 创建多边形顶点列表
    var polygonVertices = new List<Vector2>();
    
    // 添加第一条曲线的所有点
    polygonVertices.AddRange(curve1);
    
    // 添加第二条曲线的所有点（反向）
    for (int i = curve2.Length - 1; i >= 0; i--)
    {
        polygonVertices.Add(curve2[i]);
    }
    
    // 检查多边形是否自相交
    if (IsPolygonSelfIntersecting(polygonVertices.ToArray()))
    {
        // 如果自相交，使用分段填充方法
        DrawComplexCurvesFill(curve1, curve2, fillColor);
    }
    else
    {
        // 如果没有自相交，直接绘制多边形
        DrawColoredPolygon(polygonVertices.ToArray(), fillColor);
    }
}

// 处理复杂曲线填充 - 使用三角形带方法
private void DrawComplexCurvesFill(Vector2[] curve1, Vector2[] curve2, Color fillColor)
{
    // 确保两条曲线点数相同
    int pointCount = Mathf.Min(curve1.Length, curve2.Length);
    
    // 使用三角形带填充两条曲线之间的区域
    for (int i = 0; i < pointCount - 1; i++)
    {
        // 创建两个三角形组成的四边形
        var triangle1 = new Vector2[] { curve1[i], curve2[i], curve1[i + 1] };
        var triangle2 = new Vector2[] { curve1[i + 1], curve2[i], curve2[i + 1] };
        
        // 绘制三角形
        DrawColoredPolygon(triangle1, fillColor);
        DrawColoredPolygon(triangle2, fillColor);
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
            // 跳过相邻边
            if (j == (i + 1) % n) continue;
            
            Vector2 b1 = polygon[j];
            Vector2 b2 = polygon[(j + 1) % n];
            
            // 检查边是否相交
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
    // 计算方向向量
    Vector2 r = a2 - a1;
    Vector2 s = b2 - b1;
    
    // 计算叉积
    float rxs = Cross(r, s);
    Vector2 qp = b1 - a1;
    float qpr = Cross(qp, r);
    
    // 如果线段平行或共线
    if (Mathf.Abs(rxs) < 0.0001f)
    {
        return false;
    }
    
    // 计算交点参数
    float t = Cross(qp, s) / rxs;
    float u = Cross(qp, r) / rxs;
    
    // 检查交点是否在线段上
    return (t >= 0 && t <= 1 && u >= 0 && u <= 1);
}

// 计算二维向量的叉积
private float Cross(Vector2 a, Vector2 b)
{
    return a.X * b.Y - a.Y * b.X;
}

// 备选方案：使用更简单的四边形填充方法（性能更好）
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
        
        // 绘制四边形（会自动三角化）
        DrawColoredPolygon(quad, fillColor);
    }
}




    public override void _Process(double delta)
    {
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
        
        // 绘制曲线端点与控制点的连线
        //DrawLine(p1, p1 + ctl_1 - Vector2.One, ctl_color);
        //DrawLine(p2, p2 + ctl_2 - Vector2.One, ctl_color);
        
        // 绘制贝塞尔曲线
        DrawPolyline(points, curve_color);
    }
}


