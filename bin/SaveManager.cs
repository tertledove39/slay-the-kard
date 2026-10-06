using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 存档 / 读档。**单存档位**，落在 `user://save.cfg`（Godot 的 `ConfigFile`，
/// 与 `SettingsManager` 的 `user://settings.cfg` 同一套路）。
///
/// 存的是 <see cref="BattleStateManager"/> 里的**本局进度**，外加一个战斗 id
/// （`SelectedEnemy`，如 `berlin`）——读档时靠它决定「直接进这场战斗」还是「回世界地图」：
///
/// | 存档时的处境 | `SelectedEnemy` | `InBattle` | 读档后 |
/// |---|---|---|---|
/// | 战斗中按「保存并退出」 | 那一场的敌人预设 | `1` | **直接重新进这场战斗** |
/// | 世界地图按「保存并退出」 | 上一次的残留值 | `0` | 回世界地图 |
///
/// **战斗内的棋盘不还原**（手牌、场上单位、指挥点）：存档只记 id，读档是**重新打这一场**。
///
/// 目录/卡组/烈度这些「配置」不进存档——它们由 `card.ini` / `deck.ini` / `AreaPool.ini`
/// 决定，每次开局重新解析（见 `BattleStateManager.IsCardDataCached`）。
/// </summary>
public static class SaveManager
{
    private const string SavePath = "user://save.cfg";
    private const string ProgressSection = "progress";
    private const string AreaSection = "area";
    private const string IntensitySection = "intensity";
    private const string StoreSection = "store";

    /// <summary>存档格式版本。以后字段变了靠它判断要不要丢弃旧档。</summary>
    private const int Version = 1;

    public static bool HasSave() => Godot.FileAccess.FileExists(SavePath);

