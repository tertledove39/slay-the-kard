using Godot;
using System;
using System.Threading.Tasks;

public partial class Bullet : Control
{

    private Tween _currentTween;
    Vector2 offset = new Vector2(90, 120);
    Random rnd = new Random();

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
        ZIndex = 90;
        Rotation = (to.GlobalPosition - from.GlobalPosition).Angle() + (float)Math.PI/2;
        GlobalPosition = from.GlobalPosition+offset + GetOffset(30);
        this.Visible = true;
        _currentTween = CreateTween();
        _currentTween.TweenProperty(this, "position", to.GlobalPosition+offset+GetOffset(60), duration)
                    //.SetEase(Tween.EaseType.In) // 关键：设置为 Out，实现逐渐加速
                    .SetTrans(Tween.TransitionType.Cubic); // 可选：使用二次方加速曲线，也可以是 Cubic, Back 等
        _currentTween.Finished += OnMoveFinished;
    }

    Vector2 GetOffset(float i)
    {
        return new Vector2((rnd.NextSingle() - 0.5f)*2*i,(rnd.NextSingle() - 0.5f)*2*i);
    }

    private void OnMoveFinished()
    {
        this.Visible = false;
    }
}
