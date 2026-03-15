using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 资源管理器（单例），用于缓存 Texture、PackedScene，以及用于卡牌池的空卡牌实例。
/// 通过缓存避免频繁的 GD.Load/ResourceLoader.Load 造成的卡顿。
/// </summary>
public partial class ResourceManager : Node
{
    public static ResourceManager Instance { get; private set; }

    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private Dictionary<string, PackedScene> sceneCache = new Dictionary<string, PackedScene>();
    private Queue<cardBase_> emptyCardPool = new Queue<cardBase_>();

    // 默认的空卡牌池大小
    private const int DefaultEmptyCardPoolSize = 4;
    private const int EmptyCardPoolTargetSize = 12; // 预热/自动补充目标池大小
    private const string CardScenePath = "res://bin/cardbase.tscn";

    private Task? poolRefillTask;
    private bool isInitializing = false;

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 初始化资源管理器，并对常用贴图/场景做预加载。
    /// 该方法会在主线程内分帧加载，避免卡住帧率。
    /// </summary>
    public void Initialize()
    {
        if (isInitializing)
            return;

        isInitializing = true;

        // 先缓存常用贴图（阻塞执行）
        var toPreload = new List<string>()
        {
            "res://cards/ussr.png",
            "res://cards/Germany.png",
            "res://cards/卡背_command.png",
            "res://cards/HQ_moscow.png",
            "res://cards/HQ_berlin.png",
        };

        PreloadTextures(toPreload);

        // 预加载卡牌场景
        PreloadScene(CardScenePath);

        // 预加载空卡池（预热，避免后续卡顿）
        EnsureEmptyCardPool(EmptyCardPoolTargetSize);

        isInitializing = false;
    }

    /// <summary>
    /// 获取或加载贴图并缓存
    /// </summary>
    public Texture2D GetTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (textureCache.TryGetValue(path, out var tex))
            return tex;

        if (!FileAccess.FileExists(path))
            return null;

        tex = GD.Load<Texture2D>(path);
        if (tex != null)
            textureCache[path] = tex;

        return tex;
    }

    /// <summary>
    /// 获取或加载场景并缓存
    /// </summary>
    public PackedScene GetScene(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (sceneCache.TryGetValue(path, out var scene))
            return scene;

        if (!FileAccess.FileExists(path))
            return null;

        scene = ResourceLoader.Load<PackedScene>(path);
        if (scene != null)
            sceneCache[path] = scene;

        return scene;
    }

    /// <summary>
    /// 预加载贴图列表（同步完成）
    /// </summary>
    public void PreloadTextures(IEnumerable<string> paths)
    {
        if (paths == null)
            return;

        foreach (var path in paths)
        {
            GetTexture(path);
        }
    }

    /// <summary>
    /// 以不堵塞主线程的方式预加载贴图列表
    /// </summary>
    public async Task PreloadTexturesAsync(IEnumerable<string> paths)
    {
        if (paths == null)
            return;

        foreach (var path in paths)
        {
            GetTexture(path);
            await ToSignal(GetTree(), "physics_frame");
        }
    }

    /// <summary>
    /// 预加载场景（同步完成）
    /// </summary>
    public void PreloadScene(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        GetScene(path);
    }

    /// <summary>
    /// 以不堵塞主线程的方式预加载场景
    /// </summary>
    public async Task PreloadSceneAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        GetScene(path);
        await ToSignal(GetTree(), "physics_frame");
    }

    /// <summary>
    /// 获取一个空卡牌（从池中获取），如果池为空则立即创建一个，并异步补齐池。
    /// </summary>
    public cardBase_ AcquireEmptyCard()
    {
        if (emptyCardPool.Count > 0)
        {
            var card = emptyCardPool.Dequeue();
            if (card != null && card.GetParent() != null)
            {
                card.GetParent().RemoveChild(card);
            }
            card.Visible = true;
            card.SetProcess(true);

            // 如果池中数量低于目标值，后台自动补齐
            EnsurePoolRefillAsync();
            return card;
        }

        // 池空时立即创建一个，并在后台补齐池
        var newCard = CreateEmptyCard();
        EnsurePoolRefillAsync();
        if (newCard != null)
        {
            newCard.Visible = true;
            newCard.SetProcess(true);
        }

        return newCard;
    }

    private void EnsurePoolRefillAsync()
    {
        if (poolRefillTask != null && !poolRefillTask.IsCompleted)
            return;

        poolRefillTask = EnsureEmptyCardPoolAsync(EmptyCardPoolTargetSize);
    }

    /// <summary>
    /// 获取一个空卡牌（从池中获取），如果池为空则立即创建一个，并同步补齐池。
    /// </summary>
    public cardBase_ AcquireEmptyCardSync()
    {
        if (emptyCardPool.Count > 0)
        {
            var card = emptyCardPool.Dequeue();
            if (card != null && card.GetParent() != null)
            {
                card.GetParent().RemoveChild(card);
            }
            card.Visible = true;
            card.SetProcess(true);
            return card;
        }

        var newCard = CreateEmptyCard();
        EnsureEmptyCardPool(DefaultEmptyCardPoolSize);
        if (newCard != null)
        {
            newCard.Visible = true;
            newCard.SetProcess(true);
        }

        return newCard;
    }

    /// <summary>
    /// 获取一个空卡牌池，确保至少 minCount 个，立即完成（同步）。
    /// </summary>
    public void EnsureEmptyCardPool(int minCount)
    {
        if (minCount <= 0)
            return;

        while (emptyCardPool.Count < minCount)
        {
            var card = CreateEmptyCard();
            if (card != null)
            {
                card.Visible = false;
                card.SetProcess(false);
                emptyCardPool.Enqueue(card);
            }
        }
    }

    /// <summary>
    /// 归还空卡牌到池中
    /// </summary>
    public void ReleaseEmptyCard(cardBase_ card)
    {
        if (card == null)
            return;

        card.Visible = false;
        card.SetProcess(false);
        emptyCardPool.Enqueue(card);

        // 如果池大小不足则自动异步补齐
        if (emptyCardPool.Count < EmptyCardPoolTargetSize)
        {
            EnsurePoolRefillAsync();
        }
    }

    private async Task EnsureEmptyCardPoolAsync(int minCount)
    {
        if (minCount <= 0)
            return;

        while (emptyCardPool.Count < minCount)
        {
            var card = CreateEmptyCard();
            if (card != null)
            {
                card.Visible = false;
                card.SetProcess(false);
                emptyCardPool.Enqueue(card);
            }

            // 让帧先渲染，避免卡顿
            await ToSignal(GetTree(), "physics_frame");
        }
    }

    private cardBase_ CreateEmptyCard()
    {
        var scene = GetScene(CardScenePath);
        if (scene == null)
            return null;

        var card = scene.Instantiate() as cardBase_;
        if (card == null)
            return null;

        AddChild(card);
        return card;
    }
}
