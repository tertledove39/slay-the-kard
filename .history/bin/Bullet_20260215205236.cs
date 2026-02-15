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
    async public Task Fly(cardBase_ from,cardBase_ to,double duration = 0.5)
    {
        if (_currentTween != null && _currentTween.IsRunning())
        {
            _currentTween.Kill(); // 立即停止
        }

        GlobalPosition = from.GlobalPosition;
        _currentTween = CreateTween();
        AddChild(_currentTween);
        _currentTween.InterpolateProperty(this, "global_position", from.GlobalPosition, to.GlobalPosition, duration);
        _currentTween.Start();
        await ToSignal(_currentTween, "tween_all_completed");
        QueueFree();
    }
}
