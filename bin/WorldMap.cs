using Godot;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;




public class Area
{
    /// <summary>
    /// AreaPool.ini 未配置 areaTimes 时使用的默认战斗烈度
    /// </summary>
    public const int DefaultAreaTimes = 3;

    /// <summary>
    /// 这个area的id 比如area1
    /// </summary>
    string areaID = "";

    /// <summary>
    /// 可选项id列表
    /// </summary>
    List<string> entrysName;

    /// <summary>
    /// 该区域的战斗烈度：进入区域时的初始值，每完成一场战斗或事件减少1，归零时解锁下一区域
    /// </summary>
    int areaTimes = DefaultAreaTimes;

    /// <summary>
    /// 该区域的 boss 战斗预设名（AreaPool.ini 的 boss 键），空表示没有 boss。
    /// 有 boss 时：烈度不为1则它不参与抽取，烈度为1则只提供它这一场。
    /// </summary>
    string bossName = "";

    public Area(string id)
    {
        entrysName = [];
        areaID = id;
    }

/// <summary>
/// 读取该区域的 boss 预设名，没有则为空串
/// </summary>
/// <returns></returns>
    public string ReadBoss()
    {
        return bossName;
    }

/// <summary>
/// 设置该区域的 boss 预设名
/// </summary>
/// <param name="value"></param>
    public void SetBoss(string value)
    {
        bossName = value ?? "";
    }

/// <summary>
/// 把一个id添加到列表中
/// </summary>
/// <param name="s"></param>
    public void AddAnEntry(string s)
    {
        entrysName.Add(s);
    }

/// <summary>
/// 读取当前area的entrys
/// </summary>
/// <returns></returns>
    public List<string> ReadEntrys()
    {
        return entrysName;
    }

/// <summary>
/// 读取该区域的战斗烈度
/// </summary>
/// <returns></returns>
    public int ReadAreaTimes()
    {
        return areaTimes;
    }

/// <summary>
/// 设置该区域的战斗烈度，最小为1
/// </summary>
/// <param name="value"></param>
    public void SetAreaTimes(int value)
    {
        areaTimes = Math.Max(1, value);
    }
}



/// <summary>
/// 世界地图界面：显示7个可点击区域，点击后弹出选择任务面板。
/// 区域按顺序解锁（area1 → area2 → ... → area7）。
/// 按 ` 键打开控制台，支持 help / unlockall 等调试指令。
/// </summary>
public partial class WorldMap : Control
{
    private PackedScene _chooseMissionScene;
    private ChooseMission _chooseMissionPanel;

    /// <summary>
    /// 每个区域的敌人池（从AreaPool.ini加载）
    /// </summary>
    private Dictionary<string, Area> _areaPools = new();
    private readonly Dictionary<string, string> _battleNames = new();
    /// <summary>
    /// 区域按钮缓存
    /// </summary>
    private Dictionary<string, TextureButton> _areaButtons = new();

    /// <summary>
    /// 控制台
    /// </summary>
    private Panel _consolePanel;
    /// <summary>
    /// 控制台
    /// </summary>
    private LineEdit _consoleInput;
    /// <summary>
    /// 控制台
    /// </summary>
    private bool _consoleVisible;
    /// <summary>
    /// 控制台
    /// </summary>
    private Label _consoleOutput;

    /// <summary>
    /// 控制台支持的指令
    /// </summary>

    private static readonly string[] WorldMapCommands = new[]
    {
        "help",
        "unlockall",
    };

    /// <summary>
    /// store 卡组等按钮在鼠标悬浮在其上时的缩放比例
    /// </summary>
    private const float HoverScale = 1.08f;

    /// <summary>
    /// store 卡组等按钮在鼠标悬浮在其上时达到对应缩放比例需要的时间
    /// </summary>
    private const float HoverDuration = 0.12f;

