using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class DisplayCard : Control
{
    [Export] public float PanSpeed = 100.0f;
    [Export] public float MinY = -1000f;
    [Export] public float MaxY = 200f;

    private float _targetY;
    private Control _cardContainer;

    public override void _Ready()
    {
        // 半透明黑色背景遮罩，阻止点击穿透到后方场景
        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.5f);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(bg);

        _cardContainer = new Control();
        _cardContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        _cardContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_cardContainer);

        _targetY = 0f;

        // 连接返回按钮信号，并确保按钮在最上层
        var button = GetNode<Button>("Button");
        button.Pressed += _on_button_pressed;
        MoveChild(button, GetChildCount() - 1);

        // 异步加载卡牌，Display内部首帧让出使背景先渲染
        Display(BattleStateManager.Deck);
    }

    public async Task Display(List<cardBase_> cardList)
    {
        if (cardList == null || cardList.Count == 0) return;

        // 先让出当前帧，确保背景遮罩和按钮立即渲染，避免用户感知卡顿
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var sortedList = cardList
            .OrderBy(c => c.ReadCost())
            .ThenBy(c => c.name)
            .ThenByDescending(c => c.ReadAttack())
            .ToList();

        int rows = (sortedList.Count - 1) / 6;
        MinY = -rows * 300f;

        if (MinY >= MaxY)
            MinY = MaxY - 100f;

        int counter = 0;
        int row = 0;
        int batchCount = 0;
        const int batchSize = 3; // 每帧复制3张，分摊Duplicate开销

        foreach (var card in sortedList)
        {
            counter++;
            if (counter > 6)
            {
                counter = 1;
                row++;
            }

            // 使用Duplicate创建展示副本，避免影响牌组中原卡的状态（字体/布局等）
            var displayCopy = card.Duplicate() as cardBase_;
            _cardContainer.AddChild(displayCopy);
            displayCopy.Position = new Vector2(200 * counter + 100, 300 * row);

            batchCount++;
            if (batchCount >= batchSize)
            {
                batchCount = 0;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            bool isWheel = false;
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
            {
                _targetY += PanSpeed;
                isWheel = true;
            }
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
            {
                _targetY -= PanSpeed;
                isWheel = true;
            }

            if (isWheel)
            {
                _targetY = Mathf.Clamp(_targetY, MinY, MaxY);
                AcceptEvent();
            }
        }
    }

    public override void _Process(double delta)
    {
        float currentY = Mathf.Lerp(_cardContainer.Position.Y, _targetY, 10.0f * (float)delta);
        _cardContainer.Position = new Vector2(_cardContainer.Position.X, currentY);
    }

    void _on_button_pressed()
    {
        // 展示副本随DisplayCard一起释放，牌组原卡不受影响
        QueueFree();
    }
}
