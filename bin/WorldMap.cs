using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 世界地图界面：显示10个可点击区域，点击后弹出选择任务面板
/// </summary>
public partial class WorldMap : Control
{
    private PackedScene _chooseMissionScene;
    private ChooseMission _chooseMissionPanel;

    // 每个区域的敌人池（从AreaPool.ini加载）
    private Dictionary<string, List<string>> _areaPools = new();

    public override void _Ready()
    {
        LoadAreaPools();
        ConnectAreaButtons();

        // 预加载选择任务界面
        _chooseMissionScene = ResourceLoader.Load<PackedScene>("res://bin/chooseMission.tscn");
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

        var configFile = new IniFile();
        using (var stream = new System.IO.MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                Godot.FileAccess.Open(iniPath, Godot.FileAccess.ModeFlags.Read).GetAsText())))
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
                GD.Print($"Connected area button: {areaName}");
            }
        }
    }

    /// <summary>
    /// 点击区域按钮：随机抽取3个不同的敌人，弹出选择界面
    /// </summary>
    private void OnAreaPressed(string areaName)
    {
        if (_chooseMissionPanel != null) return; // 已经打开

        BattleStateManager.SelectedArea = areaName;

        // 获取该区域的敌人池
        if (!_areaPools.TryGetValue(areaName, out var pool) || pool.Count == 0)
        {
            GD.Print($"No enemy pool for area: {areaName}");
            return;
        }

        // 随机抽取3个互不相同的敌人
        var candidates = new List<string>(pool);
        var selected = PickRandomEnemies(candidates, 3);

        if (selected.Count < 3)
        {
            GD.Print($"Not enough enemies in pool for {areaName}");
            return;
        }

        // 创建并显示选择任务界面
        _chooseMissionPanel = _chooseMissionScene.Instantiate() as ChooseMission;
        if (_chooseMissionPanel != null)
        {
            _chooseMissionPanel.SetEnemies(selected[0], selected[1], selected[2]);
            _chooseMissionPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_chooseMissionPanel);

            // 添加半透明背景遮罩
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0, 0, 0, 0.75f);
            bg.MouseFilter = MouseFilterEnum.Stop;
            _chooseMissionPanel.AddChild(bg);
            _chooseMissionPanel.MoveChild(bg, 0); // 放到最底层

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
}
