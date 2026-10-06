using DialogueManagerRuntime;
using Godot;
using Godot.Collections;

public partial class GameDialogueBalloon : CanvasLayer
{
    private const string PortraitRoot = "res://assest/";
    private const string DefaultExpression = "normal";
    private TextureRect portrait;
    private Resource activeResource;

    // 内层气球场景（插件自带 example_balloon.tscn）中的目标节点路径
    private const string BalloonPanelPath = "ExampleBalloon/Balloon/MarginContainer/PanelContainer";
    private const string CharacterLabelPath = BalloonPanelPath + "/MarginContainer/HBoxContainer/VBoxContainer/CharacterLabel";

    // 对话框外观：无边框、黑色半透明底色
    private static readonly Color PanelColor = new(0f, 0f, 0f, 0.7f);
    private const int CharacterNameFontSize = 30;

    public override void _Ready()
    {
        portrait = GetNode<TextureRect>("Portrait");
        ApplyBalloonStyle();
        DialogueManager.GotDialogue += OnGotDialogue;
        DialogueManager.DialogueEnded += OnDialogueEnded;
    }

    /// <summary>
    /// 覆盖插件默认的对话框主题：去掉边框与圆角、改为黑色半透明底色，并放大角色名。
    /// 使用节点级 theme override，因此不需要改动 addons 下的插件资源。
    /// </summary>
    private void ApplyBalloonStyle()
    {
        var panel = GetNodeOrNull<PanelContainer>(BalloonPanelPath);
        if (panel != null)
        {
            var style = new StyleBoxFlat
            {
                BgColor = PanelColor,
                BorderWidthTop = 0,
                BorderWidthBottom = 0,
                BorderWidthLeft = 0,
                BorderWidthRight = 0,
                CornerRadiusTopLeft = 0,
                CornerRadiusTopRight = 0,
                CornerRadiusBottomLeft = 0,
                CornerRadiusBottomRight = 0
            };
            panel.AddThemeStyleboxOverride("panel", style);
        }

        var characterLabel = GetNodeOrNull<RichTextLabel>(CharacterLabelPath);
        if (characterLabel != null)
        {
            characterLabel.AddThemeFontSizeOverride("normal_font_size", CharacterNameFontSize);
            // 插件默认给角色名 50% 透明度，放大后更显灰暗，这里恢复为不透明白色
            characterLabel.Modulate = Colors.White;
        }
    }

    public override void _ExitTree()
    {
        DialogueManager.GotDialogue -= OnGotDialogue;
        DialogueManager.DialogueEnded -= OnDialogueEnded;
    }

    public void Start(Resource resource, string title = "", Array<Variant> states = null)
    {
        activeResource = resource;

        // 气球场景挂的是插件自带的 GDScript 实现（example_balloon.gd），
        // 节点底层类型是 CanvasLayer，不能强制转换成 C# 的 DialogueManagerRuntime.ExampleBalloon。
        // 这里按方法名动态调用其 start(with_dialogue_resource, title, extra_game_states)。
        Node balloon = GetNodeOrNull("ExampleBalloon");
        if (balloon == null)
        {
            GD.PushError($"{Time.GetDatetimeStringFromSystem()} GameDialogueBalloon.cs: ExampleBalloon node not found, dialogue skipped");
            return;
        }

        if (states == null || states.Count == 0)
            balloon.Call("start", resource, title);
        else
            balloon.Call("start", resource, title, states);
    }

    private void OnGotDialogue(DialogueLine line)
    {
        string expression = FindExpression(line);
        string portraitPath = $"{PortraitRoot}{expression}.png";
        Texture2D texture = ResourceLoader.Load<Texture2D>(portraitPath);
        if (texture == null && expression != DefaultExpression)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} GameDialogueBalloon.cs: missing {portraitPath}, using normal portrait");
            texture = ResourceLoader.Load<Texture2D>($"{PortraitRoot}{DefaultExpression}.png");
        }
        portrait.Texture = texture;
        portrait.Visible = texture != null && !line.Tags.Contains("portrait=none");
    }

    private static string FindExpression(DialogueLine line)
    {
        foreach (string expression in new[] { "happy", "sad", "angry", "normal" })
        {
            if (line.Tags.Contains(expression) || line.Tags.Contains($"expression={expression}"))
            {
                return expression;
            }
        }
        return DefaultExpression;
    }

    private void OnDialogueEnded(Resource resource)
    {
        if (resource == activeResource)
        {
            QueueFree();
        }
    }
}