    public void _on_deck_pressed()
    {
        var displayDeck = BattleStateManager.BuildDisplayDeck();
        BattleStateManager.ShowDeckViewer(this, displayDeck);
    }

    private Label pointNum;


    public override void _Ready()
    {
        MusicManager.Instance?.PlaySlot("world_map");
        // 启动时立即初始化卡牌数据和卡组（之后所有场景均可直接使用）
        LoadCardDataCache();
        LoadDeck();
        LoadEvents();
        LoadBattleNames();
        LoadAreaPools();
        ConnectAreaButtons();


        pointNum = GetNodeOrNull<Label>("pointNum");
        RefreshMaterialPoint();

        // 预加载选择任务界面
        _chooseMissionScene = ResourceLoader.Load<PackedScene>("res://bin/chooseMission.tscn");

        // 后台预加载大型场景（非阻塞），让玩家浏览地图时在后台完成加载
        SceneLoader.BeginPreload("res://bin/battleField.tscn");
        SceneLoader.BeginPreload("res://bin/worldMap.tscn");
        SceneLoader.BeginPreload("res://store.tscn");

        BattleStateManager.EnsureStoreCardQueue(14);

        ConnectHover("store");
        ConnectHover("deck");

        RefreshAreaStates();
    }

    private void RefreshMaterialPoint()
    {
        pointNum.Text = BattleStateManager.MaterialPoints.ToString();
    }
    private void ConnectHover(string path)
    {
        var button = GetNodeOrNull<Button>(path);
        if (button == null) return;
        button.MouseEntered += () => AnimateButton(button, HoverScale);
        button.MouseExited += () => AnimateButton(button, 1f);
    }

    private static void AnimateButton(Button button, float scale)
    {
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }

    /// <summary>从card.ini加载所有卡牌数据到BattleStateManager缓存中</summary>
    private static void LoadCardDataCache()
    {
        if (BattleStateManager.IsCardDataCached) return;
        var iniPath = "res://cards/card.ini";
        if (!Godot.FileAccess.FileExists(iniPath)) return;
        var content = Godot.FileAccess.Open(iniPath, Godot.FileAccess.ModeFlags.Read).GetAsText();
        var configFile = new IniFile();
        using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
            configFile.Load(stream);
        var items = new Dictionary<string, CardData>();
        foreach (var section in configFile)
        {
            var cd = new CardData();
            cd.Id = section.Key;
            cd.Name = configFile[section.Key]["name"].GetString();
            cd.Description = configFile[section.Key]["description"].GetString();
            cd.Attack = configFile[section.Key]["attack"].ToInt();
            cd.Defense = configFile[section.Key]["defense"].ToInt();
            cd.Cost = configFile[section.Key]["price"].ToInt();
            cd.Effect = configFile[section.Key]["effect"].GetString();
            cd.PlayEffect = GetOptionalValue(configFile[section.Key], "playEffect");
            cd.AttackEffect = GetOptionalValue(configFile[section.Key], "attackEffect");
            cd.IsHq = (HQ)configFile[section.Key]["isHq"].ToInt();
            cd.Rarity = CardParser.GetRarity(configFile[section.Key]["rarity"].GetString());
            cd.IconPath = configFile[section.Key]["icon"].GetString();
            cd.CardType = CardParser.GetTypes(configFile[section.Key]["cardType"].GetString());
            cd.TargetType = CardParser.GetTargetType(configFile[section.Key]["targetType"].GetString());
            cd.Traits = CardParser.GetTraitList(configFile[section.Key]["traits"].GetString());
            items[cd.Id] = cd;
        }
        BattleStateManager.CacheAllCards(items);
    }

    private static string GetOptionalValue(IniSection section, string key)
    {
        return section.TryGetValue(key, out IniValue value) ? value.GetString().Trim() : "";
    }

