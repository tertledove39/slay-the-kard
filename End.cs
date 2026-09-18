using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 战斗结束浮层：负责暗幕、国徽，以及挂在 <c>end</c> 节点下的
/// <c>SettlementOverlay</c> 场景（战斗结算面板 / 战斗失败面板）。
///
/// 面板的布局、字号与配色都在 <c>bin/settlement_panel.tscn</c> 里，
/// 本类只负责填入数值与等待玩家确认。物资点的每行明细调用
/// <see cref="BattleScore"/>，与 <c>battlefield_.CalculateMaterialPoints()</c>
/// 共用同一组系数，保证明细相加等于总额。
/// </summary>
public partial class End : CanvasLayer
{
    /// <summary>面板场景实例的根节点名（在 battleField.tscn 的 end 节点下）</summary>
    private const string OverlayPath = "SettlementOverlay";
    private const string SettlementPath = OverlayPath + "/Settlement";
    private const string DefeatPath = OverlayPath + "/Defeat";

    private ColorRect _overlay;

    private Control _settlement;
    private Label _landRow;
    private Label _airRow;
    private Label _deadRow;
    private Label _hqRow;
    private Label _enemyHqRow;
    private Label _total;
    private Button _confirmButton;

    private Control _defeat;
    private Button _returnButton;

    private Tween _tween;

    public override void _Ready()
    {
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

        // 面板必须排在遮罩之后。Godot 的 GUI 拾取按树序、后加入者优先，
        // 不读 z_index——z_index 只影响绘制。遮罩是全屏 MouseFilter.Stop，
        // 若面板排在它之前，面板上的按钮永远收不到点击。
        // 顺序最终为：img(0) → _overlay(1) → SettlementOverlay(2)。
        var panelRoot = GetNodeOrNull<Control>(OverlayPath);
        if (panelRoot != null)
        {
            MoveChild(panelRoot, GetChildCount() - 1);
        }

        CachePanelNodes();
    }

    /// <summary>
    /// 缓存面板节点。场景缺失时只记一次错误，后续调用安全返回。
    /// </summary>
    private void CachePanelNodes()
    {
        _settlement = GetNodeOrNull<Control>(SettlementPath);
        _defeat = GetNodeOrNull<Control>(DefeatPath);

        if (_settlement == null || _defeat == null)
        {
            GD.PushError($"{Time.GetDatetimeStringFromSystem()} End.cs: 未找到 {OverlayPath}，结算与失败面板不可用");
            return;
        }

        _landRow = GetNodeOrNull<Label>(SettlementPath + "/LandRow");
        _airRow = GetNodeOrNull<Label>(SettlementPath + "/AirRow");
        _deadRow = GetNodeOrNull<Label>(SettlementPath + "/DeadRow");
        _hqRow = GetNodeOrNull<Label>(SettlementPath + "/HqRow");
        _enemyHqRow = GetNodeOrNull<Label>(SettlementPath + "/EnemyHqRow");
        _total = GetNodeOrNull<Label>(SettlementPath + "/Total");
        _confirmButton = GetNodeOrNull<Button>(SettlementPath + "/ConfirmButton");

        _returnButton = GetNodeOrNull<Button>(DefeatPath + "/ReturnButton");
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
        img.Modulate = Colors.White;
        img.Visible = true;
            img.Texture        = GD.Load<Texture2D>("res://assest/苏联国徽G.png");
            img.GlobalPosition = new Vector2(800, 300);
        img.Scale = new Vector2(4f, 4f);
    }

    public async Task ShowDefeat()
    {
        Dim();
        var img = GetNode<Sprite2D>("img");
        img.Modulate = new Color(0.35f, 0.35f, 0.35f, 1f);

        if (_defeat == null || _returnButton == null)
        {
            GD.PushError($"{Time.GetDatetimeStringFromSystem()} End.cs: 失败面板缺失，无法等待玩家返回");
            return;
        }

        _defeat.Visible = true;

        var completion = new TaskCompletionSource<bool>();
        void OnReturnPressed() => completion.TrySetResult(true);
        _returnButton.Pressed += OnReturnPressed;

        await completion.Task;

        _returnButton.Pressed -= OnReturnPressed;
        _defeat.Visible = false;
    }

    /// <summary>
    /// 显示战斗结算明细并等待玩家确认。
    /// 每行的分值来自 <see cref="BattleScore"/>，与总额同源。
    /// </summary>
    public async System.Threading.Tasks.Task ShowSettlement(int landKilled, int airKilled, int friendlyDead, int hqDefenceLost, int enemyHqDamage, int pointsGained)
    {
        if (_settlement == null || _confirmButton == null)
        {
            GD.PushError($"{Time.GetDatetimeStringFromSystem()} End.cs: 结算面板缺失，跳过结算显示");
            return;
        }

        _landRow.Text = $"消灭敌方陆军 x{landKilled}    +{BattleScore.LandPoints(landKilled)}";
        _airRow.Text = $"消灭敌方空军 x{airKilled}    +{BattleScore.AirPoints(airKilled)}";
        _deadRow.Text = $"己方单位损失 x{friendlyDead}    -{BattleScore.DeadPenalty(friendlyDead)}";
        _hqRow.Text = $"总部防御损失 {hqDefenceLost}    -{BattleScore.HqPenalty(hqDefenceLost)}";
        _enemyHqRow.Text = $"对敌方总部造成伤害 {enemyHqDamage}    +{BattleScore.EnemyHqDamagePoints(enemyHqDamage)}";
        _total.Text = $"获得物资点: {pointsGained}";

        _settlement.Visible = true;

        var completion = new System.Threading.Tasks.TaskCompletionSource<bool>();
        void OnConfirmPressed() => completion.TrySetResult(true);
        _confirmButton.Pressed += OnConfirmPressed;

        await completion.Task;

        _confirmButton.Pressed -= OnConfirmPressed;
        _settlement.Visible = false;
    }
}
