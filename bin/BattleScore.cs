/// <summary>
/// 战斗评分规则——物资点结算系数的唯一来源。
///
/// <para>
/// <c>battlefield_.CalculateMaterialPoints()</c> 用这里的函数算总额，
/// 结算面板用它渲染每一行明细。两边共用同一组函数，因此
/// 「明细相加 = 总额」由代码结构保证，而不是靠在两处同步同一组数字。
/// </para>
///
/// <para>
/// 历史上系数曾在计算与显示两处各写一遍，改动其中一处就会出现
/// 面板明细与底部总额对不上的情况。调整平衡时只改本文件的常量。
/// </para>
/// </summary>
public static class BattleScore
{
    /// <summary>每击杀一个敌方陆军单位获得的物资点</summary>
    public const int LandKillPoints = 3;

    /// <summary>每击杀一个敌方空军单位获得的物资点</summary>
    public const int AirKillPoints = 4;

    /// <summary>总部防御每损失这么多点，扣 1 点物资</summary>
    public const int HqDefencePerPenalty = 2;

    /// <summary>
    /// 对敌方总部每造成这么多点伤害，得 1 点物资。
    ///
    /// <para>
    /// 没有这一项时，击杀是唯一的得分来源，于是「敌方不派兵、只是不断加固总部」
    /// 的关卡（如加里宁）无论打得多好都必然结算为 0——玩家要打穿的不仅是总部
    /// 那 40 点防御，还有它每回合回的血。
    /// </para>
    /// </summary>
    public const int EnemyHqDamagePerPoint = 5;

    public static int LandPoints(int landKilled) => landKilled * LandKillPoints;

    public static int AirPoints(int airKilled) => airKilled * AirKillPoints;

    public static int DeadPenalty(int friendlyDead) => friendlyDead < 0 ? 0 : friendlyDead;

    public static int HqPenalty(int hqDefenceLost)
        => hqDefenceLost <= 0 ? 0 : hqDefenceLost / HqDefencePerPenalty;

    public static int EnemyHqDamagePoints(int enemyHqDamage)
        => enemyHqDamage <= 0 ? 0 : enemyHqDamage / EnemyHqDamagePerPoint;

    /// <summary>
    /// 本场战斗获得的物资点。下限为 0——表现差不会倒扣已有物资。
    /// </summary>
    /// <param name="enemyHqDamage">对敌方总部累计造成的伤害（含被其治疗抵消的部分）</param>
    public static int Total(int landKilled, int airKilled, int friendlyDead, int hqDefenceLost, int enemyHqDamage)
    {
        int gained = LandPoints(landKilled) + AirPoints(airKilled) + EnemyHqDamagePoints(enemyHqDamage)
                   - DeadPenalty(friendlyDead) - HqPenalty(hqDefenceLost);
        return gained < 0 ? 0 : gained;
    }
}