    public override void _Input(InputEvent @event)
    {
        // 按 ` 键切换控制台，吞掉事件防止字符残留
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Quoteleft)
        {
            ToggleConsole();
            AcceptEvent();
        }
        // Tab 自动补全
        if (@event is InputEventKey tabEvent && tabEvent.Pressed && tabEvent.Keycode == Key.Tab && _consoleVisible)
        {
            HandleAutocomplete();
        }
    }

    /// <summary>
    /// 加载区域任务池配置文件（支持敌人和事件混合条目）
    /// 敌人条目直接存储ID，事件条目以 "event:事件ID" 格式存储
    /// </summary>
    private void LoadAreaPools()
    {
        //首先尝试读文件
        var cachedPools = BattleStateManager.GetCachedAreaPools();
        if (cachedPools != null)
        {
            _areaPools = cachedPools;
            return;
        }

        var iniPath = "res://bin/AreaPool.ini";
        if (!Godot.FileAccess.FileExists(iniPath))
        {
            GD.PushError($"AreaPool.ini not found: {iniPath}");
            return;
        }

        var content = Godot.FileAccess.Open(iniPath, Godot.FileAccess.ModeFlags.Read).GetAsText();
        var configFile = new IniFile();
        using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
        {
            configFile.Load(stream);
        }


        //接下来是语义分析

        foreach (var section in configFile)
        {
            //首先,section的key要和对应的area编码对应
            var areaName = section.Key;

            //初始化一个area 接下来下列程序经过修改,从原来的List改成了由area保存
            var area = new Area(areaName);

            //在这里读取ini里的所有key!
            var keys = configFile.GetSectionKeys(areaName);
            foreach (var key in keys)
            {
                var val = configFile[areaName][key].ToString().Trim();

                //areaTimes 是区域元数据：决定进入该区域时的初始战斗烈度，不作为可抽取任务
                if (key.Equals("areaTimes", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(val, out int times) && times > 0)
                        area.SetAreaTimes(times);
                    else
                        GD.PushError($"[WorldMap] areaTimes 非法，回退默认值 {Area.DefaultAreaTimes}: {areaName}.{key}={val}");
                    continue;
                }

                //boss 是区域元数据：指定该区域的终局战斗，不直接进入可抽取条目列表。
                //它是否出现由 MissionDrawer 按当前烈度决定。
                if (key.Equals("boss", StringComparison.OrdinalIgnoreCase))
                {
                    area.SetBoss(val);
                    continue;
                }

                //只有entry enemy开头的key可以被识别为任务条目
                if (!string.IsNullOrEmpty(val) && (key.StartsWith("entry")||key.StartsWith("enemy")))
                    area.AddAnEntry(val);
            }
                _areaPools[areaName] = area;

        }

        GD.Print($"Loaded area pools: {_areaPools.Count} areas");
        BattleStateManager.CacheAreaPools(_areaPools);
    }

    /// <summary>首次启动时从deck.ini加载卡组并持久化DeckCardIds</summary>
    private static void LoadDeck()
    {
        // 已经初始化过则跳过
        if (BattleStateManager.IsDeckInitialized) return;

        var iniPath = "res://bin/deck.ini";
        if (!Godot.FileAccess.FileExists(iniPath)) return;

        var content = Godot.FileAccess.Open(iniPath, Godot.FileAccess.ModeFlags.Read).GetAsText();
        var configFile = new IniFile();
        var tempPath = OS.GetUserDataDir() + "/temp_deck.ini";
        using (var writer = System.IO.File.CreateText(tempPath))
            writer.Write(content);
        configFile.Load(tempPath);

        if (!configFile.HasSection("deck")) return;

        var deckKeys = configFile.GetSectionKeys("deck");
        foreach (var key in deckKeys)
        {
            string cardIdValue = configFile["deck"][key].GetString();
            int count = 1;
            string actualCardId = cardIdValue;
            if (cardIdValue.Contains("*"))
            {
                string[] parts = cardIdValue.Split("*");
                actualCardId = parts[0].Trim();
                if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int parsedCount))
                    count = parsedCount;
            }
            for (int i = 0; i < count; i++)
                BattleStateManager.DeckCardIds.Add(actualCardId);
        }

        BattleStateManager.IsDeckInitialized = true;
        GD.Print($"[WorldMap] 卡组初始化完成，共{BattleStateManager.DeckCardIds.Count}张卡");
    }

    /// <summary>加载event.ini并缓存到BattleStateManager</summary>
    private static void LoadEvents()
    {
        if (BattleStateManager.IsEventDataCached) return;
        var iniPath = "res://bin/event.ini";
        if (!Godot.FileAccess.FileExists(iniPath)) return;
        var content = Godot.FileAccess.Open(iniPath, Godot.FileAccess.ModeFlags.Read).GetAsText();
        var configFile = new IniFile();
        using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
            configFile.Load(stream);
        var events = new Dictionary<string, EventData>();

        foreach (var section in configFile)
        {
            var ev = new EventData { Id = section.Key };
            ev.Title = configFile[section.Key]["title"].GetString();
            ev.Image = configFile[section.Key]["image"].GetString();
            ev.Description = configFile[section.Key]["description"].GetString();

            for (int i = 1; i <= 4; i++)
            {
                var textKey = $"choice{i}_text";
                var effectKey = $"choice{i}_effect";
                var text = configFile[section.Key][textKey].GetString();
                if (string.IsNullOrEmpty(text)) break;
                ev.Choices.Add(new EventChoice
                {
                    Text = text,
                    Effect = configFile[section.Key][effectKey].GetString()
                });
            }
            events[ev.Id] = ev;
        }

        BattleStateManager.CacheAllEvents(events);
        GD.Print($"Loaded {events.Count} events from event.ini");
    }

    /// <summary>
    /// 自动连接所有area按钮的pressed信号
    /// </summary>
    private void ConnectAreaButtons()
    {
        foreach (var child in GetChildren())
        {
            if (child is TextureButton btn && child.Name.ToString().StartsWith("area"))
            {
                var areaName = child.Name.ToString();
                btn.Pressed += () => OnAreaPressed(areaName);
                _areaButtons[areaName] = btn;
                GD.Print($"Connected area button: {areaName}");
            }
        }
    }

    /// <summary>
    /// 刷新所有区域按钮的锁定/解锁显示状态
    /// </summary>
    private void RefreshAreaStates()
    {
        foreach (var pair in _areaButtons)
        {
            bool unlocked = BattleStateManager.UnlockedArea.TryGetValue(pair.Key, out int value) && value == 1;
            pair.Value.Visible = unlocked;
            pair.Value.Disabled = !unlocked;
        }
    }

    /// <summary>
    /// 点击区域按钮：随机抽取3个不同的任务条目（敌人或事件），弹出选择界面
    /// </summary>
    private void OnAreaPressed(string areaName)
    {
        if (_chooseMissionPanel != null) return;
        if (!BattleStateManager.UnlockedArea.TryGetValue(areaName, out int unlocked) || unlocked != 1) return;

        BattleStateManager.SelectedArea = areaName;

        //从areaPool读取所有值 构成抽取本回合行动的pool

        if (!_areaPools.TryGetValue(areaName, out Area pool))
        {
            GD.Print($"No pool for area: {areaName}");
            return;
        }

        // 首次进入该区域时按 AreaPool.ini 的 areaTimes 初始化战斗烈度
        BattleStateManager.EnsureAreaIntensity(areaName, pool.ReadAreaTimes());

        // 抽取规则集中在 MissionDrawer：按当前烈度处理 boss，并保证 2战斗+1事件
        var candidates = pool.ReadEntrys();
        int intensity = BattleStateManager.ReadAreaIntensity(areaName);
        var selectedIds = MissionDrawer.Draw(candidates, pool.ReadBoss(), intensity);

        if (selectedIds.Count < 1)
        {
            GD.Print($"Not enough entries in pool for {areaName}");
            return;
        }

        // 将选中的ID转换为MissionEntry
        var entries = selectedIds.Select(id => ParseEntry(id)).ToList();

        _chooseMissionPanel = _chooseMissionScene.Instantiate() as ChooseMission;
        if (_chooseMissionPanel != null)
        {
            _chooseMissionPanel.SetEntries(entries, areaName);
            _chooseMissionPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            _chooseMissionPanel.ZIndex = 200;
            AddChild(_chooseMissionPanel);

            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0, 0, 0, 0.75f);
            bg.MouseFilter = MouseFilterEnum.Stop;
            _chooseMissionPanel.AddChild(bg);
            _chooseMissionPanel.MoveChild(bg, 0);
        }

    }

    /// <summary>
    /// 事件完成后关闭三选一面板并刷新区域状态
    /// </summary>
    public void DismissChooseMission()
    {
        if (_chooseMissionPanel != null)
        {
            _chooseMissionPanel.QueueFree();
            _chooseMissionPanel = null;
        }
        RefreshAreaStates();
        RefreshMaterialPoint();
    }

    /// <summary>
    /// 抽取阶段事件按钮上统一显示的文字。刻意不取 event.ini 的 title
    /// （形如 &lt;事件&gt;征召预备役），避免在玩家做出选择前就透露是哪个事件。
    /// </summary>
    private const string EventMaskLabel = "<事件>";

    /// <summary>将池中的ID字符串解析为MissionEntry（"event:xxx"为事件，其余为敌人）</summary>
    private MissionEntry ParseEntry(string id)
    {
        if (id.StartsWith(MissionDrawer.EventPrefix, StringComparison.Ordinal))
        {
            var eventId = id[MissionDrawer.EventPrefix.Length..];
            // 配置写错时仍然报错，但按钮文字保持脱敏，不把事件名带出去
            if (BattleStateManager.GetEvent(eventId) == null)
                GD.PushError($"[WorldMap] 事件配置不存在: {eventId}");
            return new MissionEntry
            {
                Id = eventId,
                DisplayName = EventMaskLabel,
                Type = MissionType.Event
            };
        }
        // 敌人条目
        string enemyName;
        if (!_battleNames.TryGetValue(id, out enemyName) || string.IsNullOrWhiteSpace(enemyName))
        {
            GD.PushError($"[WorldMap] 战斗配置缺少name: {id}");
            enemyName = id;
        }
        return new MissionEntry { Id = id, DisplayName = enemyName, Type = MissionType.Battle };
    }

    private void LoadBattleNames()
    {
        const string path = "res://cards/enemyTurn.ini";
        _battleNames.Clear();
        if (!Godot.FileAccess.FileExists(path))
        {
            GD.PushError($"[WorldMap] 战斗配置不存在: {path}");
            return;
        }
        string content = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read).GetAsText();
        var ini = new IniFile();
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        ini.Load(stream);
        foreach (var section in ini)
        {
            if (section.Value.TryGetValue("name", out IniValue value))
                _battleNames[section.Key] = value.GetString().Trim();
        }
    }

    // 抽取规则已移至 MissionDrawer（boss 分流 + 2战斗+1事件），避免本文件继续膨胀


    // ============================================================
    // 控制台功能
    // ============================================================

    private void ToggleConsole()
    {
        if (_consolePanel == null) CreateConsole();
        _consoleVisible = !_consoleVisible;
        _consolePanel.Visible = _consoleVisible;
        if (_consoleOutput != null) _consoleOutput.Visible = _consoleVisible;
        if (_consoleVisible)
        {
            _consoleInput.Clear();
            _consoleInput.GrabFocus();
        }
    }


