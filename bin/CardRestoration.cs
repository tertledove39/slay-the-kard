using System.Collections.Generic;
using Godot;

public static class BattleStateManager
{
    // 准备传递给战斗场景的数据
    public static string PlayerDeckId { get; set; } = "default_deck";
    public static List<cardBase_> Deck { get; set; } = new List<cardBase_>();

    public static battlefield_ battlefield { get; set; }
    
    // 也可以直接传递复杂的对象（如果不需要持久化到磁盘）
    // public List<string> InitialHandCardIds { get; set; } = new List<string>();
}