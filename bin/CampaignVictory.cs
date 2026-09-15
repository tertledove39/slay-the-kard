using Godot;
using System.Threading.Tasks;

/// <summary>
/// 战役通关浮层：最后一个区域的战斗烈度归零时显示。
/// 先播放与副官的战斗总结对白，对白结束后返回开始菜单。
/// 层级刻意低于对白气泡（GameDialogueBalloon 为 layer 100），避免暗幕盖住对白。
/// </summary>
public partial class CampaignVictory : CanvasLayer
{
    /// <summary>暗幕层级，必须低于对白气泡的 100</summary>
    private const int OverlayLayer = 90;
    private const float OverlayAlpha = 0.9f;
    private const string DialoguePath = "res://dialogues/campaign_victory.dialogue";
    private const string DialogueTitle = "campaign_victory";
    private const string StartMenuPath = "res://bin/start_menu.tscn";

    /// <summary>
    /// 在指定节点上叠加通关浮层，播放战斗总结对白，随后返回开始菜单。
    /// </summary>
    public static async Task ShowAndReturnToMenu(Node parent)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;

        var overlay = new CampaignVictory { Layer = OverlayLayer };
        parent.AddChild(overlay);
        await overlay.PlaySummaryAsync();
        overlay.QueueFree();

        await SceneLoader.ChangeSceneAsync(parent, StartMenuPath);
    }

    private async Task PlaySummaryAsync()
    {
        var background = new ColorRect();
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.Color = new Color(0f, 0f, 0f, OverlayAlpha);
        background.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(background);

        var dialogue = GetNodeOrNull<GameDialogue>("/root/GameDialogue");
        if (dialogue == null)
        {
            GD.PushWarning("[CampaignVictory] GameDialogue 未注册，跳过通关对白");
            return;
        }

        await dialogue.PlayAsync(DialoguePath, DialogueTitle);
        GD.Print("[CampaignVictory] 战役通关对白结束，返回开始菜单");
    }
}
