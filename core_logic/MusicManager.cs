using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 全局背景音乐管理器。
/// 槽位配置见 configs/music.ini 的 [music] 段：每个键是一个槽位，值可以写多首曲目（逗号分隔），
/// 播放时从中随机抽取一首。battleBGM_&lt;敌人预设名&gt; 形式的键会自动成为战斗专属BGM槽位。
/// </summary>
public partial class MusicManager : Node
{
    private const string ConfigPath = "res://configs/music.ini";
    public static MusicManager Instance { get; private set; }

    /// <summary>战斗专属BGM槽位前缀，完整槽位名为 battleBGM_&lt;敌人预设名&gt;（如 battleBGM_DonBend）。</summary>
    public const string BattleBgmPrefix = "battleBGM_";
    /// <summary>未配置战斗专属BGM时使用的通用战斗槽位。</summary>
    public const string BattleSlot = "battle";

    /// <summary>槽位值为多首曲目时的分隔符。</summary>
    private static readonly char[] PathSeparator = { ',' };

    // 槽位名按忽略大小写比较：battleBGM_DonBend 与 battleBGM_donbend 等价。
    // 若区分大小写，写错时会静默回退到通用战斗BGM，属于很难排查的失败模式。
    private readonly Dictionary<string, string[]> slotPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random rnd = new();
    private AudioStreamPlayer player;
    private string currentSlot = "";
    private string currentPath = "";

    public override void _Ready()
    {
        Instance = this;
        SettingsManager.Initialize();
        player = new AudioStreamPlayer { Bus = "Music" };
        AddChild(player);
        LoadConfig();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>槽位是否配置了至少一首曲目。用于战斗专属BGM的回退判断。</summary>
    public bool HasSlot(string slot)
    {
        return !string.IsNullOrWhiteSpace(slot)
            && slotPaths.TryGetValue(slot, out string[] paths)
            && paths.Length > 0;
    }

    /// <summary>
    /// 播放指定战斗的BGM：优先使用 battleBGM_&lt;敌人预设名&gt; 槽位，未配置时回退到通用 battle 槽位。
    /// </summary>
    public void PlayBattleSlot(string enemyPreset)
    {
        string specific = BattleBgmPrefix + enemyPreset;
        PlaySlot(HasSlot(specific) ? specific : BattleSlot);
    }

    public void PlaySlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return;
        if (!slotPaths.TryGetValue(slot, out string[] paths) || paths.Length == 0) return;
        // 已在同一槽位播放时不重新抽取，避免反复进入同一场景就换曲
        if (currentSlot == slot && player.Playing) return;

        string path = PickPath(paths);

        // 换了槽位但抽到正在播放的同一首时不重播，保持跨场景音乐连续
        if (currentPath == path && player.Playing)
        {
            currentSlot = slot;
            return;
        }

        AudioStream stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: failed to load {path}");
            return;
        }
        // 循环目前只覆盖MP3；改用ogg/wav需在此补 AudioStreamOggVorbis.Loop 与 AudioStreamWAV.LoopMode
        if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
        currentSlot = slot;
        currentPath = path;
        player.Stream = stream;
        player.Play();
        GD.Print($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: PlaySlot({slot}) -> {path}");
    }

    /// <summary>从槽位曲目中随机取一首；多首时避开正在播放的那首，避免连续重复。</summary>
    private string PickPath(string[] paths)
    {
        if (paths.Length == 1) return paths[0];
        int index = rnd.Next(paths.Length);
        if (paths[index] == currentPath)
            index = (index + 1 + rnd.Next(paths.Length - 1)) % paths.Length;
        return paths[index];
    }

    public void StopMusic()
    {
        currentSlot = "";
        currentPath = "";
        player.Stop();
    }

    public void SetVolumeDb(float db)
    {
        player.VolumeDb = db;
    }

    /// <summary>把槽位值拆成曲目列表：以逗号分隔，忽略空白项。</summary>
    private static string[] ParsePaths(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        string[] parts = raw.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(parts.Length);
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed)) result.Add(trimmed);
        }
        return result.ToArray();
    }

    private void LoadConfig()
    {
        slotPaths.Clear();
        if (!FileAccess.FileExists(ConfigPath)) return;
        string content = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read).GetAsText();
        var ini = new IniFile();
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        ini.Load(stream);
        if (!ini.HasSection("music")) return;
        // [music] 段下每个键都成为一个槽位，battleBGM_<预设名> 因此无需额外解析逻辑
        foreach (var key in ini.GetSectionKeys("music"))
        {
            string[] paths = ParsePaths(ini["music"][key].GetString());
            if (paths.Length > 0) slotPaths[key] = paths;
        }
    }
}
