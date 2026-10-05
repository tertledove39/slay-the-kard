using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Bullet : Effect
{

    private Tween _currentTween;

    /// <summary>
    /// 飞行的缓动曲线。默认 `InOut`（起步慢、中间快、**收尾减速**），
    /// 这是子弹从前的表现，没动。
    ///
    /// 航弹要的是 `In`（一直加速、**到最快那一瞬间消失**，不做减速收尾）——
    /// 所以它是 Export，由 `bin/bomb.tscn` 单独设，而不是在这里改死。
    /// </summary>
    [Export] public Tween.EaseType FlightEase = Tween.EaseType.InOut;

    /// <summary>
    /// 从一点飞向另一点
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    // source / count 这个弹体用不上：它只关心从哪飞到哪。
    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                                    cardBase_ source = null, int count = 0)
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
                    .SetEase(FlightEase)
                    .SetTrans(Tween.TransitionType.Cubic); // 可选：使用二次方加速曲线，也可以是 Cubic, Back 等
        // 缓动曲线为 In（航弹）时，**最快的那一刻正好是 tween 结束的那一刻**，
        // 所以下面这一句就是「速度最大时直接消失」。
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
