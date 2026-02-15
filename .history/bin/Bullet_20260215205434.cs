using Godot;
using System;
using System.Threading.Tasks;

public partial class Bullet : Control
{

    private Tween _currentTween;

    /// <summary>
    /// 从一点飞向另一点
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    async public Task Fly(cardBase_ from,cardBase_ to,double duration = 0.3)
    {
        if (_currentTween != null && _currentTween.IsRunning())
        {
            _currentTween.Kill(); // 立即停止
        }

        GlobalPosition = from.GlobalPosition;
        this.Visible = true;
        _currentTween = CreateTween();
        _currentTween.TweenProperty(this, "position", to.GlobalPosition, duration)
                    .SetEase(Tween.EaseType.Out) // 关键：设置为 Out，实现逐渐加速
                    .SetTrans(Tween.TransitionType.Quad); // 可选：使用二次方加速曲线，也可以是 Cubic, Back 等
        _currentTween.Finished += OnMoveFinished;
    }

    private void OnMoveFinished()
    {
        this.Visible = false;
    }
}
