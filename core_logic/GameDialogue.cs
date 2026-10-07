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

    /// <summary>
    /// `_ExitTree` 已经跑过、`playGate` 已被释放。
    ///
    /// 这个类是 **autoload**，`_ExitTree` 只在关闭游戏 / 停止调试时触发——而那一刻
    /// 完全可能正好有一段对白在播（`PlayAsync` 停在 `await completion.Task` 上）。
    /// `_ExitTree` 会把 `completion` 取消、`playGate` 释放；被取消的 `await` 随即
    /// 抛出来走 `finally`，那里还有一次 `playGate.Release()`——**对着已经释放的
    /// SemaphoreSlim 调用会抛 `ObjectDisposedException`**。
    ///
    /// 而且它发生在 async 方法的 `finally` 里，而 `Play()` 是不 await 的
    /// fire-and-forget，所以异常会存进一个没人观察的 Task，等到 GC 或同步上下文
    /// 收尾时才炸出来，从调用栈上完全看不出跟对白有关。
    /// </summary>
    private bool shutdownStarted;

    public override void _Ready()
    {
        DialogueManager.DialogueEnded += OnDialogueEnded;
    }

    public override void _ExitTree()
    {
        DialogueManager.DialogueEnded -= OnDialogueEnded;
        shutdownStarted = true;
        completion?.TrySetCanceled();
        playGate.Dispose();
    }

    public async Task<bool> PlayAsync(string resourcePath, string title = "start")
    {
        if (shutdownStarted || !CanPlay(resourcePath))
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
            // 上面那个 await 期间 _ExitTree 可能已经把 playGate 释放了——见 shutdownStarted。
            if (!shutdownStarted)
            {
                playGate.Release();
            }
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
