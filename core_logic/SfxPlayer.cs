using Godot;
using System;

/// <summary>
/// 「放一次就完」的音效声部池——按键音这类**不需要等它播完、也不需要控制音量**的音走它。
///
/// 声部**挂在传入的宿主节点上**（`MusicManager` 是 autoload，常驻）而不是调用方所在的场景：
/// 这类音效最典型的用法就是「按下去 → 立刻切场景」，挂场景里的话节点会跟着 `QueueFree`，
/// 声音刚起个头就被掐掉。
///
/// 多个声部**轮换**使用，所以连点也不会让后一声掐掉前一声。
///
/// 为什么不是 `Node` 子类：它自己不占生命周期——只是「在别人的节点下建几个播放器、
/// 轮流用」的一层簿记，没有 `_Ready` / `_ExitTree` 要做的事。做成 Node 反而要它自己
/// 找地方挂。宿主销毁时子播放器跟着走，不需要额外清理。
///
/// 从 `MusicManager` 拆出来是为了守住 300 行红线（规范 B）——它和 BGM 那套调度没有关系，
/// 唯一的交集只是「音效从哪来」由宿主通过 `picker` 提供（即 `MusicManager.PickSfx`）。
/// </summary>
public sealed class SfxPlayer
{
    /// <summary>声部数。连点时轮换使用，后一声不会掐掉前一声。</summary>
    private const int VoiceCount = 4;

    /// <summary>播放器所在的音频总线（与场景里既有的音效一致，音量由设置界面控这条总线）。</summary>
    private const string Bus = "SFX";

    private readonly Node host;
    private readonly Func<string, AudioStream> picker;

    /// <summary>首次播放时才建（没人按按钮就不花这份钱）。</summary>
    private AudioStreamPlayer[] voices;
    private int nextVoice;

    /// <param name="host">声部挂在谁下面（应当是一个常驻节点）。</param>
    /// <param name="picker">「槽位名 → 音效」，传 `MusicManager.PickSfx` 即可。</param>
    public SfxPlayer(Node host, Func<string, AudioStream> picker)
    {
        this.host = host;
        this.picker = picker;
    }

    /// <summary>抽一条音效播出来。槽位没配或加载失败时**什么都不做**（picker 已经报过警）。</summary>
    public void Play(string slot)
    {
        if (host == null || !host.IsInsideTree()) return;

        AudioStream stream = picker?.Invoke(slot);
        if (stream == null) return;

        voices ??= CreateVoices();
        AudioStreamPlayer voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        voice.Stream = stream;
        voice.Play();
    }

    private AudioStreamPlayer[] CreateVoices()
    {
        var created = new AudioStreamPlayer[VoiceCount];
        for (int index = 0; index < created.Length; index++)
        {
            var voice = new AudioStreamPlayer { Bus = Bus };
            host.AddChild(voice);
            created[index] = voice;
        }
        return created;
    }
}
