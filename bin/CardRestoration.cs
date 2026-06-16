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
}
