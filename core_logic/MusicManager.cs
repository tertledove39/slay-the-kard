using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 全局背景音乐管理器。
/// 槽位配置见 configs/music.ini 的 [music] 段：每个键是一个槽位，值可以写多首曲目（逗号分隔），
/// 播放时从中随机抽取一首。battleBGM_&lt;敌人预设名&gt; 形式的键会自动成为战斗专属BGM槽位。
/// 场景切换不会掐断当前曲目：新槽位先排队，等当前曲目自然播完再切。
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
    /// <summary>当前曲目播完后要切过去的槽位；为空表示留在当前槽位继续放。</summary>
    private string pendingSlot = "";

    public override void _Ready()
    {
        Instance = this;
        SettingsManager.Initialize();
        player = new AudioStreamPlayer { Bus = "Music" };
        // 内建循环会让曲目永不结束，Finished 也就不触发，「播完再切」无从谈起。
        // 因此统一关掉内建循环（见 DisableBuiltinLoop），改由 OnTrackFinished 在曲末决定续播还是换曲。
        player.Finished += OnTrackFinished;
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

    /// <summary>
    /// 请求播放某个槽位。若当前有曲目在播，请求只入队，等这首自然播完再切，
    /// 不会把正在响的曲子拦腰截断。
    /// </summary>
    public void PlaySlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return;
        if (!slotPaths.TryGetValue(slot, out string[] paths) || paths.Length == 0) return;

        // 当前没有在播（首次进入、或已停止）时立即起播，没有可等待的曲目
        if (!player.Playing)
        {
            pendingSlot = "";
            StartSlot(slot);
            return;
        }

        // 已经在这个槽位上：撤销排队，继续把当前这首放完
        if (currentSlot == slot)
        {
            pendingSlot = "";
            return;
        }

        // 正在播别的槽位：排队。连续切换时后者覆盖前者，只需记住最后一次请求
        pendingSlot = slot;
    }

    /// <summary>立刻起播指定槽位：随机取一首，若正是当前这首则原地续用不重播。</summary>
    private void StartSlot(string slot)
    {
        if (!slotPaths.TryGetValue(slot, out string[] paths) || paths.Length == 0) return;
        string path = PickPath(paths);

        // 换槽位但抽到正在播放的同一首时不重播，保持音乐连续
        if (path == currentPath && player.Playing)
        {
            currentSlot = slot;
            return;
        }

        AudioStream stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: failed to load {path}");
            // 载入失败时退回当前槽位续播，避免一个坏路径让整局都没有音乐（递归深度最多两层）
            if (!string.IsNullOrEmpty(currentSlot) && currentSlot != slot) StartSlot(currentSlot);
            return;
        }

        DisableBuiltinLoop(stream);
        currentSlot = slot;
        currentPath = path;
        player.Stream = stream;
        player.Play();
        GD.Print($"{Time.GetDatetimeStringFromSystem()} MusicManager.cs: PlaySlot({slot}) -> {path}");
    }

    /// <summary>曲目自然播完：有待切换的槽位就切过去，否则从当前槽位再取一首续播。</summary>
    private void OnTrackFinished()
    {
        string slot = !string.IsNullOrEmpty(pendingSlot) ? pendingSlot : currentSlot;
        pendingSlot = "";
        if (string.IsNullOrEmpty(slot)) return;
        StartSlot(slot);
    }

    /// <summary>
    /// 关闭音频流的内建循环。内建循环下曲目永不结束、Finished 不触发，
    /// 就无法做到「当前曲目播完再切槽位」。循环改由 OnTrackFinished 在曲末重新起播实现，
    /// 三种格式统一处理，不再出现 ogg/wav 播完即静音的情况。
    /// </summary>
    private static void DisableBuiltinLoop(AudioStream stream)
    {
        switch (stream)
        {
            case AudioStreamMP3 mp3: mp3.Loop = false; break;
            case AudioStreamOggVorbis ogg: ogg.Loop = false; break;
            // Godot 4.5 起类名为 AudioStreamWav（旧的 AudioStreamWAV 已废弃）
            case AudioStreamWav wav: wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled; break;
        }
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
        pendingSlot = "";
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
