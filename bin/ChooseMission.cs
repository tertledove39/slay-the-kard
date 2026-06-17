using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 任务类型：战斗或事件
/// </summary>
public enum MissionType { Battle, Event }

/// <summary>
/// 任务条目：标识符(ID)、显示名称、类型
/// </summary>
public struct MissionEntry
{
    public string Id;
    public string DisplayName;
    public MissionType Type;
}

/// <summary>
/// 选择任务界面控制器：显示3个按钮，玩家选择进入战斗或事件
/// </summary>
public partial class ChooseMission : Control
{
    private Label _label1, _label2, _label3;
    private TextureButton _btn1, _btn2, _btn3;
    private List<MissionEntry> _entries = new();
    private string _areaName;

    public override void _Ready()
    {
        _btn1 = GetNode<TextureButton>("TextureButton");
        _btn2 = GetNode<TextureButton>("TextureButton2");
        _btn3 = GetNode<TextureButton>("TextureButton3");

        _label1 = CreateLabel(_btn1, new Vector2(0, -80));
        _label2 = CreateLabel(_btn2, new Vector2(0, -80));
        _label3 = CreateLabel(_btn3, new Vector2(0, -80));

        _btn1.Pressed += () => OnChoose(0);
        _btn2.Pressed += () => OnChoose(1);
        _btn3.Pressed += () => OnChoose(2);

        ApplyNames();
    }

    /// <summary>设置任务条目列表和区域名，并刷新显示</summary>
    public void SetEntries(List<MissionEntry> entries, string areaName)
    {
        _entries = entries ?? new();
        _areaName = areaName;
        ApplyNames();
    }

    private void ApplyNames()
    {
        var labels = new[] { _label1, _label2, _label3 };
        for (int i = 0; i < labels.Length && i < _entries.Count; i++)
        {
            if (labels[i] != null)
                labels[i].Text = _entries[i].DisplayName;
        }
    }

    private static Label CreateLabel(TextureButton button, Vector2 offset)
    {
        var label = new Label();
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 26);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.Size = new Vector2(button.Size.X, 60);
        label.Position = button.Position + offset;
        button.GetParent().AddChild(label);
        return label;
    }

    private void OnChoose(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        var entry = _entries[index];
        if (string.IsNullOrEmpty(entry.Id)) return;

        if (entry.Type == MissionType.Event)
        {
            StartEvent(entry.Id);
        }
        else
        {
            StartBattle(entry.Id);
        }
    }

    private async void StartBattle(string enemyId)
    {
        BattleStateManager.SelectedEnemy = enemyId;
        BattleStateManager.IsCampaignMode = true;
        await SceneLoader.ChangeSceneAsync(this, "res://bin/battleField.tscn");
    }

    private async void StartEvent(string eventId)
    {
        // 加载事件数据
        var eventData = BattleStateManager.GetEvent(eventId);
        if (eventData == null) return;

        // 打开事件界面（CanvasLayer叠加在当前场景上）
        await EventScene.Show(this, eventData, _areaName);
    }
}
