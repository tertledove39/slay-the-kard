using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Bullet : Effect
{

    private Tween _currentTween;

    /// <summary>
    /// 从一点飞向另一点
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null)
    {
        if (positions == null || positions.Count < 2) return;
        Vector2 from = positions[0];
        Vector2 to = positions[1];
        float duration = time ?? 0.3f;
        if (_currentTween != null && _currentTween.IsRunning())
        {
            _currentTween.Kill(); // 立即停止
        }
        ZIndex = 90;
        Rotation = (to - from).Angle() + (float)Math.PI/2;
        GlobalPosition = from + GetOffset(30);
        this.Visible = true;
        _currentTween = CreateTween();
        _currentTween.TweenProperty(this, "global_position", to + GetOffset(60), duration)
                    //.SetEase(Tween.EaseType.In) // 关键：设置为 Out，实现逐渐加速
                    .SetTrans(Tween.TransitionType.Cubic); // 可选：使用二次方加速曲线，也可以是 Cubic, Back 等
        await ToSignal(_currentTween, Tween.SignalName.Finished);
        OnMoveFinished();
    }

    public override void PrepareForUse()
    {
        Visible = false;
        Rotation = 0f;
    }

    public override void ResetForPool()
    {
        _currentTween?.Kill();
        _currentTween = null;
        Visible = false;
        Rotation = 0f;
    }

    static Vector2 GetOffset(float i)
    {
        return new Vector2((Random.Shared.NextSingle() - 0.5f)*2*i,(Random.Shared.NextSingle() - 0.5f)*2*i);
    }

    private void OnMoveFinished()
    {
        this.Visible = false;
    }
}
