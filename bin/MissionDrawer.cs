using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 任务抽取规则：决定每次打开区域面板时给出哪几个候选任务。
/// 纯计算，不依赖任何场景节点，便于独立验证。
/// </summary>
public static class MissionDrawer
{
    /// <summary>事件条目的前缀，AreaPool.ini 中写作 event:事件ID</summary>
    public const string EventPrefix = "event:";

    /// <summary>一轮抽取中的战斗数量</summary>
    public const int BattleCount = 2;

    /// <summary>
    /// 一轮抽取中的事件数量。同时是硬上限：无论池子怎么配，
    /// 都不会出现第 2 个事件，兜底补位也只用战斗。
    /// </summary>
    public const int EventCount = 1;

    /// <summary>
    /// 抽取本轮候选任务。
    ///
    /// 该区域配置了 boss 时：烈度不为 1 则 boss 不参与抽取；
    /// 烈度为 1 则只给出 boss 这一场，玩家没有别的选择。
    /// 其余情况组成 2 战斗 + 1 事件；可选项不足 3 个时用剩余战斗补足，
    /// 但绝不会补第 2 个事件。
    /// </summary>
    /// <param name="pool">该区域的全部条目（enemyN 与 entryN 的值）</param>
    /// <param name="bossName">该区域 AreaPool.ini 的 boss 键，空表示没有 boss</param>
    /// <param name="intensity">当前战斗烈度</param>
    /// <param name="rng">随机源，留空则新建；测试可注入固定种子</param>
    public static List<string> Draw(IReadOnlyList<string> pool, string bossName, int intensity, Random rng = null)
    {
        var result = new List<string>();
        if (pool == null || pool.Count == 0) return result;

        rng ??= new Random();
        bool hasBoss = !string.IsNullOrWhiteSpace(bossName);

        // 烈度见底且配了 boss：本轮只剩这一场
        if (hasBoss && intensity == 1)
        {
            result.Add(bossName);
            return result;
        }

        var battles = new List<string>();
        var events = new List<string>();
        foreach (string id in pool)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            // 烈度不为 1 时 boss 不参与抽取
            if (hasBoss && id == bossName) continue;
            if (id.StartsWith(EventPrefix, StringComparison.Ordinal)) events.Add(id);
            else battles.Add(id);
        }

        Shuffle(battles, rng);
        Shuffle(events, rng);

        result.AddRange(battles.Take(BattleCount));
        result.AddRange(events.Take(EventCount));

        // 凑不满时用剩余战斗补位。事件已达上限，不再补事件。
        int nextBattle = BattleCount;
        int target = BattleCount + EventCount;
        while (result.Count < target && nextBattle < battles.Count)
            result.Add(battles[nextBattle++]);

        return result;
    }

    /// <summary>Fisher-Yates 原地洗牌</summary>
    private static void Shuffle(List<string> items, Random rng)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
