using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 任务类型：战斗（进入Battlefield对战）或事件（进入EventScene剧情）
/// </summary>
public enum MissionType { Battle, Event }

/// <summary>
/// 任务条目数据结构：包含标识符、显示名称和任务类型。
/// 由WorldMap从AreaPool.ini中随机抽取并转换为MissionEntry后传递给ChooseMission面板。
/// </summary>
public struct MissionEntry
{
    /// <summary>任务标识符。战斗类型时是敌人ID（如"wehrmacht"），事件类型时是事件ID（如"supply_drop"）</summary>
    public string Id;
    /// <summary>在ChooseMission按钮下方显示的名称，如"德国国防军"或"补给空投"</summary>
    public string DisplayName;
    /// <summary>任务类型：Battle=进入战斗场景，Event=进入事件剧情</summary>
    public MissionType Type;
}

/// <summary>
/// 选择任务界面控制器：显示3个按钮，玩家选择进入战斗或事件
/// </summary>
public partial class ChooseMission : Control
{
    private Label _label1, _label2, _label3;
    private TextureButton _btn1, _btn2, _btn3;
    private TextureButton[] _buttons;
    private Label[] _labels;
    private List<MissionEntry> _entries = new();
    private string _areaName;
    private float _buttonY;
    private float _labelOffsetY;

    public override void _Ready()
    {
        _btn1 = GetNode<TextureButton>("TextureButton");
        _btn2 = GetNode<TextureButton>("TextureButton2");
        _btn3 = GetNode<TextureButton>("TextureButton3");
        _buttons = new[] { _btn1, _btn2, _btn3 };

        _label1 = CreateLabel(_btn1, new Vector2(0, -80));
        _label2 = CreateLabel(_btn2, new Vector2(0, -80));
        _label3 = CreateLabel(_btn3, new Vector2(0, -80));
        _labels = new[] { _label1, _label2, _label3 };

        _buttonY = _btn1.Position.Y;
        _labelOffsetY = -80f;

        _btn1.Pressed += () => OnChoose(0);
        _btn2.Pressed += () => OnChoose(1);
        _btn3.Pressed += () => OnChoose(2);

        ConnectHover(_btn1);
        ConnectHover(_btn2);
        ConnectHover(_btn3);

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
        int count = _entries.Count;
        if (count == 0 || _buttons == null) return;

        float screenWidth = GetViewportRect().Size.X;
        float buttonWidth = _btn1.Size.X;
        float totalWidth = count * buttonWidth;
        float spacing = (screenWidth - totalWidth) / (count + 1);

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (i < count)
            {
                float x = spacing + i * (buttonWidth + spacing);
                _buttons[i].Visible = true;
                _buttons[i].Position = new Vector2(x, _buttonY);
                if (_labels[i] != null)
                {
                    _labels[i].Text = _entries[i].DisplayName;
                    _labels[i].Visible = true;
                    _labels[i].Position = new Vector2(x, _buttonY + _labelOffsetY);
                }
            }
            else
            {
                _buttons[i].Visible = false;
                if (_labels[i] != null) _labels[i].Visible = false;
            }
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

    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;

    private static void ConnectHover(TextureButton button)
    {
        button.MouseEntered += () => AnimateButton(button, HoverScale);
        button.MouseExited += () => AnimateButton(button, 1f);
    }

    private static void AnimateButton(CanvasItem button, float scale)
    {
        if (button is Control control) control.PivotOffset = control.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
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
        var eventData = BattleStateManager.GetEvent(eventId);
        if (eventData == null) return;

        await EventScene.Show(this, eventData, _areaName);

        if (GetParent() is WorldMap wm)
            wm.DismissChooseMission();
    }
}
