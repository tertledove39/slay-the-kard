using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 世界地图界面：显示10个可点击区域，点击后弹出选择任务面板。
/// 区域按顺序解锁（area1 → area2 → ... → area10）。
/// 按 ` 键打开控制台，支持 help / unlockall 等调试指令。
/// </summary>
public partial class WorldMap : Control
{
    private PackedScene _chooseMissionScene;
    private ChooseMission _chooseMissionPanel;

    // 每个区域的敌人池（从AreaPool.ini加载）
    private Dictionary<string, List<string>> _areaPools = new();
    // 区域按钮缓存
    private Dictionary<string, TextureButton> _areaButtons = new();

    // 控制台
    private Panel _consolePanel;
    private LineEdit _consoleInput;
    private bool _consoleVisible;
    private Label _consoleOutput;

    private static readonly string[] WorldMapCommands = new[]
    {
        "help",
        "unlockall",
    };

    public override void _Ready()
    {
        LoadAreaPools();
        ConnectAreaButtons();
        RefreshAreaStates();

        // 预加载选择任务界面
        _chooseMissionScene = ResourceLoader.Load<PackedScene>("res://bin/chooseMission.tscn");
    }

    public override void _Input(InputEvent @event)
    {
        // 按 ` 键切换控制台
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Quoteleft)
        {
            ToggleConsole();
        }
        // Tab 自动补全
        if (@event is InputEventKey tabEvent && tabEvent.Pressed && tabEvent.Keycode == Key.Tab && _consoleVisible)
        {
            HandleAutocomplete();
        }
    }

    /// <summary>
    /// 加载区域敌人池配置文件
    /// </summary>
    private void LoadAreaPools()
    {
        var iniPath = "res://bin/AreaPool.ini";
        if (!Godot.FileAccess.FileExists(iniPath))
        {
            GD.PushError($"AreaPool.ini not found: {iniPath}");
            return;
        }

        var content = Godot.FileAccess.Open(iniPath, Godot.FileAccess.ModeFlags.Read).GetAsText();
        var configFile = new IniFile();
        using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
        {
            configFile.Load(stream);
        }

        foreach (var section in configFile)
        {
            var areaName = section.Key;
            var enemies = new List<string>();
            var keys = configFile.GetSectionKeys(areaName);
            foreach (var key in keys)
            {
                var enemyId = configFile[areaName][key].ToString().Trim();
                if (!string.IsNullOrEmpty(enemyId) && !enemies.Contains(enemyId))
                    enemies.Add(enemyId);
            }
            if (enemies.Count > 0)
                _areaPools[areaName] = enemies;
        }

        GD.Print($"Loaded area pools: {_areaPools.Count} areas");
    }

    /// <summary>
    /// 自动连接所有area按钮的pressed信号
    /// </summary>
    private void ConnectAreaButtons()
    {
        foreach (var child in GetChildren())
        {
            if (child is TextureButton btn && child.Name.ToString().StartsWith("area"))
            {
                var areaName = child.Name.ToString();
                btn.Pressed += () => OnAreaPressed(areaName);
                _areaButtons[areaName] = btn;
                GD.Print($"Connected area button: {areaName}");
            }
        }
    }

    /// <summary>
    /// 刷新所有区域按钮的锁定/解锁显示状态
    /// </summary>
    private void RefreshAreaStates()
    {
        foreach (var kv in _areaButtons)
        {
            bool unlocked = BattleStateManager.IsAreaUnlocked(kv.Key);
            kv.Value.Disabled = !unlocked;
            // 锁定区域呈灰色半透明，解锁区域正常显示
            kv.Value.Modulate = unlocked ? Colors.White : new Color(0.4f, 0.4f, 0.4f, 0.6f);
        }
    }

    /// <summary>
    /// 点击区域按钮：随机抽取3个不同的敌人，弹出选择界面
    /// </summary>
    private void OnAreaPressed(string areaName)
    {
        if (_chooseMissionPanel != null) return;

        if (!BattleStateManager.IsAreaUnlocked(areaName))
        {
            GD.Print($"Area {areaName} is locked");
            return;
        }

        BattleStateManager.SelectedArea = areaName;

        if (!_areaPools.TryGetValue(areaName, out var pool) || pool.Count == 0)
        {
            GD.Print($"No enemy pool for area: {areaName}");
            return;
        }

        var candidates = new List<string>(pool);
        var selected = PickRandomEnemies(candidates, 3);

        if (selected.Count < 3)
        {
            GD.Print($"Not enough enemies in pool for {areaName}");
            return;
        }

        _chooseMissionPanel = _chooseMissionScene.Instantiate() as ChooseMission;
        if (_chooseMissionPanel != null)
        {
            _chooseMissionPanel.SetEnemies(selected[0], selected[1], selected[2]);
            _chooseMissionPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            _chooseMissionPanel.ZIndex = 200;
            AddChild(_chooseMissionPanel);

            // 半透明黑色背景遮罩
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0, 0, 0, 0.75f);
            bg.MouseFilter = MouseFilterEnum.Stop;
            _chooseMissionPanel.AddChild(bg);
            _chooseMissionPanel.MoveChild(bg, 0);

            GD.Print($"Opened ChooseMission for {areaName}: {selected[0]}, {selected[1]}, {selected[2]}");
        }
    }

    /// <summary>
    /// 从候选列表中随机抽取count个互不相同的敌人
    /// </summary>
    private static List<string> PickRandomEnemies(List<string> pool, int count)
    {
        var shuffled = new List<string>(pool);
        var rng = new Random();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled.Take(Math.Min(count, shuffled.Count)).ToList();
    }

    // ============================================================
    // 控制台功能
    // ============================================================

    private void ToggleConsole()
    {
        if (_consolePanel == null) CreateConsole();
        _consoleVisible = !_consoleVisible;
        _consolePanel.Visible = _consoleVisible;
        if (_consoleOutput != null) _consoleOutput.Visible = _consoleVisible;
        if (_consoleVisible) _consoleInput.GrabFocus();
    }

    private void CreateConsole()
    {
        // 输出面板（控制台上方，显示多行帮助等信息）
        _consoleOutput = new Label();
        _consoleOutput.Visible = false;
        _consoleOutput.Position = new Vector2(50, 50);
        _consoleOutput.Size = new Vector2(600, 0);
        _consoleOutput.ZIndex = 1001;
        _consoleOutput.AddThemeColorOverride("font_color", Colors.LimeGreen);
        _consoleOutput.AddThemeFontSizeOverride("font_size", 13);
        _consoleOutput.Text = "";
        AddChild(_consoleOutput);

        // 输入面板
        _consolePanel = new Panel();
        _consolePanel.Visible = false;
        _consolePanel.Position = new Vector2(50, 10);
        _consolePanel.Size = new Vector2(600, 36);
        _consolePanel.ZIndex = 1000;
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.85f);
        _consolePanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_consolePanel);

        _consoleInput = new LineEdit();
        _consoleInput.SetAnchorsPreset(LayoutPreset.FullRect);
        _consoleInput.AddThemeColorOverride("font_color", Colors.LimeGreen);
        _consoleInput.AddThemeFontSizeOverride("font_size", 14);
        _consoleInput.PlaceholderText = "输入指令，回车执行。输入 help 查看可用指令...";
        _consoleInput.TextSubmitted += OnConsoleSubmit;
        _consolePanel.AddChild(_consoleInput);
    }

    private void OnConsoleSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string cmd = text.Trim().ToLowerInvariant();
        _consoleInput.Text = "";

        switch (cmd)
        {
            case "help":
                _consoleOutput.Text =
                    "=== WorldMap 控制台指令 ===\n" +
                    "  help        显示此帮助信息\n" +
                    "  unlockall   解锁所有区域（area1 ~ area10）\n" +
                    "================================\n" +
                    $"当前已完成区域: {string.Join(", ", BattleStateManager.CompletedAreas)}";
                _consoleOutput.Visible = true;
                break;

            case "unlockall":
                BattleStateManager.UnlockAllAreas();
                RefreshAreaStates();
                _consoleOutput.Text = "已解锁全部区域 (area1 ~ area10)";
                _consoleOutput.Visible = true;
                GD.Print("All areas unlocked via console");
                break;

            default:
                _consoleOutput.Text = $"未知指令: {cmd}\n输入 help 查看可用指令";
                _consoleOutput.Visible = true;
                break;
        }
    }

    private void HandleAutocomplete()
    {
        if (_consoleInput == null) return;

        string currentText = _consoleInput.Text;
        if (string.IsNullOrEmpty(currentText)) return;

        var matches = WorldMapCommands
            .Where(c => c.StartsWith(currentText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) return;

        // 如果有多个匹配，找出最长公共前缀
        if (matches.Count == 1)
        {
            _consoleInput.Text = matches[0];
        }
        else
        {
            string commonPrefix = currentText;
            while (commonPrefix.Length < matches[0].Length)
            {
                char next = matches[0][commonPrefix.Length];
                if (matches.All(m => m.Length > commonPrefix.Length && m[commonPrefix.Length] == next))
                    commonPrefix += next;
                else
                    break;
            }
            _consoleInput.Text = commonPrefix;
        }
        _consoleInput.CaretColumn = _consoleInput.Text.Length;
    }
}
