using System.Collections.Generic;
using Godot;

/// <summary>
/// 跨场景静态数据管理器，用于在WorldMap / ChooseMission / Battlefield之间传递状态
/// </summary>
public static class BattleStateManager
{
    // 准备传递给战斗场景的数据
    public static string PlayerDeckId { get; set; } = "default_deck";
    public static List<cardBase_> Deck { get; set; } = new List<cardBase_>();

    public static battlefield_ battlefield { get; set; }

    // 战役模式：选中的敌人预设名（对应 enemyTurn.ini 中的节名）
    public static string SelectedEnemy { get; set; } = "berlin";

    // 战役模式：当前选中的区域名
    public static string SelectedArea { get; set; } = "";

    // 是否处于战役模式（从WorldMap进入的）
    public static bool IsCampaignMode { get; set; } = false;

    // 已完成的区域集合（静态持久，跨场景保留）
    public static HashSet<string> CompletedAreas { get; private set; } = new();

    // 所有区域的顺序列表（area1 → area10，莫斯科 → 柏林）
    private static readonly string[] AreaOrder = { "area1", "area2", "area3", "area4", "area5", "area6", "area7", "area8", "area9", "area10" };

    // 敌人预设名 → 中文显示名 映射
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

    /// <summary>
    /// 检查给定区域是否已解锁
    /// area1始终解锁，areaN需要在area(N-1)完成后解锁
    /// </summary>
    public static bool IsAreaUnlocked(string areaName)
    {
        if (areaName == "area1") return true;

        int idx = System.Array.IndexOf(AreaOrder, areaName);
        if (idx <= 0) return false;

        string prevArea = AreaOrder[idx - 1];
        return CompletedAreas.Contains(prevArea);
    }

    /// <summary>
    /// 标记区域为已完成（战斗胜利后调用）
    /// </summary>
    public static void MarkAreaCompleted(string areaName)
    {
        if (!string.IsNullOrEmpty(areaName))
            CompletedAreas.Add(areaName);
    }

    /// <summary>
    /// 解锁所有区域（控制台调试用）
    /// </summary>
    public static void UnlockAllAreas()
    {
        foreach (var area in AreaOrder)
            CompletedAreas.Add(area);
    }
}
