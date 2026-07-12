using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class SettingItem
{
    public string Id { get; init; }
    public string Type { get; init; }
    public string Name { get; init; }
    public string Key { get; init; }
}

public static class SettingsManager
{
    private const string SettingsPath = "res://bin/setting.ini";
    private static readonly List<SettingItem> _items = new();
    private static readonly Dictionary<string, bool> _boolValues = new();

    public static IReadOnlyList<SettingItem> Items => _items;

    public static void Initialize()
    {
        if (_items.Count > 0) return;
        if (!Godot.FileAccess.FileExists(SettingsPath))
        {
            GD.PrintErr($"[Settings] 配置文件不存在: {SettingsPath}");
            return;
        }

        var content = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Read).GetAsText();
        var ini = new IniFile();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        ini.Load(stream);

        foreach (var section in ini)
        {
            var type = section.Value["type"].GetString().Trim();
            var name = section.Value["name"].GetString().Trim();
            var key = section.Value["key"].GetString().Trim();
            if (type != "bool" || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(key))
            {
                GD.PrintErr($"[Settings] 无效设置项: {section.Key}");
                continue;
            }

            _items.Add(new SettingItem { Id = section.Key, Type = type, Name = name, Key = key });
            _boolValues[key] = section.Value["value"].GetString().Trim().ToLowerInvariant() == "true";
        }
    }

    public static bool GetBool(string key)
    {
        Initialize();
        return _boolValues.TryGetValue(key, out var value) && value;
    }

    public static void SetBool(string key, bool value)
    {
        Initialize();
        if (!_boolValues.ContainsKey(key)) return;
        _boolValues[key] = value;
        GD.Print($"[Settings] {key}={value}");
    }
}
