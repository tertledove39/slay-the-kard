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
    private const int CurveSegments = 24;
    private readonly Vector2[] centerCurve = new Vector2[CurveSegments + 1];
    private readonly Vector2[] arrowHead = new Vector2[3];

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
        if (p1.DistanceSquaredTo(p2) < 4.0f) return;

        // 计算主方向和垂直方向
        var mainDir = (p2 - p1).Normalized();
        var perpDir = new Vector2(-mainDir.Y, mainDir.X);
        
        // 计算箭头点
        var arrowLength = arrowWidth;  // 箭头长度是宽度的2倍
        var arrowBase = p2 - mainDir * arrowLength;
        var trailEnd = arrowBase + mainDir * (arrowWidth / 2.0f);
        var b1 = arrowBase + perpDir * arrowWidth;
        var b2 = arrowBase - perpDir * arrowWidth;
        var b3 = p2 + mainDir * arrowLength/3;

        // 计算控制点
        ctl_len = (p2 - p1).Length() / 2;
        
        // 获取贝塞尔曲线点
        FillBezierCurve(centerCurve, p1, trailEnd, ctl_1, ctl_2);
        DrawPolyline(centerCurve, Colors.Black, arrowWidth, true);
        
        // 绘制箭头
        arrowHead[0] = b3;
        arrowHead[1] = b1;
        arrowHead[2] = b2;
        
        // 验证箭头多边形
        if (IsValidPolygon(arrowHead))
        {
            DrawColoredPolygon(arrowHead, Colors.Black);
        }
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

    public override void _Process(double delta)
    {
        if (!Visible) return;
        p1 = marker.Position;
        Vector2 nextP2 = ToLocal(GetGlobalMousePosition());
        if (nextP2.IsEqualApprox(p2)) return;
        p2 = nextP2;
        var ang = (p2 - p1).Angle();
        ctl_len = (p2 - p1).Length() / 3.0f;
        ctl_1 = PVector2(-90, ctl_len);
        ctl_2 = PVector2(-90 + 2 * Mathf.RadToDeg(ang), ctl_len);
        QueueRedraw();
    }

    private static void FillBezierCurve(Vector2[] points, Vector2 start, Vector2 end, Vector2 control1, Vector2 control2)
    {
        int segments = points.Length - 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            points[i] = start.BezierInterpolate(start + control1, end + control2, end, t);
        }
    }

    private Vector2 PVector2(float angle = 0f, float length = 0f)
    {
        var dir = Vector2.Right.Rotated(Mathf.DegToRad(angle));
        return dir * length;
    }

}
