using Godot;
using System.Collections.Generic;

public partial class MusicManager : Node
{
    private const string ConfigPath = "res://configs/music.ini";
    public static MusicManager Instance { get; private set; }

    private readonly Dictionary<string, string> slotPaths = new();
    private AudioStreamPlayer player;
    private string currentSlot = "";
    private string currentPath = "";

    public override void _Ready()
    {
        Instance = this;
        player = new AudioStreamPlayer();
        AddChild(player);
        LoadConfig();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void PlaySlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return;
        if (!slotPaths.TryGetValue(slot, out string path) || string.IsNullOrWhiteSpace(path)) return;
        if (currentSlot == slot && player.Playing) return;
        AudioStream stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushWarning($"MusicManager: failed to load {path}");
            return;
        }
        currentSlot = slot;
        currentPath = path;
        player.Stream = stream;
        player.Play();
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

    private void LoadConfig()
    {
        slotPaths.Clear();
        if (!FileAccess.FileExists(ConfigPath)) return;
        string content = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read).GetAsText();
        var ini = new IniFile();
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        ini.Load(stream);
        if (!ini.HasSection("music")) return;
        foreach (var key in ini.GetSectionKeys("music"))
        {
            slotPaths[key] = ini["music"][key].GetString().Trim();
        }
    }
}
