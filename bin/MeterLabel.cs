using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 电表式滚动数字组件 - 每个数位拥有独立的裁剪窗口和垂直数字滚条，
/// 数值变化时逐位滚动，低位先动高位后动，模拟电表/里程表效果
/// </summary>
public partial class MeterLabel : Control
{
    private Font _font;
    private int _fontSize;
    private Color _fontColor;

    private int _currentValue;
    private int _digitCount;
    private bool _isAnimating;
    private float _digitH;
    private float _digitW;

    private readonly List<Control> _windows = new();
    private readonly List<Control> _strips = new();

    /// <summary>数字宽度 = 字号 * 此比率</summary>
    public float DigitWidthRatio = 0.55f;

    /// <summary>每位基础动画时长（秒）</summary>
    public float BaseAnimDuration = 0.28f;

    /// <summary>低位到高位的级联延迟（秒）</summary>
    public float CascadeDelay = 0.05f;

    /// <summary>
    /// 初始化电表组件
    /// </summary>
    public void Initialize(Font font, int fontSize, Color color, int digitCount, int initialValue)
    {
        _font = font;
        _fontSize = fontSize;
        _fontColor = color;
        _digitCount = Math.Max(digitCount, 1);
        _currentValue = initialValue;
        _digitH = fontSize;
        _digitW = fontSize * DigitWidthRatio;

        RebuildStrips();
        DisplayImmediate(initialValue);
    }

    private void RebuildStrips()
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        _windows.Clear();
        _strips.Clear();

        for (int i = 0; i < _digitCount; i++)
        {
            var window = new Control();
            window.ClipContents = true;
            window.Size = new Vector2(_digitW, _digitH);
            window.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(window);
            _windows.Add(window);

            var strip = new Control();
            strip.MouseFilter = MouseFilterEnum.Ignore;
            window.AddChild(strip);
            _strips.Add(strip);

            // 在滚条上自上而下排列 9→0
            for (int d = 9; d >= 0; d--)
            {
                var lbl = CreateDigitLabel(d);
                lbl.Position = new Vector2(0, (9 - d) * _digitH);
                strip.AddChild(lbl);
            }
        }

        // 从右向左排列各窗口
        float totalW = _digitCount * _digitW;
        for (int i = 0; i < _digitCount; i++)
            _windows[i].Position = new Vector2(totalW - (_digitCount - i) * _digitW, 0);

        Size = new Vector2(totalW, _digitH);
    }

    private Label CreateDigitLabel(int digit)
    {
        var label = new Label();
        label.Text = digit.ToString();
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Size = new Vector2(_digitW, _digitH);
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", _fontSize);
        label.AddThemeColorOverride("font_color", _fontColor);
        return label;
    }

    /// <summary>
    /// 立即显示值（跳过动画）
    /// </summary>
    public void DisplayImmediate(int value)
    {
        _currentValue = value;
        int displayCount = _digitCount;
        string str = value.ToString().PadLeft(displayCount);

        for (int i = 0; i < displayCount; i++)
        {
            char c = str[i];
            float y = c == ' '
                ? -10f * _digitH
                : -(9 - (c - '0')) * _digitH;
            _strips[displayCount - 1 - i].Position = new Vector2(0, y);
        }
    }

    /// <summary>
    /// 电表滚动到目标值（异步动画）
    /// </summary>
    public async Task AnimateTo(int targetValue)
    {
        if (targetValue == _currentValue)
            return;

        if (_isAnimating)
        {
            _currentValue = targetValue;
            DisplayImmediate(targetValue);
            return;
        }

        _isAnimating = true;

        try
        {
            // 自动扩展位数
            int needed = targetValue == 0 ? 1 : (int)Math.Floor(Math.Log10(targetValue)) + 1;
            if (needed > _digitCount)
            {
                _digitCount = needed;
                _digitW = _fontSize * DigitWidthRatio;
                RebuildStrips();
                DisplayImmediate(_currentValue);
            }

            int oldVal = _currentValue;
            _currentValue = targetValue;

            string oldStr = oldVal.ToString().PadLeft(_digitCount);
            string newStr = targetValue.ToString().PadLeft(_digitCount);

            // 找到最高变化位
            int firstChange = -1;
            for (int i = 0; i < _digitCount; i++)
            {
                if (oldStr[i] != newStr[i])
                {
                    firstChange = i;
                    break;
                }
            }

            if (firstChange < 0)
                return;

            // 从最低位（最右边）向最高变化位依次动画
            var tasks = new List<Task>();
            for (int pos = _digitCount - 1; pos >= firstChange; pos--)
            {
                if (oldStr[pos] == newStr[pos])
                    continue;

                int stripIdx = pos; // strip[0]是最左边窗口
                int oldD = oldStr[pos] == ' ' ? -1 : oldStr[pos] - '0';
                int newD = newStr[pos] == ' ' ? -1 : newStr[pos] - '0';
                float delay = (_digitCount - 1 - pos) * CascadeDelay;

                tasks.Add(AnimateDigit(stripIdx, oldD, newD, delay));
            }

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task AnimateDigit(int stripIdx, int fromDigit, int toDigit, float delaySec)
    {
        if (delaySec > 0)
            await ToSignal(GetTree().CreateTimer(delaySec), SceneTreeTimer.SignalName.Timeout);

        float targetY = toDigit >= 0
            ? -(9 - toDigit) * _digitH
            : -10f * _digitH;

        int eFrom = fromDigit < 0 ? 0 : fromDigit;
        int eTo = toDigit < 0 ? 0 : toDigit;
        int diff = Math.Abs(eTo - eFrom);
        float duration = Math.Clamp(diff / 10f * 0.5f + BaseAnimDuration * 0.4f, 0.1f, 0.45f);

        // 向下滚动：正数差直接tween；向上滚动同理
        var tween = CreateTween();
        tween.TweenProperty(_strips[stripIdx], "position:y", targetY, duration)
             .SetEase(Tween.EaseType.InOut)
             .SetTrans(Tween.TransitionType.Cubic);

        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