/// <summary>
/// 初始化控制台,未来考虑废弃,改为场景
/// </summary>
    private void CreateConsole()
    {
        // 输出面板（控制台上方，显示多行帮助等信息）
        _consoleOutput = new Label();
        _consoleOutput.Visible = false;
        _consoleOutput.Position = new Vector2(50, 50);
        _consoleOutput.Size = new Vector2(600, 0);
        _consoleOutput.ZIndex = 1001;
        _consoleOutput.AddThemeColorOverride("font_color", Colors.LimeGreen);
        _consoleOutput.AddThemeFontSizeOverride("font_size", 13);
        _consoleOutput.Text = "";
        AddChild(_consoleOutput);

        // 输入面板
        _consolePanel = new Panel();
        _consolePanel.Visible = false;
        _consolePanel.Position = new Vector2(50, 10);
        _consolePanel.Size = new Vector2(600, 36);
        _consolePanel.ZIndex = 1000;
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.85f);
        _consolePanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_consolePanel);

        _consoleInput = new LineEdit();
        _consoleInput.SetAnchorsPreset(LayoutPreset.FullRect);
        _consoleInput.AddThemeColorOverride("font_color", Colors.LimeGreen);
        _consoleInput.AddThemeFontSizeOverride("font_size", 14);
        _consoleInput.PlaceholderText = "输入指令，回车执行。输入 help 查看可用指令...";
        _consoleInput.TextSubmitted += OnConsoleSubmit;
        _consolePanel.AddChild(_consoleInput);
    }


