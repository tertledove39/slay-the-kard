using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public sealed class SettingItem
{
    public string Id { get; init; }
    public string Type { get; init; }
    public string Name { get; init; }
    public string Key { get; init; }
    public float DefaultValue { get; init; }
    public float MinValue { get; init; }
    public float MaxValue { get; init; }
    public float Step { get; init; }
}

public static class SettingsManager
{
    private const string SettingsPath = "res://bin/setting.ini";
    private const string UserSettingsPath = "user://settings.cfg";
    private static readonly List<SettingItem> _items = new();
    private static readonly Dictionary<string, bool> _boolValues = new();
    private static readonly Dictionary<string, float> _floatValues = new();

    public static IReadOnlyList<SettingItem> Items => _items;

    public static void Initialize()
    {
        EnsureAudioBuses();
        if (_items.Count > 0)
        {
            ApplyAllVolumes();
            return;
        }
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
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(key))
            {
                GD.PrintErr($"[Settings] 无效设置项: {section.Key}");
                continue;
            }

            if (type == "bool")
            {
                _items.Add(new SettingItem { Id = section.Key, Type = type, Name = name, Key = key });
                _boolValues[key] = section.Value["value"].GetString().Trim().ToLowerInvariant() == "true";
                continue;
            }

            if (type == "float"
                && TryGetFloat(section.Value, "value", out float defaultValue)
                && TryGetFloat(section.Value, "min", out float minValue)
                && TryGetFloat(section.Value, "max", out float maxValue)
                && TryGetFloat(section.Value, "step", out float step)
                && minValue <= maxValue && step > 0)
            {
                defaultValue = Mathf.Clamp(defaultValue, minValue, maxValue);
                _items.Add(new SettingItem
                {
                    Id = section.Key,
                    Type = type,
                    Name = name,
                    Key = key,
                    DefaultValue = defaultValue,
                    MinValue = minValue,
                    MaxValue = maxValue,
                    Step = step
                });
                _floatValues[key] = defaultValue;
                continue;
            }

            GD.PrintErr($"[Settings] 无效设置项: {section.Key}");
        }

        LoadUserValues();
        ApplyAllVolumes();
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
        SaveUserValues();
    }

    public static float GetFloat(string key)
    {
        Initialize();
        return _floatValues.TryGetValue(key, out float value) ? value : 0f;
    }

    public static void SetFloat(string key, float value)
    {
        Initialize();
        SettingItem item = _items.Find(candidate => candidate.Key == key && candidate.Type == "float");
        if (item == null) return;
        _floatValues[key] = Mathf.Clamp(value, item.MinValue, item.MaxValue);
        ApplyVolume(key, _floatValues[key]);
        SaveUserValues();
    }

    private static bool TryGetFloat(IniSection section, string key, out float value)
    {
        return float.TryParse(section[key].GetString().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static void LoadUserValues()
    {
        var config = new ConfigFile();
        if (config.Load(UserSettingsPath) != Error.Ok) return;
        foreach (SettingItem item in _items)
        {
            if (!config.HasSectionKey("settings", item.Key)) continue;
            if (item.Type == "bool")
                _boolValues[item.Key] = config.GetValue("settings", item.Key).AsBool();
            else if (item.Type == "float")
                _floatValues[item.Key] = Mathf.Clamp((float)config.GetValue("settings", item.Key).AsDouble(), item.MinValue, item.MaxValue);
        }
    }

    private static void SaveUserValues()
    {
        var config = new ConfigFile();
        foreach (var pair in _boolValues) config.SetValue("settings", pair.Key, pair.Value);
        foreach (var pair in _floatValues) config.SetValue("settings", pair.Key, pair.Value);
        Error error = config.Save(UserSettingsPath);
        if (error != Error.Ok) GD.PushWarning($"[Settings] 保存用户配置失败: {error}");
    }

    private static void ApplyAllVolumes()
    {
        foreach (var pair in _floatValues) ApplyVolume(pair.Key, pair.Value);
    }

    private static void EnsureAudioBuses()
    {
        foreach (string bus in new[] { "Music", "SFX", "UI" })
        {
            if (AudioServer.GetBusIndex(bus) >= 0) continue;
            AudioServer.AddBus();
            int busIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(busIndex, bus);
            AudioServer.SetBusSend(busIndex, "Master");
        }
    }

    private static void ApplyVolume(string key, float value)
    {
        string bus = key switch
        {
            "master_volume" => "Master",
            "music_volume" => "Music",
            "sfx_volume" => "SFX",
            "ui_volume" => "UI",
            _ => ""
        };
        if (string.IsNullOrEmpty(bus)) return;
        int busIndex = AudioServer.GetBusIndex(bus);
        if (busIndex < 0)
        {
            GD.PushWarning($"[Settings] Audio Bus不存在: {bus}");
            return;
        }
        float linear = Mathf.Clamp(value / 100f, 0f, 1f);
        AudioServer.SetBusMute(busIndex, linear <= 0f);
        AudioServer.SetBusVolumeDb(busIndex, linear > 0f ? Mathf.LinearToDb(linear) : -80f);
    }
}
