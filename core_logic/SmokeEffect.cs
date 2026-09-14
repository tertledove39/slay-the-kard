using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class SmokeEffect : Effect
{
    private const int FrameCount = 16;
    private const float DefaultDuration = 0.4f;
    private Sprite2D sprite;
    private Tween currentTween;

    public override void _Ready()
    {
        sprite = GetNode<Sprite2D>("SmokeSprite");
    }

    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null)
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
        float scale = 1f + frame * 0.2f;
        sprite.Scale = Vector2.One * scale;
        float alpha = progress < 0.1f
            ? progress / 0.1f
            : progress > 0.3f ? (1f - progress) / 0.7f : 1f;
        sprite.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
    }
}
