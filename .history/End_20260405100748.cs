using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class End : CanvasLayer
{
    private ColorRect _overlay;
    private Tween _tween;

    // 单例实例（方便全局调用）
    private static End _instance;
    public static End Instance => _instance;

    // 卡牌选择相关
    private List<cardBase_> _choiceCards = new List<cardBase_>();
    private TaskCompletionSource<cardBase_> _choiceTaskSource;
    private HBoxContainer _choiceContainer;

    public override void _Ready()
    {
        _instance = this;

        // 1. 创建黑色矩形
        _overlay = new ColorRect();
        
        // 2. 设置矩形铺满整个屏幕 (Godot 4 写法)
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.GrowHorizontal = Control.GrowDirection.Both;
        _overlay.GrowVertical = Control.GrowDirection.Both;
        
        // 3. 设置初始颜色为黑色，透明度为 0 (完全透明)
        _overlay.Color = new Color(0, 0, 0, 0);
        
        // 4. 设置鼠标过滤：
        // MouseFilterEnum.Stop = 阻挡点击 (变暗时通常不希望玩家操作)
        // MouseFilterEnum.Ignore = 穿透点击 (仅视觉变暗)
        _overlay.MouseFilter = Control.MouseFilterEnum.Stop; 
        GetNode<Sprite2D>("img").Visible = false;
        AddChild(_overlay);

        // 创建卡牌选择容器
        _choiceContainer = new HBoxContainer();
        _choiceContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
        _choiceContainer.Alignment = BoxContainer.AlignmentMode.Center;
        _choiceContainer.Visible = false;
        AddChild(_choiceContainer);
    }

    /// <summary>
    /// 变暗效果
    /// </summary>
    /// <param name="duration">过渡时间 (秒)</param>
    /// <param name="alpha">最终透明度 (0-1)</param>
    public void Dim(float duration = 0.5f, float alpha = 0.7f)
    {
        this.Visible = true;
        if (_tween != null) _tween.Kill(); // 杀死旧的动画防止冲突

        _tween = CreateTween();
        // 补间动画：修改 _overlay 的 color 属性，目标 Alpha 为指定值
        _tween.TweenProperty(_overlay, "color", new Color(0, 0, 0, alpha), duration);

        var img = GetNode<Sprite2D>("img");
        img.Visible = true;
            img.Texture        = GD.Load<Texture2D>("res://assest/苏联国徽G.png");
            img.GlobalPosition = new Vector2(800, 300);
        img.Scale = new Vector2(4f, 4f);
    }

    /// <summary>
    /// 恢复明亮
    /// </summary>
    /// <param name="duration">过渡时间 (秒)</param>
    public void Brighten(float duration = 0.5f)
    {
        if (_tween != null) _tween.Kill();

        _tween = CreateTween();
        // 目标 Alpha 为 0 (完全透明)
        _tween.TweenProperty(_overlay, "color", new Color(0, 0, 0, 0), duration);
    }

    /// <summary>
    /// 显示卡牌选择界面
    /// </summary>
    public async Task<cardBase_> ShowCardChoice(List<cardBase_> cards)
    {
        _choiceTaskSource = new TaskCompletionSource<cardBase_>();
        _choiceCards = new List<cardBase_>(cards);

        // 清空容器
        foreach (var child in _choiceContainer.GetChildren())
        {
            _choiceContainer.RemoveChild(child);
            child.QueueFree();
        }

        // 添加卡牌到容器
        foreach (var card in _choiceCards)
        {
            var cardCopy = card.Duplicate() as cardBase_;
            cardCopy.Scale = new Vector2(0.8f, 0.8f);
            cardCopy.MouseFilter = Control.MouseFilterEnum.Stop;
            cardCopy.Connect("gui_input", Callable.From<InputEvent>((input) => OnCardClicked(cardCopy, input)));
            _choiceContainer.AddChild(cardCopy);
        }

        // 显示界面
        _choiceContainer.Visible = true;
        Dim(0.3f, 0.5f);

        // 等待选择
        var selectedCard = await _choiceTaskSource.Task;

        // 隐藏界面
        _choiceContainer.Visible = false;
        Brighten(0.3f);

        return selectedCard;
    }

    private void OnCardClicked(cardBase_ clickedCard, InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            // 找到对应的原始卡牌
            var originalCard = _choiceCards.Find(c => c.name == clickedCard.name && c.id == clickedCard.id);
            if (originalCard != null)
            {
                _choiceTaskSource.SetResult(originalCard);
            }
        }
    }
}