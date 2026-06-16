using Godot;
using System;

/// <summary>
/// 选择任务界面控制器：显示3个敌人按钮，玩家选择一个进入战斗
/// </summary>
public partial class ChooseMission : Control
{
    private Label _label1;
    private Label _label2;
    private Label _label3;
    private TextureButton _btn1;
    private TextureButton _btn2;
    private TextureButton _btn3;

    private string _enemy1;
    private string _enemy2;
    private string _enemy3;

    public override void _Ready()
    {
        // 获取按钮
        _btn1 = GetNode<TextureButton>("TextureButton");
        _btn2 = GetNode<TextureButton>("TextureButton2");
        _btn3 = GetNode<TextureButton>("TextureButton3");

        // 创建按钮上方的敌人名称标签
        _label1 = CreateEnemyLabel(_btn1, new Vector2(0, -80));
        _label2 = CreateEnemyLabel(_btn2, new Vector2(0, -80));
        _label3 = CreateEnemyLabel(_btn3, new Vector2(0, -80));

        // 连接按钮信号
        _btn1.Pressed += OnChoose1Pressed;
        _btn2.Pressed += OnChoose2Pressed;
        _btn3.Pressed += OnChoose3Pressed;

        // 从BattleStateManager读取已选定的敌人
        ApplyEnemyNames();
    }

    /// <summary>
    /// 由WorldMap在打开本界面之前调用，设置3个候选敌人
    /// </summary>
    public void SetEnemies(string enemy1, string enemy2, string enemy3)
    {
        _enemy1 = enemy1;
        _enemy2 = enemy2;
        _enemy3 = enemy3;
    }

    private void ApplyEnemyNames()
    {
        if (_label1 != null && !string.IsNullOrEmpty(_enemy1))
            _label1.Text = GetDisplayName(_enemy1);
        if (_label2 != null && !string.IsNullOrEmpty(_enemy2))
            _label2.Text = GetDisplayName(_enemy2);
        if (_label3 != null && !string.IsNullOrEmpty(_enemy3))
            _label3.Text = GetDisplayName(_enemy3);
    }

    private static Label CreateEnemyLabel(TextureButton button, Vector2 offset)
    {
        var label = new Label();
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 28);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.Size = new Vector2(button.Size.X, 60);
        label.Position = button.Position + offset;
        button.GetParent().AddChild(label);
        return label;
    }

    private void OnChoose1Pressed()
    {
        StartBattle(_enemy1);
    }

    private void OnChoose2Pressed()
    {
        StartBattle(_enemy2);
    }

    private void OnChoose3Pressed()
    {
        StartBattle(_enemy3);
    }

    private void StartBattle(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return;

        BattleStateManager.SelectedEnemy = enemyId;
        BattleStateManager.IsCampaignMode = true;

        // 切换到战斗场景
        GetTree().ChangeSceneToFile("res://bin/battleField.tscn");
    }

    private static string GetDisplayName(string enemyId)
    {
        if (BattleStateManager.EnemyDisplayNames.TryGetValue(enemyId, out var name))
            return name;
        return enemyId;
    }
}