/// <summary>
/// 控制台语义分析
/// </summary>
/// <param name="text"></param>
    private void OnConsoleSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string cmd = text.Trim().ToLowerInvariant();
        _consoleInput.Text = "";

        switch (cmd)
        {
            case "help":
                _consoleOutput.Text =
                    "=== WorldMap 控制台指令 ===\n" +
                    "  help        显示此帮助信息\n" +
                    "  unlockall   解锁所有区域（area1 ~ area7）\n" +
                    "================================\n" +
                    $"当前开放区域: {string.Join(", ", BattleStateManager.UnlockedArea.Where(pair => pair.Value == 1).Select(pair => pair.Key))}";
                _consoleOutput.Visible = true;
                break;

            case "unlockall":
                BattleStateManager.UnlockAllAreas();
                RefreshAreaStates();
                _consoleOutput.Text = "已解锁全部区域 (area1 ~ area7)";
                _consoleOutput.Visible = true;
                GD.Print("All areas unlocked via console");
                break;

            default:
                _consoleOutput.Text = $"未知指令: {cmd}\n输入 help 查看可用指令";
                _consoleOutput.Visible = true;
                break;
        }
    }
    private void HandleAutocomplete()
    {
        if (_consoleInput == null) return;

        string currentText = _consoleInput.Text;
        if (string.IsNullOrEmpty(currentText)) return;

        var matches = WorldMapCommands
            .Where(c => c.StartsWith(currentText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) return;

        // 如果有多个匹配，找出最长公共前缀
        if (matches.Count == 1)
        {
            _consoleInput.Text = matches[0];
        }
        else
        {
            string commonPrefix = currentText;
            while (commonPrefix.Length < matches[0].Length)
            {
                char next = matches[0][commonPrefix.Length];
                if (matches.All(m => m.Length > commonPrefix.Length && m[commonPrefix.Length] == next))
                    commonPrefix += next;
                else
                    break;
            }
            _consoleInput.Text = commonPrefix;
        }
        _consoleInput.CaretColumn = _consoleInput.Text.Length;
    }


/// <summary>
/// 当store按钮被按下时,弹出商店界面,注意是叠加显示的
/// </summary>
    void _on_store_pressed()
    {
        var storeScene = ResourceLoader.Load<PackedScene>("res://store.tscn");
        var store = storeScene.Instantiate() as Store;
        if (store == null) return;

        var canvasLayer = new CanvasLayer();
        canvasLayer.Layer = 10;
        AddChild(canvasLayer);
        canvasLayer.AddChild(store);
    }
}
