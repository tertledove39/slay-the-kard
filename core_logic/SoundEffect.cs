using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// **只放一段音效**、不画任何东西的特效。卡牌语音（打出这张卡时喊一声）走它。
///
/// 槽位不写死在场景里，而是从**特效名的参数**传进来：卡牌写 `playEffect = sfx(严冬)`，
/// `EffectRegistry.ParseName` 把「严冬」拆出来交给 <see cref="Configure"/>，
/// 这里再去 `configs/music.ini` 的 `[sfx]` 段取文件。
///
/// 之所以不做成「一个音效一个场景」：语音会越加越多，一音效一场景的话每加一句
/// 都要新建一个 `.tscn` 再改注册表；现在加一句只要在 `[sfx]` 写一行、
/// 在卡上写 `playEffect = sfx(名字)`。
///
/// **音效文件与槽位名都在 `configs/music.ini`**，本类只负责「什么时候放、放完再走」。
/// </summary>
public partial class SoundEffect : Effect
{
    /// <summary>
    /// 音量（dB，0 = 原音量）。同一个场景服务所有语音，所以这是**全局**的一个微调；
    /// 单条语音要单独调，改素材或者以后给参数加第二段即可。
    /// </summary>
    [Export] public float VolumeDb = 0f;

    /// <summary>槽位名，由特效名的参数给出（`sfx(严冬)` → 「严冬」）。</summary>
    private string slot;

    private AudioStreamPlayer player;

    public override void _Ready()
    {
        // `Configure` 在 AddChild 之后才调用，那时本节点已经进树、_Ready 已跑过，
        // 所以这里取播放器是安全的。
        player = GetNodeOrNull<AudioStreamPlayer>("Sfx");
    }

    public override void Configure(string argument)
    {
        slot = argument;
    }

    public override async Task Play(IReadOnlyList<Vector2> positions = null, float? time = null,
                                    cardBase_ source = null, int count = 0)
    {
        if (player == null)
        {
            GD.PushWarning("SoundEffect: 场景里没有 Sfx 播放器节点");
            return;
        }
        if (string.IsNullOrWhiteSpace(slot))
        {
            // 写成 `playEffect = sfx` 而漏了括号时走这里——留一行日志，
            // 否则表现是「这张卡打出时没声」，很难查。
            GD.PushWarning("SoundEffect: 没给音效槽位，应该写成 playEffect = sfx(槽位名)");
            return;
        }

        AudioStream stream = MusicManager.Instance?.PickSfx(slot);
        if (stream == null)
        {
            GD.PushWarning($"SoundEffect: 音效槽位 '{slot}' 取不到（检查 configs/music.ini 的 [sfx] 段）");
            return;
        }

        player.VolumeDb = VolumeDb;
        player.Stream = stream;
        player.Play();

        // **必须等它放完再返回**：调用方 `RunEffect` 的 finally 会立刻回收这个特效节点，
        // 不等的话播放器会连着声音一起被删掉，只听得见开头一小截。
        // 整条链是 fire-and-forget 的，所以等在这里不会卡住游戏。
        await ToSignal(player, AudioStreamPlayer.SignalName.Finished);
    }
}