    /// <summary>把当前进度写盘。写失败只报警、不抛出——调用方多半正在切场景。</summary>
    public static void Save()
    {
        var config = new ConfigFile();

        config.SetValue(ProgressSection, "version", Version);
        config.SetValue(ProgressSection, "enemy", BattleStateManager.SelectedEnemy ?? "");
        config.SetValue(ProgressSection, "area", BattleStateManager.SelectedArea ?? "");
        config.SetValue(ProgressSection, "inBattle", BattleStateManager.IsCampaignMode ? 1 : 0);
        config.SetValue(ProgressSection, "hp", BattleStateManager.Hp);
        config.SetValue(ProgressSection, "points", BattleStateManager.MaterialPoints);
        config.SetValue(ProgressSection, "deck", Join(BattleStateManager.DeckCardIds));

        foreach (var pair in BattleStateManager.UnlockedArea)
            config.SetValue(AreaSection, pair.Key, pair.Value);

        foreach (var pair in BattleStateManager.ReadAllAreaIntensity())
            config.SetValue(IntensitySection, pair.Key, pair.Value);

        // 商店：库存队列 + 当前 7 个货架（含折扣与已售状态）
        config.SetValue(StoreSection, "queue", Join(BattleStateManager.StoreCardQueue));
        var slots = BattleStateManager.StoreCurrentSlots;
        config.SetValue(StoreSection, "slotCount", slots?.Count ?? 0);
        for (int i = 0; i < (slots?.Count ?? 0); i++)
        {
            var slot = slots[i];
            config.SetValue(StoreSection, $"slot{i}", $"{slot.CardId}|{slot.OriginalPrice}|{(slot.IsDiscounted ? 1 : 0)}|{(slot.IsSold ? 1 : 0)}");
        }

        Error error = config.Save(SavePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} SaveManager.cs: 存档写入失败 {error}");
            return;
        }
        GD.Print($"[Save] 已存档：敌人={BattleStateManager.SelectedEnemy} 区域={BattleStateManager.SelectedArea} "
               + $"血量={BattleStateManager.Hp} 物资={BattleStateManager.MaterialPoints} 卡组={BattleStateManager.DeckCardIds.Count} 张");
    }

    /// <summary>把存档读回 <see cref="BattleStateManager"/>。返回 false 表示没有档或档读不了。</summary>
    public static bool Load()
    {
        if (!HasSave()) return false;

        var config = new ConfigFile();
        if (config.Load(SavePath) != Error.Ok)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} SaveManager.cs: 存档读取失败 {SavePath}");
            return false;
        }

        int version = (int)config.GetValue(ProgressSection, "version", 0).AsInt64();
        if (version != Version)
        {
            // 版本对不上就当作没有档：宁可让玩家重开一局，也不要用半截数据把状态弄坏。
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} SaveManager.cs: 存档版本 {version} ≠ {Version}，已忽略");
            return false;
        }

        BattleStateManager.SelectedEnemy = config.GetValue(ProgressSection, "enemy", BattleStateManager.DefaultEnemyPreset).AsString();
        BattleStateManager.SelectedArea = config.GetValue(ProgressSection, "area", "").AsString();
        BattleStateManager.IsCampaignMode = config.GetValue(ProgressSection, "inBattle", 0).AsInt64() != 0;
        BattleStateManager.Hp = (int)config.GetValue(ProgressSection, "hp", BattleStateManager.InitialHp).AsInt64();
        BattleStateManager.MaterialPoints = (int)config.GetValue(ProgressSection, "points", 0).AsInt64();

        BattleStateManager.DeckCardIds.Clear();
        BattleStateManager.DeckCardIds.AddRange(Split(config.GetValue(ProgressSection, "deck", "").AsString()));
        // 卡组已经由存档给出，别再让 deck.ini 覆盖一遍
        BattleStateManager.IsDeckInitialized = true;

        foreach (string area in BattleStateManager.UnlockedArea.Keys.ToList())
            BattleStateManager.UnlockedArea[area] = (int)config.GetValue(AreaSection, area, 0).AsInt64();

        var intensity = new Dictionary<string, int>();
        if (config.HasSection(IntensitySection))
        {
            foreach (string key in config.GetSectionKeys(IntensitySection))
                intensity[key] = (int)config.GetValue(IntensitySection, key, 0).AsInt64();
        }
        BattleStateManager.RestoreAreaIntensity(intensity);

        BattleStateManager.StoreCardQueue.Clear();
        foreach (string cardId in Split(config.GetValue(StoreSection, "queue", "").AsString()))
            BattleStateManager.StoreCardQueue.Enqueue(cardId);

        int slotCount = (int)config.GetValue(StoreSection, "slotCount", 0).AsInt64();
        if (slotCount > 0)
        {
            var slots = new List<BattleStateManager.StoreSlot>();
            for (int i = 0; i < slotCount; i++)
            {
                string[] parts = config.GetValue(StoreSection, $"slot{i}", "").AsString().Split('|');
                if (parts.Length < 4) continue;
                slots.Add(new BattleStateManager.StoreSlot
                {
                    CardId = parts[0],
                    OriginalPrice = int.TryParse(parts[1], out int price) ? price : 0,
                    IsDiscounted = parts[2] == "1",
                    IsSold = parts[3] == "1",
                });
            }
            BattleStateManager.StoreCurrentSlots = slots;
        }
        else
        {
            BattleStateManager.StoreCurrentSlots = null;
        }

        GD.Print($"[Save] 已读档：敌人={BattleStateManager.SelectedEnemy} 区域={BattleStateManager.SelectedArea} "
               + $"血量={BattleStateManager.Hp} 物资={BattleStateManager.MaterialPoints} 卡组={BattleStateManager.DeckCardIds.Count} 张");
        return true;
    }

    /// <summary>删掉存档（「放弃」与「覆盖」都走它）。没有档时什么都不做。</summary>
    public static void Delete()
    {
        if (!HasSave()) return;
        Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
        if (error != Error.Ok)
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} SaveManager.cs: 删档失败 {error}");
        else
            GD.Print("[Save] 存档已删除");
    }

    /// <summary>
    /// 读档后该进哪个场景：存档里带着「当时在哪」。
    /// `IsCampaignMode` 为真表示**在战斗中存的**，直接回到那一场；否则回世界地图。
    /// </summary>
    public static string ResolveScenePath()
        => BattleStateManager.IsCampaignMode ? "res://bin/battleField.tscn" : "res://bin/worldMap.tscn";

    private static string Join(IEnumerable<string> values)
        => values == null ? "" : string.Join(",", values.Where(v => !string.IsNullOrWhiteSpace(v)));

    private static List<string> Split(string raw)
        => string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
}
