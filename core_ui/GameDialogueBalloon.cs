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
        GetNode<ExampleBalloon>("ExampleBalloon").Start(resource, title, states);
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
