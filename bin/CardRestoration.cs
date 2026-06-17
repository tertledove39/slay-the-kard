using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// 跨场景静态数据管理器，用于在WorldMap / ChooseMission / Battlefield之间传递状态
/// </summary>
public static class BattleStateManager
{
    public static string PlayerDeckId { get; set; } = "default_deck";
    public static battlefield_ battlefield { get; set; }

    // 战役模式：选中的敌人预设名
    public static string SelectedEnemy { get; set; } = "berlin";
    // 战役模式：当前选中的区域名
    public static string SelectedArea { get; set; } = "";
    // 是否处于战役模式
    public static bool IsCampaignMode { get; set; } = false;
    // 已完成的区域集合（跨场景持久）
    public static HashSet<string> CompletedAreas { get; private set; } = new();

    private static readonly string[] AreaOrder = { "area1", "area2", "area3", "area4", "area5", "area6", "area7", "area8", "area9", "area10" };

    public static readonly Dictionary<string, string> EnemyDisplayNames = new()
    {
        { "wehrmacht", "德国国防军" },
        { "luftflotte", "德国航空舰队" },
        { "ss_panzer", "党卫装甲军" },
        { "ostwall", "东方壁垒防线" },
        { "volkssturm", "国民冲锋队" },
        { "fuehrerbunker", "元首地堡" },
        { "berlin", "柏林保卫战" },
    };

    // ============================ 卡组持久化 ============================

    /// <summary>临时卡牌引用列表（当前场景的Player.deck引用，不跨场景持久）</summary>
    public static List<cardBase_> Deck { get; set; } = new();
    /// <summary>从deck.ini或奖励交换中生成的持久卡牌ID列表（不含战斗中临时卡）</summary>
    public static List<string> DeckCardIds { get; set; } = new();
    /// <summary>deck.ini是否已加载（避免重复加载）</summary>
    public static bool IsDeckInitialized { get; set; } = false;
    /// <summary>所有卡牌的CardData缓存（card.ini解析结果，跨场景复用）</summary>
    private static Dictionary<string, CardData> _allCards;
    /// <summary>所有事件数据缓存（event.ini解析结果）</summary>
    private static Dictionary<string, EventData> _allEvents;

    /// <summary>缓存所有卡牌数据，供跨场景访问</summary>
    public static void CacheAllCards(Dictionary<string, CardData> cards)
    {
        if (cards != null && cards.Count > 0)
            _allCards = cards;
    }

    /// <summary>根据ID获取缓存的卡牌数据</summary>
    public static CardData GetCachedCard(string id)
    {
        if (_allCards != null && _allCards.TryGetValue(id, out var card))
            return card;
        return null;
    }

    /// <summary>获取所有已缓存的卡牌数据（复用缓存，避免重复解析INI文件）</summary>
    public static Dictionary<string, CardData> GetAllCachedCards()
    {
        return _allCards != null ? new Dictionary<string, CardData>(_allCards) : new Dictionary<string, CardData>();
    }

    /// <summary>卡牌数据是否已缓存（WorldMap加载完成后即为true）</summary>
    public static bool IsCardDataCached => _allCards != null && _allCards.Count > 0;

    /// <summary>缓存所有事件数据</summary>
    public static void CacheAllEvents(Dictionary<string, EventData> events)
    {
        if (events != null && events.Count > 0)
            _allEvents = events;
    }

    /// <summary>根据ID获取事件数据</summary>
    public static EventData GetEvent(string id)
    {
        if (_allEvents != null && _allEvents.TryGetValue(id, out var ev))
            return ev;
        return null;
    }

    /// <summary>从DeckCardIds构建cardBase_列表（仅用于显示，不入战斗）</summary>
    public static List<cardBase_> BuildDisplayDeck()
    {
        var result = new List<cardBase_>();
        if (DeckCardIds == null || DeckCardIds.Count == 0) return result;

        var cardScene = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        foreach (var id in DeckCardIds)
        {
            var cd = GetCachedCard(id);
            if (cd == null) continue;
            var card = cardScene.Instantiate() as cardBase_;
            card.SetCardInformation(cd);
            card.SetIsFriend(IsFriend.friend);
            result.Add(card);
        }
        return result;
    }

    // ============================ 区域管理 ============================

    /// <summary>
    /// 检查区域是否可进入：未完成且前序区域已完成（area1始终解锁但完成后锁定）
    /// </summary>
    public static bool IsAreaUnlocked(string areaName)
    {
        // 已完成区域不可再进入
        if (CompletedAreas.Contains(areaName)) return false;

        if (areaName == "area1") return true;

        int idx = System.Array.IndexOf(AreaOrder, areaName);
        if (idx <= 0) return false;

        string prevArea = AreaOrder[idx - 1];
        return CompletedAreas.Contains(prevArea);
    }

    /// <summary>标记区域为已完成</summary>
    public static void MarkAreaCompleted(string areaName)
    {
        if (!string.IsNullOrEmpty(areaName))
            CompletedAreas.Add(areaName);
    }

    /// <summary>解锁所有区域（控制台调试用）</summary>
    public static void UnlockAllAreas()
    {
        foreach (var area in AreaOrder)
            CompletedAreas.Add(area);
    }

    // ============================ UI辅助 ============================

    /// <summary>
    /// 以CanvasLayer叠加显示卡组查看界面。
    /// 将展示用卡牌挂到holder上确保Godot管理其生命周期，DisplayCard关闭时一并清理。
    /// </summary>
    public static void ShowDeckViewer(Node parent, List<cardBase_> deckCards)
    {
        deckCards ??= new();
        Deck = deckCards;

        var canvasLayer = new CanvasLayer();
        canvasLayer.Layer = 2;
        parent.AddChild(canvasLayer);

        // 将展示卡牌挂到不可见holder上，确保Godot统一管理生命周期（避免GC时native handle失效）
        var holder = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvasLayer.AddChild(holder);
        foreach (var card in deckCards)
            holder.AddChild(card);

        var displayScene = ResourceLoader.Load<PackedScene>("res://bin/display_card.tscn");
        var display = displayScene.Instantiate() as DisplayCard;
        canvasLayer.AddChild(display);

        // DisplayCard关闭时清理整个CanvasLayer（holder及其中的display cards一起释放）
        display.TreeExiting += () => canvasLayer.QueueFree();
    }
}
