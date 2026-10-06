using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 事件选项的效果解析与执行。
///
/// 从 <see cref="EventScene"/> 里拆出来（原先该类 299 行，已逼近规范里 300 行的红线）：
/// 事件界面负责「长什么样、点了哪个」，本类负责「点了之后发生什么」。
///
/// event.ini 的 choiceN_effect 语法（逗号分隔，同一选项内可混用）：
///   none                    无变化
///   materialPoints(n)       增减资源点，负数即花费
///   hp(n)                   增减血量，负数即扣血；血量无上限，下限 0
///   replaceCard(id)         让玩家选一张卡，替换为 id
///   replaceRandomCard(id)   随机替换一张卡为 id
/// </summary>
public static class EventEffectRunner
{
    /// <summary>
    /// 执行一条选项效果。<paramref name="owner"/> 只用于给选择卡牌的弹窗提供父节点。
    /// </summary>
    public static async Task Execute(Node owner, string effect)
    {
        effect = (effect ?? "").Trim();
        if (string.IsNullOrEmpty(effect) || effect == "none")
            return;

        var replaceCardIds = new List<string>();
        var randomReplaceCardIds = new List<string>();

        foreach (var seg in effect.Split(','))
        {
            var s = seg.Trim();
            if (s.StartsWith("materialPoints(") && s.EndsWith(")"))
            {
                int amount = s["materialPoints(".Length..^1].ToInt();
                // 负得超过余额时钳到 0，不出现负数资源点。
                // 界面侧已按 ParseMaterialCost 拦掉买不起的选项，这里只是兜底。
                BattleStateManager.MaterialPoints = Math.Max(0, BattleStateManager.MaterialPoints + amount);
            }
            else if (s.StartsWith("hp(") && s.EndsWith(")"))
            {
                BattleStateManager.AddHp(s["hp(".Length..^1].ToInt());
            }
            else if (s.StartsWith("replaceCard(") && s.EndsWith(")"))
            {
                replaceCardIds.Add(s["replaceCard(".Length..^1].Trim());
            }
            else if (s.StartsWith("replaceRandomCard(") && s.EndsWith(")"))
            {
                randomReplaceCardIds.Add(s["replaceRandomCard(".Length..^1].Trim());
            }
        }

        if (replaceCardIds.Count > 0)
        {
            var chosenOldIds = await ChooseSomeCard.Show(owner, replaceCardIds.Count, "选择要替换的卡牌");
            for (int i = 0; i < chosenOldIds.Count && i < replaceCardIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(chosenOldIds[i]))
                    ReplaceCardInDeck(chosenOldIds[i], replaceCardIds[i]);
            }
        }

        foreach (var newCardId in randomReplaceCardIds)
        {
            await DoReplaceCard(owner, newCardId, random: true);
        }
    }

    /// <summary>
    /// 选项的净资源点开销：只累加负数（花费）。正数（获得）不计入，
    /// 免得出现「先给后扣」被误判成买不起。
    /// 供事件界面在生成按钮时判断是否可选。
    /// </summary>
    public static int ParseMaterialCost(string effect)
    {
        int cost = 0;
        if (string.IsNullOrEmpty(effect)) return 0;

        foreach (var seg in effect.Split(','))
        {
            var s = seg.Trim();
            if (!s.StartsWith("materialPoints(") || !s.EndsWith(")")) continue;

            int v = s["materialPoints(".Length..^1].ToInt();
            if (v < 0) cost += -v;
        }
        return cost;
    }

    /// <summary>
    /// 选项会往卡组加入哪些卡——即 replaceCard / replaceRandomCard 的目标卡，
    /// 按 id **去重计数**并保持首次出现的顺序。供悬浮预览使用。
    /// </summary>
    public static List<(CardData card, int count)> ParseAddedCards(string effect)
    {
        var result = new List<(CardData, int)>();
        if (string.IsNullOrEmpty(effect)) return result;

        var order = new List<string>();
        var counts = new Dictionary<string, int>();

        foreach (var seg in effect.Split(','))
        {
            var s = seg.Trim();
            string id = null;
            if (s.StartsWith("replaceCard(") && s.EndsWith(")"))
                id = s["replaceCard(".Length..^1].Trim();
            else if (s.StartsWith("replaceRandomCard(") && s.EndsWith(")"))
                id = s["replaceRandomCard(".Length..^1].Trim();

            if (string.IsNullOrEmpty(id)) continue;

            if (!counts.ContainsKey(id)) order.Add(id);
            counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
        }

        foreach (var id in order)
        {
            var data = BattleStateManager.GetCachedCard(id);
            if (data != null) result.Add((data, counts[id]));
        }
        return result;
    }

    private static void ReplaceCardInDeck(string oldCardId, string newCardId)
    {
        var deckIds = BattleStateManager.DeckCardIds;
        int idx = deckIds.IndexOf(oldCardId);
        if (idx >= 0)
        {
            deckIds[idx] = newCardId;
            GD.Print($"[EventEffect] 卡牌替换: {oldCardId} -> {newCardId}");
        }
    }

    /// <summary>
    /// 随机替换：从卡组里随机挑一张换成 <paramref name="newCardId"/>。
    /// 抽不到可替换的卡（卡组空）时静默返回。
    /// </summary>
    private static async Task DoReplaceCard(Node owner, string newCardId, bool random)
    {
        var deckIds = BattleStateManager.DeckCardIds;
        if (deckIds == null || deckIds.Count == 0) return;

        string oldCardId;
        if (random)
        {
            oldCardId = deckIds[new Random().Next(deckIds.Count)];
        }
        else
        {
            var chosen = await ChooseSomeCard.Show(owner, 1, "选择一张要替换的卡牌");
            oldCardId = chosen.Count > 0 ? chosen[0] : null;
            if (string.IsNullOrEmpty(oldCardId)) return;
        }

        ReplaceCardInDeck(oldCardId, newCardId);
    }
}
