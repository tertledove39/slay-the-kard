using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 在指定位置冒一下烟。
///
/// **一个脚本、两个场景**，差别只在 Export 出去的 `SizeScale`：
/// `effects/smoke_effect.tscn`（阵亡烟，默认 1）与 `effects/smoke_small_effect.tscn`
/// （坦克炮口/落点的小烟）——所以不是两套实现，是两个 Export 值
/// （与 `bullet` / `bombing` / `TankAttack` 同一套路）。
/// </summary>
public partial class SmokeEffect : Effect
{
    private const int FrameCount = 16;
    private const float DefaultDuration = 0.4f;

    /// <summary>
    /// 整体尺寸倍数。**1 = 原大小**（阵亡那一声烟就是它）；
    /// `smoke_small` 那个场景填得比 1 小，炮口烟才不会遮住半张桌子。
    /// 它乘在「逐帧放大」之上，所以放大过程本身的速度不受影响。
    /// </summary>
    [Export] public float SizeScale = 1f;

    private Sprite2D sprite;
    private Tween currentTween;

    public override void _Ready()
    {
        sprite = GetNode<Sprite2D>("SmokeSprite");
    }

    // source / count 这个特效用不上：它只是在给定位置冒一下。
    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                                    cardBase_ source = null, int count = 0)
    {
        if (positions == null || positions.Count == 0) return;
        sprite.GlobalPosition = positions[0];
        float duration = time.HasValue && time.Value > 0f ? time.Value : DefaultDuration;
        sprite.Modulate = new Color(1f, 1f, 1f, 0f);
        currentTween?.Kill();
        currentTween = CreateTween();
        currentTween.TweenMethod(Callable.From<float>(UpdateProgress), 0f, 1f, duration);
        await ToSignal(currentTween, Tween.SignalName.Finished);
    }

    public override void PrepareForUse()
    {
        Visible = true;
        sprite.Frame = 0;
        sprite.Scale = Vector2.One;
        sprite.Modulate = Colors.Transparent;
    }

    public override void ResetForPool()
    {
        currentTween?.Kill();
        currentTween = null;
        sprite.Frame = 0;
        sprite.Scale = Vector2.One;
        sprite.Modulate = Colors.Transparent;
        Visible = false;
    }

    private void UpdateProgress(float progress)
    {
        int frame = Mathf.Min(FrameCount - 1, Mathf.FloorToInt(progress * FrameCount));
        sprite.Frame = frame;
        float scale = (1f + frame * 0.2f) * SizeScale;
        sprite.Scale = Vector2.One * scale;
        float alpha = progress < 0.1f
            ? progress / 0.1f
            : progress > 0.3f ? (1f - progress) / 0.7f : 1f;
        sprite.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
    }
}
