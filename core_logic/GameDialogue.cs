using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DialogueManagerRuntime;
using Godot;

public partial class GameDialogue : Node
{
    private const string StartMenuPath = "res://bin/start_menu.tscn";
    private readonly Dictionary<string, Resource> resourceCache = new();
    private readonly SemaphoreSlim playGate = new(1, 1);
    private TaskCompletionSource<bool> completion;
    private Resource activeResource;

    public override void _Ready()
    {
        DialogueManager.DialogueEnded += OnDialogueEnded;
    }

    public override void _ExitTree()
    {
        DialogueManager.DialogueEnded -= OnDialogueEnded;
        completion?.TrySetCanceled();
        playGate.Dispose();
    }

    public async Task<bool> PlayAsync(string resourcePath, string title = "start")
    {
        if (!CanPlay(resourcePath))
        {
            return false;
        }

        Resource resource = LoadDialogue(resourcePath);
        if (resource == null)
        {
            return false;
        }

        await playGate.WaitAsync();
        try
        {
            activeResource = resource;
            completion = new TaskCompletionSource<bool>();
            DialogueManager.ShowDialogueBalloon(resource, title);
            return await completion.Task;
        }
        finally
        {
            activeResource = null;
            completion = null;
            playGate.Release();
        }
    }

    public void Play(string resourcePath, string title = "start")
    {
        _ = PlayAsync(resourcePath, title);
    }

    private bool CanPlay(string resourcePath)
    {
        string currentPath = GetTree().CurrentScene?.SceneFilePath ?? "";
        if (currentPath == StartMenuPath)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} GameDialogue.cs: dialogue is disabled on the start menu");
            return false;
        }

        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            GD.PushError($"{Time.GetDatetimeStringFromSystem()} GameDialogue.cs: dialogue path is empty");
            return false;
        }
        return true;
    }

    private Resource LoadDialogue(string resourcePath)
    {
        if (resourceCache.TryGetValue(resourcePath, out Resource cached))
        {
            return cached;
        }

        Resource resource = ResourceLoader.Load(resourcePath);
        if (resource == null)
        {
            GD.PushError($"{Time.GetDatetimeStringFromSystem()} GameDialogue.cs: failed to load {resourcePath}");
            return null;
        }
        resourceCache[resourcePath] = resource;
        return resource;
    }

    private void OnDialogueEnded(Resource resource)
    {
        if (resource != activeResource)
        {
            return;
        }
        completion?.TrySetResult(true);
    }
}
