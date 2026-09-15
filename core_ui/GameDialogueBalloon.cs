using DialogueManagerRuntime;
using Godot;
using Godot.Collections;

public partial class GameDialogueBalloon : CanvasLayer
{
    private const string PortraitRoot = "res://assest/";
    private const string DefaultExpression = "normal";
    private TextureRect portrait;
    private Resource activeResource;

    public override void _Ready()
    {
        portrait = GetNode<TextureRect>("Portrait");
        DialogueManager.GotDialogue += OnGotDialogue;
        DialogueManager.DialogueEnded += OnDialogueEnded;
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
