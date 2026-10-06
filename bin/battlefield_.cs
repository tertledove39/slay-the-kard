using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;
using System.Text;
using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;



public partial class battlefield_ : Control
{

/// <summary>
/// 是否允许控制输入 0 允许 1 禁止
/// </summary> 
    int allowControl = 0;

    /// <summary>
    /// 控制锁的嵌套层数。ForbidControl / AllowControl 会嵌套调用
    /// （敌方回合内的每次 Attack 都是一层），内层解除不得推翻外层的禁止。
    /// </summary>
    int controlLockDepth = 0;

    /// <summary>
    /// 是否暂停死亡检查 0 不暂停 1 暂停
    /// </summary>
    int pauseDeathCheck = 0;
    private bool deathCheckRunning = false;
    private bool deathCheckRequested = false;
    private bool turnTransitionRunning = false;

    /// <summary>
    /// 允许控制输入。只在最外层解除时才真正解锁。
    /// 内层调用必须被忽略：Attack() 结尾会无条件调用本函数，而敌方回合里
    /// 每次 Attack 都嵌套在 EnemyTurnAsync / OnNextTurnButtonPressed 的禁止之下，
    /// 若直接解锁，Next 按钮会在每一次敌方行动后闪一下可点击状态。
    /// </summary>
    void AllowControl()
    {
        if (controlLockDepth > 0)
            controlLockDepth--;
        if (controlLockDepth > 0)
            return;

        allowControl = 0;
        buttonNextTurn.Disabled = false;
    }

    /// <summary>
    /// 禁止控制输入
    /// </summary>
    void ForbidControl()
    {
        controlLockDepth++;
        allowControl = 1;
        buttonNextTurn.Disabled = true;
    }

    /// <summary>
    /// 读取当前是否允许控制输入 是否允许控制输入 0 允许 1 禁止
    /// </summary>
    int ReadControlState()
    {
        return allowControl;
    }

    /// <summary>
    /// 暂停死亡检查
    /// </summary>
    void PauseDeathCheck()
    {
        pauseDeathCheck = 1;
    }

    /// <summary>
    /// 恢复死亡检查
    /// </summary>
    void ResumeDeathCheck()
    {
        pauseDeathCheck = 0;
    }

    /// <summary>
    /// 读取死亡检查状态
    /// </summary>
    int ReadDeathCheckState()
    {
        return pauseDeathCheck;
    }

    /// <summary>
    /// 全局 储存所有场上的卡
    /// </summary>
    private List<cardBase_> cardInPlaces = new List<cardBase_>();
    public bool _displayOrderDirty = true;
    private VBoxContainer _enemyIntentContainer;
    private int _battleEnemyLandKilled = 0;
    private int _battleEnemyAirKilled = 0;
    private int _battleFriendlyDead = 0;
    private int _battleHqDefenceLost = 0;
    private int _battleHqDefenceStart = 0;

    private CanvasLayer choiceLayer;
    private ColorRect choiceDim;
    private HBoxContainer choiceContainer;
    private List<cardBase_> choiceCards = new List<cardBase_>();

    /// <summary>
    /// 关卡开局效果，取自 `enemyTurn.ini` 该关 section 里可选的 `battleStart=` 键。
    /// 这是「游戏开始时，若 xxx 则 xxx」的通用入口，详见 RunBattleStartEffectAsync。
    /// </summary>
    private string _battleStartEffect = "";

    /// <summary>`enemyTurn.ini` 里关卡开局效果的键名。改动此处即改动配置写法。</summary>
    private const string BattleStartKey = "battleStart";

    /// <summary>撤退结算后、交给死亡检查前的缓冲（毫秒）。</summary>
    private const int RetreatSettleDelayMs = 100;

    /// <summary>阵亡爆炸音效所在的音效槽位名，对应 configs/music.ini 的 [sfx] 段。</summary>
    private const string DeadSfxSlot = "dead";

    /// <summary>
    /// `attackEffect` / `playEffect` 里写多个特效名时的分隔符。
    /// 与 `traits` 等卡牌多值字段一致用英文逗号。
    /// </summary>
    private static readonly char[] EffectNameSeparator = { ',' };

    /// <summary>
    /// 多张单位卡被同时弃置时，卡与卡之间错开的起飞间隔（秒）。
    /// `cardBase_.DiscardCard()` 单张就要 3.5 秒（飞入1 + 停留1 + 飞出1.5），
    /// 若一张播完再播下一张，3 张就要 10 秒。错开起飞后动画互相重叠，单张观感不变。
    /// </summary>
    private const float DiscardStaggerSeconds = 0.5f;

    /// <summary>
    /// 单位阵亡之后，卡在场上**停留多久**才消失并冒烟、爆炸（秒）。
    ///
    /// 防御力归零的那一瞬间就消失，观感是「中弹」和「爆炸」挤在同一帧里，
    /// 看不清发生了什么。留一拍之后，玩家能先看到血条归零、再看到那声爆炸。
    /// </summary>
    private const float DeathPresentationDelaySeconds = 1f;

    /// <summary>
    /// **指令卡**没在 `cards/card.ini` 里写 `playEffect` 时，默认播的效果（一声「咚」）。
    ///
    /// 做成默认值而不是在那 86 张卡上各写一行：那是同一个值抄 86 遍（规范 E），
    /// 而且以后每加一张指令卡都要记得补——漏了就静默没声，没人会发现。
    ///
    /// 卡上写了自己的 `playEffect`（那 8 张带语音的）就以卡为准，这条不生效。
    /// 单位卡不走这里（部署音是另一回事）。
    /// 音效文件本身仍只配在 `configs/music.ini` 的 `[sfx]` 段：`咚=res://assest/咚.wav`。
    /// </summary>
    private const string DefaultCommandPlayEffect = "sfx(咚)";

    /// <summary>
    /// 地面单位进场音的档位边界，按 `attack + defence` 分（需求方给的规则）：
    /// ≤ `SmallMax` 小 / 到 `MediumMax` 为止是中 / 再往上是大。步兵与坦克/火炮共用这套边界。
    /// </summary>
    private const int DeploySoundSmallMax = 4;
    private const int DeploySoundMediumMax = 8;

    /// <summary>
    /// 档位名，与 `[sfx]` 槽位里的后缀逐字一致（`infantry_small`、`artillery_large_fire`…）。
    /// 写成常量是为了让「档位」这个词在代码里只有一种拼法。
    /// </summary>
    private const string TierSmall = "small";
    private const string TierMedium = "medium";
    private const string TierLarge = "large";

    /// <summary>
    /// 进场音的**槽位前缀**，与档位后缀拼成 `[sfx]` 里的槽位名（如 `infantry_small`、`tank_large`）。
    /// 步兵与坦克/火炮各一套三档素材，所以前缀不同；音频文件配在 `configs/music.ini` 的 `[sfx]` 段。
    /// </summary>
    private const string InfantryVoicePrefix = "infantry";
    private const string TankVoicePrefix = "tank";

    /// <summary>
    /// **飞机（战斗机 `Plane` 与轰炸机 `Bomber`）**的两条兜底音。
    ///
    /// 素材是按**时机**给的（`depl` = 部署、`flyby` = 移动），所以**部署与移动是两条不同的槽位**，
    /// 两组不通用。每组的多个文件配在 `[sfx]` 段的同一行里（逗号分隔 = 每次随机抽一条），
    /// 所以「同类型随机播放」不用写任何抽签代码。
    ///
    /// **两种飞机行为完全一样**，所以按时机命名而不是按兵种——槽位名与素材文件名一一对应
    /// （`AU_depl_Fighter_*` → `plane_deploy`、`AU_Flyby_Fighter_small_v2_*` → `plane_flyby`）。
    /// </summary>
    private const string PlaneDeployEffect = "sfx(plane_deploy)";
    private const string PlaneFlybyEffect = "sfx(plane_flyby)";

    /// <summary>
    /// 抽一张牌时的那一声。`[sfx]` 段的 `draw` 槽位写了 5 条变体（`Draw_One_A`~`E`），
    /// 该段本来就是「逗号分隔、每次随机抽一条」，所以随机由 `MusicManager` 负责，这里不用管。
    /// </summary>
    private const string DrawSoundEffect = "sfx(draw)";

    /// <summary>
    /// **坦克与火炮**没在 `cards/card.ini` 里写 `attackEffect` 时，默认打的那一发炮弹。
    /// 卡上写了就以卡为准（喀秋莎写了自己的 `bullet,sfx(katyusha_fire)`）。
    /// 做成默认值而不是在 28 张卡上各写一行，理由与进场音那几处一样（规范 E）。
    ///
    /// 这只是**名字那一段**：真正写进特效串的是 `TankAttack(命中音槽位)`，见 `TankAttackEffectFor`。
    /// </summary>
    private const string TankAttackName = "TankAttack";

    /// <summary>
    /// **飞机**没写 `attackEffect` 时的默认攻击动画——两种飞机各一条，都是「飞掠 + 打一下」：
    ///
    /// | 兵种 | 默认 | 表现 |
    /// |------|------|------|
    /// | 战斗机 `Plane` | `strafe` | 起飞即**扫射**（10 发子弹）→ 悬停 → 降落 |
    /// | 轰炸机 `Bomber` | `airstrike` | 起飞即**投弹**（弹数 = 攻击力）→ 悬停 → 降落 |
    ///
    /// 这两条本来是毛驴与伊尔2M 各自卡上的配置，现在提升成兵种默认：13 张飞机卡
    /// 一个 `attackEffect` 都不用写，新加的飞机也自动有。
    ///
    /// **开火声会跟着变，而且是两样的**：`strafe` **不在** `NoFiringSoundNames` 里
    /// （它打的就是子弹，那声机枪正是要的），`airstrike` 在名单里（炸弹配机枪是串味）。
    /// 改这两条默认值时记得连 `EffectRegistry.NoFiringSoundNames` 一起看。
    /// </summary>
    private const string PlaneStrikeEffect = "strafe";
    private const string BomberStrikeEffect = "airstrike";

    /// <summary>
    /// 坦克炮的 `[sfx]` 槽位。**开火音只有中/大两套素材，小的也归 medium**（需求方指定）；
    /// 命中音完全不分档。
    /// </summary>
    private const string TankCannonMediumFireSlot = "tank_cannon_medium";
    private const string TankCannonLargeFireSlot = "tank_cannon_large";
    private const string TankCannonImpactSlot = "tank_cannon_impact";

    /// <summary>
    /// 火炮的 `[sfx]` 槽位：`artillery_{档}_fire` 与 `artillery_{档}_impact`。
    /// 这套素材三档是齐的，所以**不分档的例外一个都没有**——与坦克不同。
    /// </summary>
    private const string ArtillerySlotPrefix = "artillery";
    private const string FireSlotSuffix = "fire";
    private const string ImpactSlotSuffix = "impact";

    /// <summary>
    /// 自定义内存变量：在当前战场场景中持久保存
    /// </summary>
    private Dictionary<string, int> memoryVariables = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 读取场上所有卡构成的列表
    /// </summary>
    /// <returns></returns>
    public List<cardBase_> ReadCardInPlaces()
    {
        return cardInPlaces;
    }

    /// <summary>
    /// 设置场上所有卡所构成的列表 慎用!
    /// </summary>
    /// <param name="value"></param>
    public void SetCardInPlaces(List<cardBase_> value)
    {
        // 保证 cardInPlaces 永远不会为 null，避免 Sort/Refresh 抛出异常
        cardInPlaces = value ?? new List<cardBase_>();
    }

    /// <summary>
    /// 支援阵线
    /// </summary>
    List<place_> supportLine = [];

    /// <summary>
    /// 前线
    /// </summary>
    List<place_> frontLine = [];

    /// <summary>
    /// 上一个被加入支援阵线的卡牌引用
    /// </summary>
    private cardBase_ lastCardAddedToSupportLine = null;
    private cardBase_ lastCardAddedToHand = null;
    private List<cardBase_> lastCardsShuffledIntoDeck = new();
    /// <summary>
    /// 上次攻击溢出的伤害
    /// </summary>
    private int lastOverflowDamage = 0;
    private int lastDamage = 0;
    /// <summary>
    /// 上一个死亡的友方陆军单位的id
    /// </summary>
    private string lastDeadFriendlyLandUnitId = "";

    /// <summary>
    /// 敌方阵线
    /// </summary>
    List<place_> enemySupprotLine = [];
    List<place_> allPlaces = [];
    
    // 敌方行动队列：key为回合数或"ADD"，value为效果字符串列表
    Dictionary<string, List<string>> enemyActionQueue = new Dictionary<string, List<string>>();

/// <summary>
/// 根据id返回一个place的引用
/// </summary>
/// <param name="id"></param>
/// <returns></returns>
    public place_ GetPlaceById(string id)
    {
        foreach(var place in allPlaces)
        {
            if(place.Name == id)
            {
                return place;
            }
        }
        return null;
    }

    /// <summary>
    /// 获得上一个被加入支援阵线的卡的引用
    /// </summary>
    public cardBase_ GetCardBeingAddToSupportLine()
    {
        return lastCardAddedToSupportLine;
    }

    

/// <summary>
/// 给定一个若干张卡组成的列表 重新按照从上到下从左到右排序
/// </summary>
/// <param name="list"></param>
/// <returns></returns>
    List<cardBase_> SortCardList(List<cardBase_> list)
    {
        if (list == null)
            return new List<cardBase_>();

        // 过滤掉 null 以避免 OrderBy 抛出异常
        var safeList = list.Where(c => c != null).ToList();

        var sortedCards = safeList
            .OrderBy(c => c.GlobalPosition.Y)   // 先按 Y（行）
            .ThenBy(c => c.GlobalPosition.X)    // 再按 X（列）
            .ToList();
        return sortedCards;
    }


/// <summary>
/// 重新按照顺序显示卡
/// </summary> 
    void RefreshAllCardDisplayOrder()
    {
        if (cardInPlaces == null || cardInPlaces.Count == 0)
            return;

        cardInPlaces = SortCardList(cardInPlaces).AsEnumerable().Reverse().ToList();

        if (cardNowChoose != null && cardNowChoose.GetParent() == this)
        {
            // 移到子节点末尾确保渲染在最上层
            MoveChild(cardNowChoose, GetChildCount() - 1);
            cardNowChoose.ZIndex = 100;
        }

        if (cardInPlaces != null)
        {
            foreach (var card in cardInPlaces)
            {
                if (card == null)
                    continue;

                if (card == cardNowChoose)
                    continue;

                // 弃牌动画中的卡不参与Z-index/子节点顺序重置；
                // 正在播卡牌自身特效（flying）的卡同理——那会儿它的层级是特效在管。
                if (card.isDiscarding || card.isUnderCardEffect)
                    continue;

                // 跳过已临时Reparent到其他节点的卡（如ShowCardChoice期间）
                if (card.GetParent() != this)
                    continue;

                MoveChild(card, 1);

                if (isShowingChoiceUI && choiceCards != null && choiceCards.Contains(card))
                {
                    card.ZIndex = 40;
                }
                else
                {
                    card.ZIndex = 10;
                }
            }
        }

        var handCards = player1.GetCardsInHand();
        int hoveredIdx = player1.GetHoveredHandIndex();
        for (int i = 0; i < handCards.Count; i++)
        {
            // 跳过已临时Reparent到其他节点的卡（如起手换牌界面期间）。
            // 手牌仍在 cardsInHand 里但父节点已不是本节点，直接 MoveChild 会报
            // "Child is not a child of this node"。上面的 cardInPlaces 循环有同样守卫。
            if (handCards[i] == null || handCards[i].GetParent() != this)
                continue;

            MoveChild(handCards[i], 1);
            handCards[i].ZIndex = (i == hoveredIdx) ? 30 : 20;
        }
    }

    /// <summary>
    /// 检查指定的格子是否已经被占用
    /// </summary>
    /// <param name="place"></param>
    /// <returns></returns>
    Boolean CheckIfThePlaceIsOccupied(place_ place)
    {
        if(place.GetMyCard()!= null)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

/// <summary>
/// 将一张卡牌加入到指定位置 如果这张卡一开始不在场上 那就初始化这张卡
/// </summary>
/// <param name="card"></param>
/// <param name="place"></param>
/// <returns></returns>
    async Task AddCardToPlace(cardBase_ card, place_ place)
    {
        if (place.GetMyCard()!= null) return;
        PauseDeathCheck(); // 暂停死亡检查
        if (!cardInPlaces.Contains(card))
        {
            AddToBattleField(card);
        }
        card.SetMyPlace(place);
        card.setState(CardState.placed);
        //RefreshAllCardDisplayOrder();
        await card.MoveToPosition(place.GetPlaceGlobalPosition());
        PlayCardEffect(card);

        // 重置Z-index，防止新部署单位始终在最前
        card.ZIndex = 10;

        // 闪击特性：单位被加入战场时刷新
        if(card.HasTrait(UnitTraits.Blitz)) card.RefreshUnit();

        // 前线单位无法具有烟幕
        if (frontLine.Contains(place) && card.HasSmokeScreenActive())
        {
            card.RemoveSmokeScreen();
        }

                      // 触发被加入战场的效果
        await TriggerUnitEffects("BeingAddedToField", card, new List<cardBase_>(), checkOnlySourceCard: true);

        // 触发友方单位入场效果
        if (card.GetIsFriend() == IsFriend.friend && card.isHq != HQ.hq)
        {
            await TriggerUnitEffects("FriendlyUnitEnteringField", card, new List<cardBase_> { card }, checkOnlySourceCard: true);
        }

        ResumeDeathCheck(); // 恢复死亡检查
        await CheckIfAnyUnitDiedAsync(); // 检查死亡
        RefreshAllBeGuardianedStatus(); // 部署后刷新被守护状态
    }

/// <summary>
/// 返回给定全局坐标应当返回的第一张卡
/// </summary>
/// <returns></returns>
    cardBase_ CheckCardClick(Godot.Vector2 mousePosition)
    {

        if(cardInPlaces== null) return null;
        var aa = cardInPlaces.AsEnumerable().ToList();
        foreach (var card in aa)
        {
            // 阵亡后暂留在场上的卡点不中：它已经死了，只是还没消失。
            // 放它过去的话，cardNowChoose 会变成那张尸卡，层级还会被抬到 100。
            if (CanBeSelected(card) && card.GetGlobalRect().HasPoint(mousePosition))
            {
                // 点击到了卡牌
                return card;
            }
        }
        return null;
    }

/// <summary>
/// 外部方法 将一张卡加入到战场全局
/// </summary>
/// <param name="card"></param>
    public void AddToBattleField(cardBase_ card)
    {
        AddChild(card);
        cardInPlaces.Add(card);
        _displayOrderDirty = true;
    }

/// <summary>
/// 获得对应位置的格点
/// </summary>
/// <param name="GlobalPosition"></param>
    public place_ GetPlaceWithPosition(Vector2 globalPosition)
    {
        foreach (var place in allPlaces)
        {
            if (place.GetNode<Control>("Control").GetGlobalRect().HasPoint(globalPosition))
            {
                return place;
            }
        }
        return null;
    }

Player player1;
Player player2;
    private cardBase_ myHq;

    /// <summary>左上角的设置按钮（场景里的 `SettingsButton`）。与 ESC 走同一个入口。</summary>
    private Button settingsButton;

    /// <summary>
    /// 检查卡牌是否在合法区域内
    /// </summary>
    Control validArea;
Marker2D arrowFrom; 
CardMaganer cardMaganer = new();




/// <summary>
/// 返回卡牌加载器
/// </summary>
/// <returns></returns> 
public CardMaganer GetCardMaganer()
    {
        return cardMaganer;
    }


AudioStreamPlayer battleSound;
AudioStreamPlayer deadSound;
TextureButton buttonNextTurn;
private bool defeatTransitionStarted;

/// <summary>起手抽牌张数，也是换牌阶段平铺在屏幕前的牌数</summary>
private const int OpeningHandSize = 5;

/// <summary>
/// 初始化
/// </summary>
    public override void _Ready()
    {
        // 战斗专属BGM优先：music.ini 中配置了 battleBGM_<敌人预设名> 就用它，否则回退到通用 battle 槽位
        MusicManager.Instance?.PlayBattleSlot(BattleStateManager.ResolveEnemyPreset());
        GD.Print("_Ready method called");

        // 资源管理器：缓存纹理/场景并维护空卡牌池（同步初始化，一次性完成）
        var resourceManager = new ResourceManager();
        AddChild(resourceManager);
        resourceManager.Initialize();
        var effectPool = new BattleEffectPool();
        AddChild(effectPool);
        effectPool.Prewarm(resourceManager);

        CreateChoiceOverlay();

        // 保证卡牌列表立刻可用，避免 Sort/Refresh 时出现 null
        cardInPlaces = new List<cardBase_>();
        //初始化各个阵线
        enemySupprotLine = [(place_)GetNode("Place1"), (place_)GetNode("Place2"), (place_)GetNode("Place3"), (place_)GetNode("Place4"), (place_)GetNode("Place5")];
        frontLine = [(place_)GetNode("Place6"), (place_)GetNode("Place7"), (place_)GetNode("Place8"), (place_)GetNode("Place9"), (place_)GetNode("Place10")];
        supportLine = [(place_)GetNode("Place11"), (place_)GetNode("Place12"), (place_)GetNode("Place13"), (place_)GetNode("Place14"), (place_)GetNode("Place15")];
        allPlaces = [.. enemySupprotLine, .. frontLine, .. supportLine];

        deadSound = GetNode<AudioStreamPlayer>("deadSound");
        battleSound = GetNode<AudioStreamPlayer>("battleSound");
        
        //初始化打牌判定区域
        validArea = GetNode<Control>("validCardArea");

        //初始化箭头起始位置
        arrowFrom = GetNode<Marker2D>("Cardbase/Marker2D");
        //初始化箭头
        cardBase             = GetNode<Node2D>("Cardbase");
        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
        cardBase.Visible     = false;
        //卡牌地址
        cardRes = ResourceManager.Instance?.GetScene("res://bin/cardbase.tscn") ?? ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        //初始化按钮
        buttonNextTurn           = GetNode<TextureButton>("NextTurnButton");
        // 「下一回合」按下时响一声按键音（战斗界面里只有它用这个音）。
        UiClickSound.Attach(buttonNextTurn);

        // 左上角的设置按钮：与 ESC 同一个入口（见 OpenPauseMenu）
        settingsButton = GetNodeOrNull<Button>("SettingsButton");
        if (settingsButton != null)
        {
            settingsButton.Pressed += OpenPauseMenu;
            UiClickSound.Attach(settingsButton);
        }

        // 初始化卡组：优先复用WorldMap已缓存的卡牌数据，避免重复解析card.ini
        if (BattleStateManager.IsCardDataCached)
        {
            cardMaganer.SetCardDictionary(BattleStateManager.GetAllCachedCards());
            GD.Print("[battlefield] 复用已缓存的卡牌数据，跳过INI加载");
        }
        else
        {
            // 冷启动回退：独立调试battlefield场景时手动加载
            var INIpath = "res://cards/card.ini";
            if (!Godot.FileAccess.FileExists(INIpath))
            {
                GD.PushError($"INI file not found: {INIpath}");
                return;
            }
            var configFile = new IniFile();
            configFile.Load("cards\\card.ini");
            Dictionary<string, CardData> _items = [];
            foreach (var section in configFile)
            {
                var card = new CardData();
                card.Id          = section.Key;
                card.Name        = configFile[section.Key]["name"].ToString().Trim();
                card.Description = configFile[section.Key]["description"].ToString().Trim();
                card.Attack      = configFile[section.Key]["attack"].ToInt();
                card.Defense     = configFile[section.Key]["defense"].ToInt();
                card.Cost        = configFile[section.Key]["price"].ToInt();
                card.IsHq        = (HQ)configFile[section.Key]["isHq"].ToInt();
                card.Effect      = configFile[section.Key]["effect"].ToString().Trim();
                card.PlayEffect  = GetOptionalValue(configFile[section.Key], "playEffect");
                card.AttackEffect = GetOptionalValue(configFile[section.Key], "attackEffect");
                card.CardType    = CardParser.GetTypes(configFile[section.Key]["cardType"].ToString().Trim());
                card.Rarity      = CardParser.GetRarity(configFile[section.Key]["rarity"].ToString().Trim());
                card.IconPath    = configFile[section.Key]["icon"].ToString().Trim();
                card.TargetType  = CardParser.GetTargetType(configFile[section.Key]["targetType"].ToString().Trim());
                card.Traits      = CardParser.GetTraitList(configFile[section.Key]["traits"].ToString().Trim());
                _items[card.Id] = card;
            }
            cardMaganer.SetCardDictionary(_items);
            BattleStateManager.CacheAllCards(_items);
        }

        // 创建电表式指挥点数字显示（替换原有Label）
        SetupMeterLabels();

        player1 = new Player(new Vector2(700,700), this,IsFriend.friend);
        player2 = new Player(new Vector2(700,0), this,IsFriend.enemy);


        //初始化hq
        myHq = cardMaganer.LoadHq(1);
        AddCardToPlace(myHq,supportLine[2]);
        _battleHqDefenceStart = myHq.ReadDefence();

        enemyHq = cardMaganer.LoadHq(2);
        AddCardToPlace(enemyHq,enemySupprotLine[2]);
        
        // 加载敌方行动队列（战役模式从BattleStateManager读取，否则默认berlin）
        string enemyPreset = BattleStateManager.ResolveEnemyPreset();
        LoadEnemyActionQueue(enemyPreset);
        GD.Print($"Loaded enemy preset: {enemyPreset}");
        CreateEnemyIntentPanel();
        RefreshEnemyIntentPanel();

        //初始化敌人
        EnemyInit();
        
        GetNode<End>("end").Visible = false;

        _ = StartBattleAsync();

        // 右上角"查看卡组"按钮
        CreateDeckViewButton();
    }

    /// <summary>
    /// 战斗开场：先结算关卡的开局效果，再走起手抽牌与换牌。
    /// `_Ready` 是同步的，所以这里自行把整条异步链接起来。
    /// 开局效果必须排在最前——它改的是总部防御这类开局状态，
    /// 玩家在换牌界面上就应该已经看到最终数值。
    /// </summary>
    private async Task StartBattleAsync()
    {
        await RunBattleStartEffectAsync();
        await StartOpeningHandAsync();
    }

    /// <summary>
    /// 执行关卡开局效果（`enemyTurn.ini` 该关的 `battleStart=` 键），战斗开始时结算一次。
    ///
    /// 这是「游戏开始时，若 xxx 则 xxx」的通用入口：效果脚本本身就能写条件，
    /// 来源卡固定为敌方总部，所以常用写法是
    ///   `enemyHq|heal(&hp*10)`              —— 每有 1 条命，敌方总部 +10 防御
    ///   `if(&hp&lt;2)skip&amp;|enemyHq|heal(20)`   —— 血量不足 2 时才加
    /// 可用的 `&hp` 是战役血量（`BattleStateManager.Hp`）。
    ///
    /// 没写这个键的关卡就是没有开局效果，行为与从前完全一致。
    /// </summary>
    private async Task RunBattleStartEffectAsync()
    {
        if (string.IsNullOrWhiteSpace(_battleStartEffect))
        {
            return;
        }

        GD.Print($"[BattleStart] 关卡={BattleStateManager.SelectedEnemy} 血量={BattleStateManager.Hp} "
               + $"开局效果={_battleStartEffect}");
        await ParseAndExecuteEffect(_battleStartEffect, enemyHq, null);
    }

    /// <summary>
    /// 开局流程：抽起手牌 → 换牌界面 → 解锁操作。
    /// 全程锁住操作，避免换牌期间点到底下的战场。
    /// </summary>
    private async Task StartOpeningHandAsync()
    {
        ForbidControl();
        try
        {
            await player1.DrawCard(OpeningHandSize);
            await MulliganScreen.ShowAsync(this, player1);
        }
        finally
        {
            // 无论换牌流程是否出问题，都必须解锁，否则玩家永远动不了
            AllowControl();
        }
    }

    /// <summary>
    /// 在屏幕右上角创建"查看卡组"按钮，显示持久化的原始卡组（不含战斗临时卡）
    /// </summary>
    private void CreateDeckViewButton()
    {
        var viewSize = GetViewportRect().Size;
        var viewDeckBtn = new Button();
        viewDeckBtn.Text = "卡组";
        viewDeckBtn.Position = new Vector2(viewSize.X - 110, 10);
        viewDeckBtn.Size = new Vector2(90, 36);
        viewDeckBtn.ZIndex = 1000;
        viewDeckBtn.Pressed += () =>
        {
            var displayDeck = BattleStateManager.BuildDisplayDeck();
            BattleStateManager.ShowDeckViewer(this, displayDeck);
        };
        AddChild(viewDeckBtn);

        // 这个按钮是 `_Ready` 之后才建的，所以 `AttachAll` 那一轮扫不到它，
        // 得在**创建处**单独挂。（战斗里点开卡组之后那个「返回」按钮在 DisplayCard 里，
        // 由它自己的 `_Ready` 覆盖。）
        UiClickSound.Attach(viewDeckBtn);
    }

    /// <summary>
    /// 创建电表式滚动数字组件，替换场景中原有的 point/pointMax Label
    /// </summary>
    private void SetupMeterLabels()
    {
        var font = ResourceManager.Instance?.GetFont("res://bin/FRADMCN.TTF") ?? ResourceLoader.Load<FontFile>("res://bin/FRADMCN.TTF");
        var meterColor = new Color(1, 0.725f, 0, 1);

        // 读取原Label位置作为参考
        var oldPoint = GetNode<Label>("point");
        var oldPointMax = GetNode<Label>("pointMax");
        var oldSlash = GetNode<Label>("charleft");

        // 创建指挥点电表
        var pointMeter = new MeterLabel();
        pointMeter.Name = "pointMeter";
        pointMeter.ZIndex = 100;
        pointMeter.Position = oldPoint.Position;
        pointMeter.Initialize(font, 145, meterColor, digitCount: 1, initialValue: 1);
        AddChild(pointMeter);

        // 创建指挥点上限电表
        var pointMaxMeter = new MeterLabel();
        pointMaxMeter.Name = "pointMaxMeter";
        pointMaxMeter.ZIndex = 100;
        pointMaxMeter.Position = oldPointMax.Position;
        pointMaxMeter.DigitWidthRatio = 0.5f;
        pointMaxMeter.Initialize(font, 130, meterColor, digitCount: 1, initialValue: 1);
        AddChild(pointMaxMeter);

        // 隐藏原有Label
        oldPoint.Visible = false;
        oldPointMax.Visible = false;
        oldSlash.Visible = false;

        // 创建新的 "/" 分隔符Label，放置在两个电表之间
        var slashLabel = new Label();
        slashLabel.Name = "slashMeter";
        slashLabel.Text = "/";
        slashLabel.ZIndex = 100;
        slashLabel.AddThemeFontOverride("font", font);
        slashLabel.AddThemeFontSizeOverride("font_size", 130);
        slashLabel.AddThemeColorOverride("font_color", meterColor);
        slashLabel.Set("theme_override_constants/outline_size", 12);
        slashLabel.Set("theme_override_colors/outline_color", meterColor);
        slashLabel.Position = oldSlash.Position;
        AddChild(slashLabel);
    }

    private void CreateChoiceOverlay()
    {
        choiceLayer = new CanvasLayer();
        AddChild(choiceLayer);

        choiceDim = new ColorRect();
        choiceDim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        choiceDim.Color = new Color(0, 0, 0, 0);
        choiceDim.MouseFilter = Control.MouseFilterEnum.Stop;
        choiceDim.Visible = false;
        choiceLayer.AddChild(choiceDim);

        choiceContainer = new HBoxContainer();
        choiceContainer.Alignment = BoxContainer.AlignmentMode.Center;
        choiceContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
        choiceContainer.SetCustomMinimumSize(new Vector2(900, 420));
        choiceContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        choiceContainer.Visible = false;
        choiceLayer.AddChild(choiceContainer);
    }

    private async Task<cardBase_> ShowCardChoice(List<cardBase_> cards, bool animateExit = true)
    {
        if (cards == null || cards.Count == 0)
            return null;

        choiceCards = new List<cardBase_>(cards);



        Vector2 screenSize = GetViewportRect().Size;
        float cardWidth = 240f; // 卡牌宽度
        float cardSpacing = 10f; // 卡牌间距
        float totalWidth = choiceCards.Count * cardWidth + (choiceCards.Count - 1) * cardSpacing;
        float startX = (screenSize.X - totalWidth) / 2 + cardWidth / 2-40; // 起始x坐标，确保居中

        // 设置所有卡牌在最上层显示，并添加到选择层中
        foreach (var card in choiceCards)
        {
            card.ZIndex = 100;
        }

        for (int i = 0; i < choiceCards.Count; i++)
        {
            var card = choiceCards[i];
            AddToBattleField(card);
            card.Reparent(choiceLayer);
        
            // 计算卡牌目标位置：屏幕中心水平排列，垂直居中
            float xPos = startX + i * (cardWidth + cardSpacing) - 90;
            float yPos = screenSize.Y / 2 - 120;

            // 从屏幕左侧外部开始飞入，并确保卡牌直立
            card.ResetVisualsInstant();
            card.Rotation = 0f;
            card.GlobalPosition = new Vector2(-cardWidth - 100, yPos);
            
            await card.MoveToPosition(new Vector2(xPos, yPos), 0.5f);
            
            // 可以移除或减少延迟时间
            await Task.Delay(20); // 从10000减少到500毫秒
        }

        choiceContainer.Visible = true;
        isShowingChoiceUI = true;

        // 等待玩家选择一张卡牌
        selectedChoiceCard = null;
        while (selectedChoiceCard == null)
        {
            await Task.Delay(50); // 异步等待，避免忙等待阻塞游戏
        }

        choiceContainer.Visible = false;
        isShowingChoiceUI = false;

        if (animateExit)
        {
            // Choose模式：被选择的卡飞出屏幕
            if (selectedChoiceCard != null)
            {
                selectedChoiceCard.ResetVisualsInstant();
                var exitY = selectedChoiceCard.GlobalPosition.Y;
                var exitPos = new Vector2(-cardWidth - 200, exitY);
                await selectedChoiceCard.MoveToPosition(exitPos, 0.25f);
            }

            // 清理用于选择的临时卡牌
            foreach (var card in choiceCards)
            {
                if (card == null)
                    continue;

                if (card.GetParent() != null)
                {
                    RemoveCard(card);
                }
            }
        }
        else
        {
            // Develop模式：被选择的卡回到正常亮度并正常加入手中，多余的卡飞出屏幕并被移除
            if (selectedChoiceCard != null)
            {
                selectedChoiceCard.ResetVisualsInstant();
                selectedChoiceCard.RestoreColor(); // 恢复正常亮度
                selectedChoiceCard.ZIndex = 10; // 恢复正常层级
            }

            // 多余的卡飞出屏幕并被移除
            foreach (var card in choiceCards)
            {
                if (card == null)
                    continue;

                if (card != selectedChoiceCard)
                {
                    var exitY = card.GlobalPosition.Y;
                    var exitPos = new Vector2(-cardWidth - 200, exitY);
                    await card.MoveToPosition(exitPos, 0.25f);

                    // 使用RemoveCard正确清理卡牌
                    RemoveCard(card);
                }
            }
        }

        choiceCards.Clear();

        return selectedChoiceCard;
    }

    private cardBase_ selectedChoiceCard;
    private bool isShowingChoiceUI = false;

    /// <summary>
    /// 处理卡牌选择界面的点击
    /// </summary>
    private void HandleChoiceCardClick(Vector2 mousePosition)
    {
        if (!isShowingChoiceUI || choiceCards == null || choiceCards.Count == 0)
            return;

        foreach (var card in choiceCards)
        {
            if (card == null)
                continue;

            if (card.GetGlobalRect().HasPoint(mousePosition))
            {
                selectedChoiceCard = card;
                return;
            }
        }
    }



    /// <summary>
    /// 播「这张卡被打出」时的效果。**卡上写了 `playEffect` 就一律以卡为准**，
    /// 没写的才按兵种兜底：
    /// - 指令卡 → 一声「咚」（`DefaultCommandPlayEffect`）；
    /// - 步兵 → 按身材选一档进场脚步声（`InfantryDeployEffect`）；
    /// - 其它（坦克 / 飞机 / 火炮 / 总部）→ 什么都不播。
    ///
    /// 这个函数是**所有「卡进场」路径的汇聚点**，所以两件事都挂在这里就够：
    /// - 从手牌拖上场的走 `Move()`（`isDeployedFromHand`，同时触发 `Deployed`）；
    /// - 效果刷进场的走 `AddCardToPlace()`（同时触发 `BeingAddedToField`）。
    /// 在场上挪位置的单位两条都不满足，不会重播。
    /// </summary>
    private void PlayCardEffect(cardBase_ card)
    {
        if (card == null || !IsInstanceValid(card)) return;

        string effect = card.playEffect;
        if (string.IsNullOrWhiteSpace(effect))
        {
            effect = card.cardType == CardTypes.Command ? DefaultCommandPlayEffect : DeployMoveEffect(card);
        }

        StartEffect(effect, new List<Vector2> { GetCardCenter(card) }, null, card);
    }

    /// <summary>
    /// 卡进场（部署 / 加入战场）时的默认音：
    /// - **步兵** → `infantry_{档}`；
    /// - **坦克与火炮** → `tank_{档}`（素材自己叫 Light/Medium/Heavy，槽位统一叫 small/medium/large）；
    /// - **战斗机与轰炸机** → 不分档，但**部署与移动不是同一条**（见 `PlaneFallbackEffect`）；
    /// - 其余（总部、指令卡）→ `null`。
    ///
    /// 三档按 `attack + defence`：≤`DeploySoundSmallMax` 小、到 `DeploySoundMediumMax` 为止是中、
    /// 再往上是大。取的是**进场那一刻的当前值**（= 卡面值）：`PlayCardEffect` 排在
    /// `Deployed` / `BeingAddedToField` 之前，「部署时 +1/+1」那类还没结算。
    ///
    /// 档位边界与槽位前缀只写在这里一处，音频文件本身配在 `configs/music.ini` 的 `[sfx]` 段。
    /// </summary>
    private static string DeployMoveEffect(cardBase_ card)
    {
        // 总部的 cardType 也是 Infantry，不排掉的话开局摆总部也会响一声。
        if (card.isHq == HQ.hq) return null;

        string plane = PlaneFallbackEffect(card, forDeploy: true);
        if (plane != null) return plane;

        string family = card.cardType switch
        {
            CardTypes.Infantry => InfantryVoicePrefix,
            CardTypes.Tank or CardTypes.Artillery => TankVoicePrefix,
            _ => null
        };
        if (family == null) return null;

        return $"sfx({family}_{SizeTier(card)})";
    }

    /// <summary>
    /// 飞机的兜底音——**「哪些兵种算飞机、这一刻该放哪条」只在这一个函数里判**。
    ///
    /// | | 部署（`forDeploy: true`） | 在场上挪位置（`false`） |
    /// |---|---|---|
    /// | 战斗机 `Plane` | `plane_deploy` | `plane_flyby` |
    /// | 轰炸机 `Bomber` | `plane_deploy` | `plane_flyby` |
    /// | 其它兵种 | `null` | `null` |
    ///
    /// 抽出来是因为**两个调用点要用同一张表**：部署走 `DeployMoveEffect`（由 `PlayCardEffect` 兜底），
    /// 移动走 `PlayMoveEffect`。分开各写一遍的话，「飞机这一刻放哪条」就会有两个说法。
    ///
    /// 返回 `null` 表示「这个兵种不归我管」，调用方据此决定要不要继续往下走。
    /// </summary>
    private static string PlaneFallbackEffect(cardBase_ card, bool forDeploy)
    {
        if (card.cardType is not (CardTypes.Plane or CardTypes.Bomber)) return null;
        return forDeploy ? PlaneDeployEffect : PlaneFlybyEffect;
    }

    /// <summary>
    /// `attack + defence` 落在哪一档：≤`DeploySoundSmallMax` 小、到 `DeploySoundMediumMax` 为止是中、
    /// 再往上是大。取的是**当前值**（进场音要的就是进场那一刻的卡面值）。
    ///
    /// **进场音、开火音、命中音共用这一个判据**——档位边界只写这一处（规范 E）。
    /// </summary>
    private static string SizeTier(cardBase_ card)
    {
        int size = card.ReadAttack() + card.ReadDefence();
        return size <= DeploySoundSmallMax ? TierSmall
             : size <= DeploySoundMediumMax ? TierMedium
             : TierLarge;
    }

    /// <summary>
    /// 抽一张牌时的音（`Player` 抽牌时调）。
    ///
    /// `Player` 里有**四条各自独立的抽牌实现**（`DrawCard` / `DrawCardsWithName` /
    /// `DrawCardsWithType` / `DrawUnitCards`），所以这一声要在四处各调一次。
    /// 那四段的下半截看着一样、其实有微妙差异（爆牌那条一个走 `CardDiscardAndRemove`、
    /// 三个走 `DiscardCard + RemoveCard`），所以**没有合并**——合并是行为改动，
    /// 不属于「加一声抽卡音」的范围。
    ///
    /// 以后**新加抽牌方式时要记得也调这里**，否则那一种抽法会静默没声。
    /// </summary>
    public void PlayDrawSound() => StartEffect(DrawSoundEffect, null, null, null);

    /// <summary>
    /// 单位在场上**挪位置**（支援阵线 → 前线）时的音。一次移动只响一声：
    ///
    /// 1. 卡上写了 `playEffect` 就用它——喀秋莎的「进入阵地」
    ///    （`playEffect = sfx(katyusha_into_pos)`）就是这么配的，**进场与移动共用一行**；
    /// 2. 没写的话，走 `PlaneFallbackEffect`：**战斗机放 `fighter_flyby`**（与它部署时的
    ///    `fighter_deploy` 不是同一条），轰炸机放 `plane_flyby`；
    /// 3. 其余不播。
    ///
    /// 与入场那一声互斥：入场走 `Move()` 的部署分支 / `AddCardToPlace()`，移动走这里的 else 分支，
    /// 两条路不会同时走，所以不会连响两声。
    ///
    /// 单位素材（`playEffect`）在这里也生效，是为了让「部署音」与「移动音」能用同一处配置表达；
    /// 指令卡的 `playEffect` 不会走到这里（指令卡不上场）。
    /// </summary>
    private void PlayMoveEffect(cardBase_ card)
    {
        if (card == null || !IsInstanceValid(card)) return;

        string effect = card.playEffect;
        if (string.IsNullOrWhiteSpace(effect))
        {
            // 非飞机返回 null —— 这就是原来那句 `is not (Plane or Bomber) return;`，
            // 只是「谁是飞机、这个兵种归谁管」现在由 PlaneFallbackEffect 一处说了算。
            effect = PlaneFallbackEffect(card, forDeploy: false);
            if (effect == null) return;
        }

        StartEffect(effect, new List<Vector2> { GetCardCenter(card) }, null, card);
    }

    /// <summary>
    /// 这次攻击**实际**要播的特效串：卡上写了就用卡上的，没写就用兵种默认
    /// （**坦克与火炮 = `TankAttack(命中音槽位)`**，打一发炮弹；其余兵种没有默认，返回空）。
    ///
    /// 抽出来是因为**有两处要用**：`PlayAttackEffect` 拿它去播，
    /// `Attack()` 拿它决定放哪种开火声。两处都读 `card.attackEffect` 的话，
    /// 「没写」的那一格里外会不一致。
    /// </summary>
    private static string ResolveAttackEffect(cardBase_ from)
    {
        if (from == null) return null;
        if (!string.IsNullOrWhiteSpace(from.attackEffect)) return from.attackEffect;

        return DefaultAttackEffect(from);
    }

    /// <summary>
    /// 没写 `attackEffect` 时的**兵种默认攻击动画**——「哪个兵种默认打什么」只在这一个函数里判。
    ///
    /// | 兵种 | 默认 | 常量 |
    /// |------|------|------|
    /// | 战斗机 `Plane` | `strafe`（飞掠 + 扫射） | `PlaneStrikeEffect` |
    /// | 轰炸机 `Bomber` | `airstrike`（飞掠 + 投弹） | `BomberStrikeEffect` |
    /// | 坦克 `Tank` / 火炮 `Artillery` | `TankAttack(命中音槽位)` | `TankAttackEffectFor` |
    /// | 步兵 / 指令 / 总部 | 无（返回 `null`，不播特效） | — |
    ///
    /// 抽出来而不是把 `switch` 塞进 `ResolveAttackEffect`：那个函数要说的是
    /// 「卡上写了就用卡上的」，这里要说的是「没写的话按兵种给什么」，两件事。
    /// </summary>
    private static string DefaultAttackEffect(cardBase_ from)
    {
        return from.cardType switch
        {
            CardTypes.Plane => PlaneStrikeEffect,
            CardTypes.Bomber => BomberStrikeEffect,
            CardTypes.Tank or CardTypes.Artillery => TankAttackEffectFor(from),
            _ => null
        };
    }

    /// <summary>
    /// 坦克/火炮那一发炮弹的**完整特效名**，把命中音槽位当参数带上：
    /// `TankAttack(artillery_large_impact)`。不是坦克/火炮返回 `null`。
    ///
    /// 命中音槽位为什么走**参数**而不是场景 Export：**口径取决于攻击者**
    /// （火炮按身材分三档、坦克不分档），而特效自己不知道是谁打的。由调用方算好了传进去，
    /// 场景就不必为每个口径各做一份——见 `BulletEffect.Configure`。
    /// </summary>
    private static string TankAttackEffectFor(cardBase_ from)
    {
        string impact = ImpactSlot(from);
        return impact == null ? null : $"{TankAttackName}({impact})";
    }

    /// <summary>命中音槽位：坦克不分档，火炮按身材三档。都不是则 `null`。</summary>
    private static string ImpactSlot(cardBase_ from)
    {
        return from.cardType switch
        {
            CardTypes.Tank => TankCannonImpactSlot,
            CardTypes.Artillery => $"{ArtillerySlotPrefix}_{SizeTier(from)}_{ImpactSlotSuffix}",
            _ => null
        };
    }

    /// <summary>
    /// 坦克/火炮这一炮的**开火声**（按口径分档，在炮弹出膛那一刻响）。
    /// 返回 `null` 表示「不该放这个」——这次攻击的特效里根本没有 `TankAttack`
    /// （比如喀秋莎打的是自己的 `sfx(katyusha_fire)`），那就退回通用开火声。
    ///
    /// 与命中音的分工：**开火音这时候响，命中音等炮弹飞到了由 `BulletEffect` 响**。
    /// </summary>
    private static string TankCannonSoundEffect(cardBase_ from, string attackEffect)
    {
        // 先判特效名再碰 from.cardType：`from` 为 null 时 `ResolveAttackEffect` 已返回 null，
        // 这里会直接短路掉，不必再补一个 null 检查。
        if (!EffectRegistry.Contains(attackEffect, TankAttackName)) return null;

        string slot = from.cardType switch
        {
            // 坦克只有 medium / heavy 两套开火素材，**小的也归 medium**（需求方指定）。
            CardTypes.Tank => SizeTier(from) == TierLarge ? TankCannonLargeFireSlot : TankCannonMediumFireSlot,
            CardTypes.Artillery => $"{ArtillerySlotPrefix}_{SizeTier(from)}_{FireSlotSuffix}",
            _ => null
        };
        return slot == null ? null : $"sfx({slot})";
    }

    private bool PlayAttackEffect(cardBase_ from, cardBase_ to)
    {
        if (from == null || to == null || !IsInstanceValid(from) || !IsInstanceValid(to)) return false;
        int attack = from.ReadAttack();
        if (attack <= 0) return false;

        // 把攻击者与攻击力一起交给特效：
        // - `source` 给 flying 用（要动的是这张卡本身，光有坐标拿不到节点）；
        // - `count` 给 bombing 用（扔几发航弹 = 攻击力，特效自己不知道攻击力多少）。
        return StartEffect(ResolveAttackEffect(from),
                           new List<Vector2> { GetCardCenter(from), GetCardCenter(to) },
                           null, from, attack);
    }

    /// <summary>
    /// 播放特效。名字里可以写**多个**特效，英文逗号分隔（如 `bullet,smoke`），
    /// 各自独立开跑、互不等待——总时长等于最长的那个，而不是相加。
    ///
    /// 单个特效名还可以**带一个参数**：`名字(参数)`，例如 `playEffect = sfx(严冬)`。
    /// 参数经 `Effect.Configure` 交给特效，不需要参数的特效忽略即可。
    ///
    /// 分隔符沿用卡牌多值字段的约定（`traits` 也是逗号），但拆分交给
    /// `SplitEffectString`——它是**括号与引号感知**的，所以参数里出现逗号也不会被拆坏。
    /// 写错名字时留一行日志：静默忽略会让「attackEffect 拼错了」表现成「打了没特效」，
    /// 很难查。
    /// </summary>
    private bool StartEffect(string effectNames, IReadOnlyList<Vector2> positions,
                             float? time = null, cardBase_ source = null, int count = 0)
    {
        if (string.IsNullOrWhiteSpace(effectNames)) return false;

        bool started = false;
        foreach (string raw in SplitEffectString(effectNames, ','))
        {
            EffectRegistry.ParseName(raw, out string name, out string argument);
            if (name.Length == 0) continue;

            Effect effect = EffectRegistry.Create(name);
            if (effect == null)
            {
                GD.Print($"[Effect] 未知特效名，已跳过: '{name}'（完整字段: {effectNames}）");
                continue;
            }

            // 顺序是刻意的：先进树（`_Ready` 跑完、子节点就绪），再传参数，最后才开演。
            // 反过来的话 `Configure` 里 `GetNode` 会拿到 null。
            AddChild(effect);
            effect.Configure(argument);
            effect.PrepareForUse();
            _ = RunEffect(effect, positions, time, source, count);
            started = true;
        }
        return started;
    }

    private async Task RunEffect(Effect effect, IReadOnlyList<Vector2> positions, float? time,
                                 cardBase_ source = null, int count = 0)
    {
        try
        {
            await effect.Play(positions, time, source, count);
        }
        finally
        {
            if (IsInstanceValid(effect)) EffectRegistry.Release(effect);
        }
    }

    private static Vector2 GetCardCenter(cardBase_ card) => card.GlobalPosition + card.Size / 2f;

    private static string GetOptionalValue(IniSection section, string key)
    {
        return section.TryGetValue(key, out IniValue value) ? value.GetString().Trim() : "";
    }

/// <summary>
/// 播放战斗音效
/// </summary>
/// <param name="id"></param>
    void PlayBattleSound(int id)
    {
        //if (battleSound.Playing != true)
        {
            battleSound.Play();
        }
    }

/// <summary>
/// 播放死亡音效（单位阵亡的爆炸声）。
///
/// 音效不再是场景里写死的那一条：每次播放从 `configs/music.ini` 的 `[sfx] dead`
/// 槽位里随机抽一条爆炸 wav（清单里已排除下划线开头的未采用版本）。
/// 抽不到（槽位没配 / 加载失败）就保持场景里原有的那条，不会变成没声音。
/// </summary>
/// <param name="id"></param>
    void PlayDeadSound(int id)
    {
        var stream = MusicManager.Instance?.PickSfx(DeadSfxSlot);
        if (stream != null)
        {
            deadSound.Stream = stream;
        }

        //if (deadSound.Playing != true)
        {
            deadSound.Play();
        }
    }


/// <summary>
/// 点击后获得的当前理应正在选中的卡
/// </summary>
    cardBase_ cardNowChoose = null;


    /// <summary>
    /// 鼠标点击的卡牌的位置和鼠标点击点之间的位置差异 用来拖动卡牌
    /// </summary>
    Godot.Vector2 offset;

    //箭头
    Node2D cardBase;

    /// <summary>
    /// 箭头显示的起点与卡牌锚点的偏移
    /// </summary>
    /// <returns></returns>
    Vector2 arrowOffset = new Vector2(90, 120);

    // 临时用于显示一次性箭头的Line2D引用（可同时显示多个）


/// <summary>
/// 用于控制input
/// </summary>
    enum InputState
    {
        P_InHandCommand,

        P_InHandUnit,
        P_InPlaceUnit,
        P_InHandCommandNeedChooseTarget,
        P_InHandUnitNeedChooseTarget,

        P_ChoosingCard,
        waitingForChoosingTarget,
        nil
    }

InputState currentInputState = InputState.nil;

    // 控制台面板
    private Panel _consolePanel;
    private LineEdit _consoleInput;
    private RichTextLabel _consoleOutput;
    private bool _consoleVisible;

    // 控制台自动补全
    private int _autoCompleteIndex = -1;
    private string _autoCompletePrefix = "";
    // 控制台指令历史
    private List<string> _cmdHistory = new List<string>();
    private int _cmdHistoryIndex = -1;
    private string _cmdBeforeHistoryScroll = null;
    private static readonly string[] ConsoleCommands = new[]
    {
        "myHq", "enemyHq", "this", "target", "GetCardBeingAddToSupportLine", "GetCardBeingAddToHand",
        "Heal()", "damage()", "GetAttack()", "SetDefence()", "addDefence()",
        "setCost()", "addCost()", "subCost()",
        "setResult()", "setTarget", "drawCard", "DrawUnitCards()",
        "GetEffect()", "AddToHand()", "addToSupportLine()", "addToEnemySupportLine()",
        "addToDeck()", "SetMemory()", "AddPoint()", "AddPointMax()", "losePointAtNextTurnBegin()", "GetHandMax()",
        "ShuffleIntoDeck", "GetCardsShuffledIntoDeck",
        "displayAllCardState", "GetAllFriendUnits", "GetAllEnemyUnits", "GetAllFriendTargets", "GetAllEnemyTargets",
        "GetRandomFriendUnit", "GetRandomEnemyUnit",
        "GetRandomFriendTarget", "GetRandomEnemyTarget", "GetRandomNumber()",
        "KillAllTargets", "HealAllTargets", "Refresh", "Retreat", "Discard", "DiscardWithTarget",
        "foreach", "End&", "Develop", "Choose()", "Play",
        "AddTrait()", "RemoveTrait()", "DrawACard()", "GetCardsBeingTreated",
        "getCount()", "setTargets()", "DiscardRandomly()", "DiscardPlayerRandomly()", "DiscardWithName()",
        "addANewUnitToBattlefieldWithCostAndType()", "GetHighestAttackFriendUnit()", "FightRandomEnemy()", "Fight", "GetLeftTarget", "GetRightTarget",
        "convert()",
    };

    private void ToggleConsole()
    {
        if (_consolePanel == null) CreateConsole();
        _consoleVisible = !_consoleVisible;
        _consolePanel.Visible = _consoleVisible;
        if (_consoleVisible) _consoleInput.GrabFocus();
    }

    private void CreateConsole()
    {
        _consolePanel = new Panel();
        _consolePanel.Visible = false;
        _consolePanel.Position = new Vector2(50, 10);
        _consolePanel.Size = new Vector2(600, 200);
        _consolePanel.ZIndex = 1000;
        AddChild(_consolePanel);

        _consoleInput = new LineEdit();
        _consoleInput.Position = new Vector2(0, 0);
        _consoleInput.Size = new Vector2(600, 30);
        _consoleInput.AddThemeColorOverride("font_color", ConsoleStyle.TextColor);
        _consoleInput.AddThemeFontSizeOverride("font_size", 14);
        _consoleInput.PlaceholderText = "输入效果指令，回车执行...";
        _consoleInput.TextSubmitted += OnConsoleSubmit;
        _consolePanel.AddChild(_consoleInput);

        _consoleOutput = new RichTextLabel();
        _consoleOutput.Position = new Vector2(4, 34);
        _consoleOutput.Size = new Vector2(592, 162);
        _consoleOutput.ScrollFollowing = true;
        _consoleOutput.BbcodeEnabled = true;
        _consoleOutput.AddThemeColorOverride("default_color", ConsoleStyle.TextColor);
        _consoleOutput.AddThemeFontSizeOverride("normal_font_size", 12);
        _consolePanel.AddChild(_consoleOutput);

        ConsoleStyle.Apply(_consolePanel, _consoleInput);
    }

    private void ConsolePrint(string text)
    {
        if (_consoleOutput != null)
            _consoleOutput.AppendText(text + "\n");
    }

    private async void OnConsoleSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string cmd = text.Trim();
        _consoleInput.Text = "";
        _autoCompleteIndex = -1;
        _autoCompletePrefix = "";
        // 记录到历史（去重相邻相同项）
        if (_cmdHistory.Count == 0 || _cmdHistory[_cmdHistory.Count - 1] != cmd)
            _cmdHistory.Add(cmd);
        _cmdHistoryIndex = _cmdHistory.Count;
        _cmdBeforeHistoryScroll = null;
        ConsolePrint($"> {cmd}");
        // 以友方总部为sourceCard和targetCard执行，确保setTarget和drawCard等指令能正常工作
        await ParseAndExecuteEffect(cmd, myHq, null, myHq);
        await CheckIfAnyUnitDiedAsync();
    }

    /// <summary>
    /// 控制台Tab自动补全：根据用户原始输入的前缀，在所有匹配指令之间轮流切换
    /// </summary>
    private void HandleConsoleAutocomplete()
    {
        if (_consoleInput == null) return;

        string currentText = _consoleInput.Text;
        int lastSep = Math.Max(currentText.LastIndexOf('|'), currentText.LastIndexOf(' '));
        string currentPrefix = lastSep >= 0 ? currentText.Substring(lastSep + 1) : currentText;

        // 判断是否仍在同一轮补全循环中：当前单词必须以原始前缀开头
        bool inCycle = _autoCompleteIndex >= 0
            && !string.IsNullOrEmpty(_autoCompletePrefix)
            && currentPrefix.StartsWith(_autoCompletePrefix, StringComparison.OrdinalIgnoreCase);

        // 确定搜索前缀：循环中始终使用用户最初输入的前缀
        string searchPrefix = inCycle ? _autoCompletePrefix : currentPrefix;

        // 查找所有以搜索前缀开头的匹配指令
        var matches = new List<string>();
        foreach (var cmd in ConsoleCommands)
        {
            if (cmd.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase) && !cmd.Equals(searchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(cmd);
            }
        }

        if (matches.Count == 0)
        {
            _autoCompleteIndex = -1;
            _autoCompletePrefix = "";
            return;
        }

        // 首次进入此轮补全，记录原始前缀
        if (!inCycle)
        {
            _autoCompletePrefix = currentPrefix;
            _autoCompleteIndex = 0;
        }
        else
        {
            _autoCompleteIndex = (_autoCompleteIndex + 1) % matches.Count;
        }

        string completion = matches[_autoCompleteIndex];

        // 替换最后一个单词为补全项
        string newText = lastSep >= 0
            ? currentText.Substring(0, lastSep + 1) + completion
            : completion;
        _consoleInput.Text = newText;
        _consoleInput.CaretColumn = newText.Length;
    }

    /// <summary>
    /// 安全取消当前拖拽，把卡放回原处。
    ///
    /// 拖拽是「按下进入、松开退出」的一对事件。截图工具（系统截图、Snipaste、QQ 截图等）
    /// 会把鼠标抢走，让「松开左键」永远送不到游戏里；窗口失焦（Alt-Tab）也一样。
    /// 这时卡会永久停在 caught / inplaceAndCaught，而 `RefreshMyHand()` 对拖拽中的卡
    /// 是 `continue` 跳过的，于是它再也不会归位，显示层级也跟着乱。
    /// 所以收尾不能指望那个可能永远不来的事件——外部一有风吹草动就主动撤。
    ///
    /// 只处理「卡还在手上 / 还在地上等着落位」这两种中间态；若效果已经结算
    /// （卡被消耗、或已经是 placed 以外的状态），这里不插手。
    /// </summary>
    private void CancelCurrentDrag()
    {
        if (cardNowChoose == null)
        {
            return;
        }

        var card = cardNowChoose;
        cardNowChoose = null;
        currentInputState = InputState.nil;

        switch (card.getState())
        {
            case CardState.caught:
            case CardState.commandCardCaught:
                card.setState(CardState.inHand);
                card.ResetVisualsInstant();
                break;
            case CardState.inplaceAndCaught:
                card.setState(CardState.placed);
                card.ResetVisualsInstant();
                break;
        }

        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
        cardBase.Visible = false;
        RestoreAllTargetsColor();          // 取消高亮，恢复所有单位的原始颜色
        player1?.RefreshMyHand();
        _displayOrderDirty = true;

        GD.Print($"[Drag] 拖拽被外部打断（窗口失焦或鼠标被抢占），已安全取消：{card.id}");
    }

    /// <summary>
    /// 窗口失去焦点时兜底取消拖拽：Alt-Tab、被截图工具抢焦点等情况，
    /// 「松开左键」都不会再送到本窗口。
    /// </summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
        {
            CancelCurrentDrag();
        }
    }

    public override void _Input(InputEvent @event)
    {
    // ESC 开关暂停菜单（设置 / 认输 / 保存并退出）。
    // 菜单自己开着的时候由它处理 ESC（`PauseMenu._Input` 先 AcceptEvent），
    // 所以这里只在**没开**的时候响应，不会一按就开了又关。
    if (@event is InputEventKey escEvent && escEvent.Pressed && !escEvent.Echo
        && escEvent.Keycode == Key.Escape && !PauseMenu.IsOpen)
    {
        OpenPauseMenu();
        AcceptEvent();
        return;
    }

    // 按`键开关控制台
    if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Quoteleft)
    {
        ToggleConsole();
        AcceptEvent();
        return;
    }

    // 控制台Tab自动补全
    if (@event is InputEventKey tabEvent && tabEvent.Pressed && tabEvent.Keycode == Key.Tab && _consoleVisible && _consoleInput != null)
    {
        HandleConsoleAutocomplete();
        AcceptEvent();
        return;
    }

    // 控制台↑↓历史回滚
    if (@event is InputEventKey arrowEvent && arrowEvent.Pressed && _consoleVisible && _consoleInput != null)
    {
        if (arrowEvent.Keycode == Key.Up)
        {
            if (_cmdHistory.Count == 0) return;
            if (_cmdHistoryIndex == _cmdHistory.Count)
                _cmdBeforeHistoryScroll = _consoleInput.Text;
            if (_cmdHistoryIndex > 0) _cmdHistoryIndex--;
            _consoleInput.Text = _cmdHistory[_cmdHistoryIndex];
            _consoleInput.CaretColumn = _consoleInput.Text.Length;
            AcceptEvent();
            return;
        }
        if (arrowEvent.Keycode == Key.Down)
        {
            if (_cmdHistoryIndex == _cmdHistory.Count) return;
            _cmdHistoryIndex++;
            if (_cmdHistoryIndex == _cmdHistory.Count)
                _consoleInput.Text = _cmdBeforeHistoryScroll ?? "";
            else
                _consoleInput.Text = _cmdHistory[_cmdHistoryIndex];
            _consoleInput.CaretColumn = _consoleInput.Text.Length;
            AcceptEvent();
            return;
        }
    }

    // 卡牌选择界面（ShowCardChoice）是模态的，必须放在控制锁判定之前处理。
    //
    // 「友方回合开始时」这类时点触发的 Develop 嵌在回合切换的控制锁里面：
    //   OnNextTurnButtonPressed → ForbidControl → RunTurnTransitionAsync → FriendlyTurnBegin
    // 此时 allowControl == 1，但选择界面已经弹出、正等玩家点一张卡。若让下面的
    // ReadControlState() 先把事件吃掉，点击永远到不了 HandleChoiceCardClick，
    // 界面就永久卡死（卡能看见、但一张都点不动）。
    if (isShowingChoiceUI && @event is InputEventMouseButton choiceClick
        && choiceClick.ButtonIndex == MouseButton.Left && choiceClick.Pressed)
    {
        HandleChoiceCardClick(GetGlobalMousePosition());
        return; // 选择界面中不要处理其他输入
    }

    // 兜底：拖拽中途鼠标被抢走（截图工具、输入法弹窗等），「松开左键」就再也送不进来了。
    // 鼠标一动就核对一次物理左键状态，发现其实早就松开，就安全取消这次拖拽。
    // 必须放在下面的控制锁判定之前——收尾不能被「当前不允许操作」挡住。
    if (cardNowChoose != null && @event is InputEventMouseMotion
        && !Input.IsMouseButtonPressed(MouseButton.Left))
    {
        CancelCurrentDrag();
        return;
    }

    //如果当前正处于无法操作状态 取消这一次操作
    if (ReadControlState() == 1) return;

    if (@event is InputEventMouseButton mouseButton)
    {

            var mousePosition = GetGlobalMousePosition();
            // 必须限定左键：拖拽期间右键按下会误入此分支，
            // 把 currentInputState 冲成 nil，导致随后的左键释放无法落位
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            var card = CheckCardClick(mousePosition);
            if(currentInputState != InputState.waitingForChoosingTarget) cardNowChoose = card;
            if (card == null) return; // 没有点击到卡牌，不处理
            // 点击时立即将卡牌提升到最上层
            _displayOrderDirty = true;
            var validTargets = GetAllowedTargets(card);
            if(currentInputState == InputState.waitingForChoosingTarget) ;//如果是等待 那直接跳过
            else if (card.cardType == CardTypes.Command && card.getState() == CardState.inHand && card.targetType != TargetType.NOTarget) {currentInputState = InputState.P_InHandCommandNeedChooseTarget;HighlightValidTargets(card.targetType);}
            else if (card.cardType == CardTypes.Command && card.getState() == CardState.inHand && card.targetType == TargetType.NOTarget) currentInputState = InputState.P_InHandCommand;
            else if (card.cardType != CardTypes.Command && card.getState() == CardState.inHand && card.targetType != TargetType.NOTarget) currentInputState = InputState.P_InHandUnitNeedChooseTarget;
            else if (card.cardType != CardTypes.Command && card.getState() == CardState.inHand && card.targetType == TargetType.NOTarget) currentInputState = InputState.P_InHandUnit;
            else if (card.cardType != CardTypes.Command && card.getState() == CardState.placed ) currentInputState = InputState.P_InPlaceUnit;
            else currentInputState = InputState.nil;

            switch (currentInputState)
            {
                case InputState.P_InHandCommandNeedChooseTarget:
                if(player1 != null && player1.HasPoint(card.ReadCost()))
                    {
                        cardNowChoose = card;
                        cardNowChoose.setState(CardState.commandCardCaught);
                        // 显示箭头起点
                        arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                        cardBase.ProcessMode     = Node.ProcessModeEnum.Inherit;
                        cardBase.Visible         = true;
                        
                    }
                break;

                //这些都是拖拽
                case InputState.P_InHandCommand:
                case InputState.P_InHandUnit:
                case InputState.P_InHandUnitNeedChooseTarget:
                if(player1 != null && player1.HasPoint(card.ReadCost()))
                        {
                    //先拖拽到某个位置,然后再考虑目标的问题,大概吧
                    card.ResetVisualsInstant();

                    //拖拽这张卡!
                    offset        = card.GetGlobalPosition() - mousePosition;
                    cardNowChoose = card;
                    card.setState(CardState.caught);
                        }
                
                if(currentInputState == InputState.waitingForChoosingTarget)
                        {
                            HighlightValidTargets(cardNowChoose.targetType);
                        }
                    break;

                case InputState.P_InPlaceUnit:
                     if (card.GetIsFriend() != IsFriend.friend || card.isHq == HQ.hq)
                    {
                        // 敌方/HQ不可被拖动，重置输入状态防止释放时误操作
                        cardNowChoose = null;
                        currentInputState = InputState.nil;
                        return;
                    }

                    card.ResetVisualsInstant();
                    cardNowChoose = card;
                    card.setState(CardState.inplaceAndCaught);
                    arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                    cardBase.ProcessMode = Node.ProcessModeEnum.Inherit;
                    cardBase.Visible = true;
                    // 高亮合法攻击目标（敌方单位）
                    HighlightValidAttackTargets(card);
                    break;

                case InputState.waitingForChoosingTarget:
                
                    break;

                default:
                    break;
            }
        } 

        // 必须限定左键：右键抬起会误入此分支，走 switch(nil) 跳过全部落位逻辑，
        // 使卡牌永久停留在 caught 状态（RefreshMyHand 会跳过拖拽中的卡，不再归位）
        if (mouseButton.Pressed == false && mouseButton.ButtonIndex == MouseButton.Left)
            {


                if(cardNowChoose== null ) return; // 没有卡牌被拖动，不处理
                var result = GetPlaceWithPosition(mousePosition);
                

                switch (currentInputState)
                {
                case InputState.P_InHandCommandNeedChooseTarget:
                if(result == null || result.GetMyCard() == null ||result.GetMyCard() != null && IsValidTarget(result.GetMyCard(), cardNowChoose.targetType)== false)
                    {
                        GD.Print($"[TargetSelect] 校验失败: targetType={cardNowChoose.targetType}, card={result?.GetMyCard()?.id}, isValid={result?.GetMyCard() != null && IsValidTarget(result.GetMyCard(), cardNowChoose.targetType)}");
                        cardNowChoose.setState(CardState.inHand);
                        cardNowChoose = null;
                        break;
                    }
                    if (player1.UsePoint(cardNowChoose.ReadCost()))
                        {
                            ExecuteCommandAndDiscard(cardNowChoose, new List<cardBase_> { result.GetMyCard() }, needRestoreColor: true);
                        }
                    currentInputState = InputState.nil;
                    cardNowChoose = null;
                    break;

                    case InputState.P_InHandCommand:
                    //不在合法区域释放,归位
                    if (validArea.GetGlobalRect().HasPoint(mousePosition)!=true)
                        {
                            cardNowChoose.setState(CardState.inHand);
                            cardNowChoose = null;
                            break;
                        } 

                    if (player1.UsePoint(cardNowChoose.ReadCost()))
                        {
                            if(cardNowChoose==null) break;
                            ExecuteCommandAndDiscard(cardNowChoose, null);
                        }
                        else
                        {
                            // 点数不足，什么也不执行
                        }

                    currentInputState = InputState.nil;
                    cardNowChoose = null;

                    break;

                    case InputState.P_InHandUnit:
                    if(result == null || result.GetMyCard() != null || supportLine.Contains(result) == false)
                        {
                            
                            cardNowChoose.setState(CardState.inHand);
                            cardNowChoose = null;
                            break;
                        }
                        
                    if (player1.UsePoint(cardNowChoose.ReadCost()))
                        {
                             _ = Move(cardNowChoose,result);
                              player1.RemoveFromHand(cardNowChoose);
                             cardNowChoose.setState(CardState.placed);
                             cardNowChoose = null;
                        }
                    currentInputState = InputState.nil;
                    cardNowChoose = null;
                    
                    break;

                    case InputState.P_InHandUnitNeedChooseTarget:

                    if(result == null || result.GetMyCard() != null || supportLine.Contains(result) == false)
                        {
                            cardNowChoose.setState(CardState.inHand);
                            cardNowChoose = null;
                            break;
                        }
                        
                    if (player1.UsePoint(cardNowChoose.ReadCost()))
                        {
                             _ = Move(cardNowChoose,result);
                              player1.RemoveFromHand(cardNowChoose);
                             cardNowChoose.setState(CardState.placed);
                            if(GetHowManyCardIsValid(cardNowChoose.targetType)>0) currentInputState = InputState.waitingForChoosingTarget;
                            break;
                        }
                    cardNowChoose.setState(CardState.inHand);
                    cardNowChoose = null;
                    break;

                    case InputState.P_InPlaceUnit:
                    if (cardNowChoose.isHq == HQ.hq)
                    {
                        cardNowChoose = null;
                        break;
                    }
                    cardNowChoose.setState(CardState.placed);
                    if(result == null)
                        {
                            cardNowChoose = null;
                            break;
                        }
                    if(result.GetMyCard() == null)
                        {
                            _=Move(cardNowChoose, result);
                            cardNowChoose = null;
                            break;
                        }
                    // 攻击落点**不走 IsValidTarget**（那边是给指令用的目标类型筛选），
                    // 所以这里要自己挡一次：阵亡后暂留在场上的卡不能当攻击目标。
                    // 挡不住就能对着一具还没消失的尸体开火。
                    else if(CanBeSelected(result.GetMyCard())
                            && result.GetMyCard().GetIsFriend() != cardNowChoose.GetIsFriend())
                        {
                            Attack(cardNowChoose, result.GetMyCard());
                            cardNowChoose = null;
                            break;
                        }
                    currentInputState = InputState.nil;
                    cardNowChoose = null;
                    break;

                    case InputState.waitingForChoosingTarget:
                    if(result == null || result.GetMyCard() == null||result.GetMyCard() != null && IsValidTarget(result.GetMyCard(), cardNowChoose.targetType)== false)
                        {
                            ;
                        }
                    else
                        {
                            // 触发被指向时点
                            _ = ResolveTargetedCommandAsync(cardNowChoose, result.GetMyCard());
                            cardNowChoose = null;
                            currentInputState = InputState.nil;
                        }
                    break;

                    default:break;
                }
                if(currentInputState != InputState.waitingForChoosingTarget)
                {
                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                cardBase.Visible = false;
                RestoreAllTargetsColor(); // 取消高亮，恢复所有单位的原始颜色
                }
                else if(currentInputState == InputState.waitingForChoosingTarget)
                {
                    cardBase.ProcessMode = Node.ProcessModeEnum.Inherit;
                    cardBase.Visible = true;
                    HighlightValidTargets(cardNowChoose.targetType); // 根据 Command 卡的目标类型高亮合法目标
                    arrowFrom.GlobalPosition = cardNowChoose.GetGlobalPosition() + arrowOffset;
                }
                player1.RefreshMyHand();
                
            }


        //不管怎么说 先把箭头隐藏了
        
        _displayOrderDirty = true;

        _ = CheckIfAnyUnitDiedAsync();

            
    }
}

    public override void _Process(double delta)
    {
        // 必须先 UpdateHover 再 RefreshAllCardDisplayOrder
        // 否则 SetHover(false) 的 ZIndex=10 不会被纠正为 20，导致1帧闪烁
        if (player1 != null)
        {
            if (cardNowChoose == null || cardNowChoose.getState() != CardState.caught)
            {
                player1.UpdateHover(GetGlobalMousePosition());
            }
            else
            {
                player1.UpdateHover(new Vector2(-9999, -9999));
            }
        }

        if (_displayOrderDirty)
        {
            RefreshAllCardDisplayOrder();
            _displayOrderDirty = false;
        }

        if (Input.IsMouseButtonPressed(MouseButton.Left) && cardNowChoose != null && cardNowChoose.getState() == CardState.caught )
        {
            cardNowChoose.SetGlobalPosition(GetGlobalMousePosition() + offset);
        }
    }

    /// <summary>
    /// 替换所有 & 开头的变量为其实际值
    /// </summary>
    private string ReplaceVariables(string effectString, int result, List<cardBase_> targets = null, cardBase_ sourceCard = null)
    {
        StringBuilder sb = new StringBuilder();
        int i = 0;

        while (i < effectString.Length)
        {
            // 检查是否是变量引用
            if (effectString[i] == '&' && i + 1 < effectString.Length)
            {
                // 获取变量名
                int varStart = i + 1;
                int varEnd = varStart;

                // 变量名可以包含字母、数字和下划线
                while (varEnd < effectString.Length &&
                       (char.IsLetterOrDigit(effectString[varEnd]) || effectString[varEnd] == '_'))
                {
                    varEnd++;
                }

                string varName = effectString.Substring(varStart, varEnd - varStart);

                // 根据变量名替换为实际值
                switch (varName)
                {
                    case "result":
                        sb.Append(result);
                        break;
                    case "targetsCount":
                    case "targets.count":
                        sb.Append(targets?.Count ?? 0);
                        break;
                    case "sourceAttack":
                    case "source.attack":
                        sb.Append(sourceCard?.ReadAttack() ?? 0);
                        break;
                    case "sourceDefence":
                    case "source.defence":
                        sb.Append(sourceCard?.ReadDefence() ?? 0);
                        break;
                    case "sourceCost":
                    case "source.cost":
                        sb.Append(sourceCard?.ReadCost() ?? 0);
                        break;
                    case "attackCountThisTurn":
                        sb.Append(sourceCard?.ReadAttackCountThisTurn() ?? 0);
                        break;
                    case "overflow":
                        sb.Append(lastOverflowDamage);
                        break;
                    case "lastDamage":
                        sb.Append(lastDamage);
                        break;
                    case "LastdeadFriendlyLandUnit":
                        sb.Append(lastDeadFriendlyLandUnitId ?? "");
                        break;
                    case "fieldFriendUnitCount":
                    case "field.friend.unit.count":
                        sb.Append(GetFieldUnitCount(IsFriend.friend));
                        break;
                    case "fieldEnemyUnitCount":
                    case "field.enemy.unit.count":
                        sb.Append(GetFieldUnitCount(IsFriend.enemy));
                        break;
                    case string s when s.StartsWith("fieldFriend") && s.EndsWith("Count"):
                        {
                            string typeName = s.Substring("fieldFriend".Length, s.Length - "fieldFriend".Length - "Count".Length);
                            sb.Append(GetFieldUnitCount(IsFriend.friend, ParseCardTypeFromName(typeName)));
                        }
                        break;
                    case string s when s.StartsWith("fieldEnemy") && s.EndsWith("Count"):
                        {
                            string typeName = s.Substring("fieldEnemy".Length, s.Length - "fieldEnemy".Length - "Count".Length);
                            sb.Append(GetFieldUnitCount(IsFriend.enemy, ParseCardTypeFromName(typeName)));
                        }
                        break;
                    case "friendHqDefence":
                    case "friend.hq.defence":
                        sb.Append(GetHqDefence(IsFriend.friend));
                        break;
                    case "enemyHqDefence":
                    case "enemy.hq.defence":
                        sb.Append(GetHqDefence(IsFriend.enemy));
                        break;
                    case "friendCommandPoint":
                        sb.Append(player1?.ReadPoint() ?? 0);
                        break;
                    case "friendCommandPointMax":
                        sb.Append(player1?.ReadPointMax() ?? 0);
                        break;
                    case "friendHandCount":
                        sb.Append(player1?.GetCardsInHand().Count ?? 0);
                        break;
                    case "friendDeckRemainingCount":
                        sb.Append(player1?.ReadDeckCount() ?? 0);
                        break;
                    case "hp":
                        // 战役血量（「还能失败几次」）。非战役模式（直接跑战场场景调试）
                        // 没有血量概念，BattleStateManager.Hp 会停在初始值，属预期。
                        sb.Append(BattleStateManager.Hp);
                        break;
                    case "lifeTime":
                        if (targets != null && targets.Count > 0)
                            sb.Append(targets[0].ReadLifeTime());
                        else if (sourceCard != null)
                            sb.Append(sourceCard.ReadLifeTime());
                        else
                            sb.Append(0);
                        break;
                    case "targetAttack":
                    case "target.attack":
                        sb.Append((targets != null && targets.Count > 0) ? targets[0].ReadAttack() : 0);
                        break;
                    case "targetDefence":
                    case "target.defence":
                        sb.Append((targets != null && targets.Count > 0) ? targets[0].ReadDefence() : 0);
                        break;
                    case "targetCost":
                    case "target.cost":
                        sb.Append((targets != null && targets.Count > 0) ? targets[0].ReadCost() : 0);
                        break;
                    case "theNumberOfSkirmisher":
                        sb.Append(ReadCardInPlaces().Count(x => x.getState() == CardState.placed && x.id == "轻步兵"));
                        break;
                    default:
                        sb.Append(ReadMemory(varName));
                        break;
                }

                i = varEnd;
            }
            else
            {
                sb.Append(effectString[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private int GetFieldUnitCount(IsFriend side, CardTypes? type = null)
    {
        var units = ReadCardInPlaces()
            .Where(x => x.getState() == CardState.placed && x.GetIsFriend() == side && x.isHq != HQ.hq);

        if (type.HasValue)
            units = units.Where(x => x.cardType == type.Value);

        return units.Count();
    }

    private int GetHqDefence(IsFriend side)
    {
        var hq = side == IsFriend.friend ? myHq : enemyHq;
        return hq != null ? hq.ReadDefence() : 0;
    }

    private int ReadMemory(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 0;

        if (!memoryVariables.ContainsKey(name))
        {
            memoryVariables[name] = 0;
        }

        return memoryVariables[name];
    }

    private void SetMemory(string name, int value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        memoryVariables[name] = value;
    }

    private CardTypes? ParseCardTypeFromName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (Enum.TryParse<CardTypes>(typeName, true, out var parsedType))
            return parsedType;

        return typeName.ToLowerInvariant() switch
        {
            "tank" => CardTypes.Tank,
            "infantry" => CardTypes.Infantry,
            "plane" => CardTypes.Plane,
            "bomber" => CardTypes.Bomber,
            "artillery" => CardTypes.Artillery,
            "command" => CardTypes.Command,
            _ => null,
        };
    }

    /// <summary>
    /// 移除字符串中所有引号外的[...]（attribute元数据）
    /// </summary>
    private static string StripBracketsOutsideQuotes(string s)
    {
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        char quoteChar = '\0';
        int depth = 0;
        foreach (char c in s)
        {
            if ((c == '"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
                sb.Append(c);
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
                quoteChar = '\0';
                sb.Append(c);
            }
            else if (inQuotes)
            {
                sb.Append(c);
            }
            else
            {
                if (c == '[') depth++;
                else if (c == ']') { depth--; continue; }
                if (depth == 0) sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 分割效果字符串，忽略括号内的逗号
    /// </summary>
    /// <summary>
    /// 分割效果字符串，考虑引号和括号内的内容
    /// </summary>
    /// <param name="effectString">要分割的字符串</param>
    /// <param name="delimiter">分隔符，默认为逗号</param>
    /// <returns>分割后的字符串数组</returns>
    private string[] SplitEffectString(string effectString, char delimiter = ',')
    {
        List<string> segments = new List<string>();
        StringBuilder currentSegment = new StringBuilder();
        int parenCount = 0;
        int bracketCount = 0;
        bool inQuotes = false;
        char quoteChar = '\0';

        foreach (char c in effectString)
        {
            // 处理引号
            if ((c == '\"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
                currentSegment.Append(c); // 将引号添加到当前段
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
                quoteChar = '\0';
                currentSegment.Append(c); // 将引号添加到当前段
            }
            // 在引号内，不进行任何符号处理，直接添加到当前段
            else if (inQuotes)
            {
                currentSegment.Append(c);
            }
            // 处理括号（不在引号内）
            else if (c == '(' && !inQuotes)
            {
                parenCount++;
                currentSegment.Append(c);
            }
            else if (c == ')' && !inQuotes)
            {
                parenCount--;
                currentSegment.Append(c);
            }
            // 处理方括号（不在引号内）
            else if (c == '[' && !inQuotes)
            {
                bracketCount++;
                currentSegment.Append(c);
            }
            else if (c == ']' && !inQuotes)
            {
                bracketCount--;
                currentSegment.Append(c);
            }
            // 处理分隔符（不在引号内且不在括号内）
            else if (c == delimiter && parenCount == 0 && bracketCount == 0 && !inQuotes)
            {
                segments.Add(currentSegment.ToString().Trim());
                currentSegment.Clear();
            }
            else
            {
                currentSegment.Append(c);
            }
        }

        if (currentSegment.Length > 0)
        {
            segments.Add(currentSegment.ToString().Trim());
        }

        return segments.ToArray();
    }

    /// <summary>
    /// 计算表达式（支持 + - * / 和括号），并向下取整
    /// </summary>
    private int EvaluateExpression(string expr, int result, List<cardBase_> targets = null, cardBase_ sourceCard = null)
    {
        if (string.IsNullOrWhiteSpace(expr))
            return 0;

        expr = expr.Trim();
        expr = ReplaceVariables(expr, result, targets, sourceCard);

        // 仅允许数字、+-*/(). 以及空白
        if (!System.Text.RegularExpressions.Regex.IsMatch(expr, "^[0-9\\+\\-\\*\\/\\(\\)\\.\\s]+$"))
            return 0;

        try
        {
            var dt = new DataTable();
            var valueObj = dt.Compute(expr, "");
            if (valueObj == null)
                return 0;

            double value = Convert.ToDouble(valueObj);
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            return (int)Math.Floor(value);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 在特定时点触发场上单位的效果
    /// </summary>
    /// <param name="triggerPoint">触发时点 如 "Attacking", "BeingAttacked", "Moving" 等</param>
    /// <param name="sourceCard">触发效果的单位（self指向此单位）</param>
    /// <param name="targetCards">效果的目标单位列表（target指向这些单位）</param>
    /// <param name="checkOnlySourceCard">是否只检查sourceCard的效果，true=只检查源卡，false=检查所有单位</param>
    public async Task TriggerUnitEffects(string triggerPoint, cardBase_ sourceCard, List<cardBase_> targetCards = null, bool checkOnlySourceCard = false)
    {
        if (targetCards == null) targetCards = new List<cardBase_>();

        // 确定要检查的单位列表
        List<cardBase_> unitsToCheck = new List<cardBase_>();
        if (checkOnlySourceCard && sourceCard != null)
        {
            unitsToCheck = new List<cardBase_> { sourceCard };
        }
        else
        {
            unitsToCheck = ReadCardInPlaces().Where(x => x.getState() == CardState.placed).ToList();
        }

        foreach (var unit in unitsToCheck)
        {
            if (unit == null || string.IsNullOrEmpty(unit.effect)) continue;

            // 检查效果是否有指定的时点前缀
            var effectString = unit.effect;
            if (!effectString.Contains(":")) continue;

            // 分割多个效果（用逗号分隔，但要考虑括号内的逗号）
            var effectSegments = SplitEffectString(effectString);
            
            foreach (var segment in effectSegments)
            {
                if (!segment.Contains(":")) continue;
                
                var prefix = segment.Split(":")[0].Trim();
                if (prefix != triggerPoint) continue;

                // 移除时间前缀，获取实际效果
                var actualEffect = segment.Substring(segment.IndexOf(":") + 1);

                // 触发此单位的效果（unit 作为执行效果的单位，targetCards 作为目标列表）
                // 如果是FriendlyTurnBegin事件，将unit作为targetCard传递，以便KillAllTargets等指令可以正确执行
                if (triggerPoint == "FriendlyTurnBegin")
                {
                    await ParseAndExecuteEffect(actualEffect, unit, targetCards, unit);
                }
                else
                {
                    await ParseAndExecuteEffect(actualEffect, unit, targetCards);
                }
            }
        }

    }

    /// <summary>
    /// 「友方抽到卡」时点。
    ///
    /// 这里必须 **await 并且补一次死亡检查**，理由和「打出指令」那条一模一样：
    /// 时点里的效果一样能打死人（`damage()` 是缓存型变更，由 `ParseAndExecuteEffect`
    /// 末尾的 `ExecuteChangeLists()` 落地），而这条路径原先写成 `_ = TriggerUnitEffects(...)`
    /// ——发后不理，之后没有任何人查死亡。当前挂在 `FriendlyCardDrawn:` 上的效果只有减费，
    /// 所以还没暴露（见 BUGS.md 第 49 条）；一旦有卡写成「抽牌时造成伤害」，
    /// 就会重演「0 血不死」。
    /// </summary>
    public async Task TriggerFriendlyCardDrawn(cardBase_ card)
    {
        await TriggerUnitEffects("FriendlyCardDrawn", card, new List<cardBase_> { card });
        await CheckIfAnyUnitDiedAsync();
    }

    /// <summary>
    /// 两军交战 注意攻击本身不会导致单位死亡 检查函数才会导致单位死亡
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param> 
    public async Task Attack(cardBase_ from,cardBase_ to)
    {
        lastOverflowDamage = 0;
        lastDamage = 0;
        ForbidControl();
        PauseDeathCheck(); // 暂停死亡检查
        if (!from.CheckIfCanAttack())
        {
            AllowControl();
            ResumeDeathCheck(); // 恢复死亡检查
            return;
        }

        // 检查步兵和坦克是否只能攻击相邻阵线的单位
        // 前线的单位可以攻击敌方支援阵线，支援阵线的单位可以攻击敌方前线
        if ((from.cardType == CardTypes.Infantry || from.cardType == CardTypes.Tank) && 
            from.GetMyPlace() != null && to.GetMyPlace() != null)
        {
            place_ fromPlace = from.GetMyPlace();
            place_ toPlace = to.GetMyPlace();
            
            // 获取攻击者和目标的阵线位置
            bool fromInFrontLine = frontLine.Contains(fromPlace);
            bool toInFrontLine = frontLine.Contains(toPlace);
            bool fromInFriendSupportLine = supportLine.Contains(fromPlace);
            bool fromInEnemySupportLine = enemySupprotLine.Contains(fromPlace);
            bool toInFriendSupportLine = supportLine.Contains(toPlace);
            bool toInEnemySupportLine = enemySupprotLine.Contains(toPlace);
            
            // 获取攻击者和目标的阵营
            bool fromIsFriend = from.GetIsFriend() == IsFriend.friend;
            bool toIsEnemy = to.GetIsFriend() == IsFriend.enemy;
            bool fromIsEnemy = from.GetIsFriend() == IsFriend.enemy;
            bool toIsFriend = to.GetIsFriend() == IsFriend.friend;
            
            // 检查是否可以攻击，满足以下四个条件之一即可：
            // 1. 友方单位在前线且敌方单位在敌方支援阵线
            // 2. 友方单位在支援阵线且敌方单位在前线
            // 3. 敌方单位在前线且友方单位在支援阵线
            // 4. 敌方单位在敌方支援阵线且友方单位在前线
            bool canAttack = 
                (fromIsFriend && fromInFrontLine && toIsEnemy && toInEnemySupportLine) ||
                (fromIsFriend && fromInFriendSupportLine && toIsEnemy && toInFrontLine) ||
                (fromIsEnemy && fromInFrontLine && toIsFriend && toInFriendSupportLine) ||
                (fromIsEnemy && fromInEnemySupportLine && toIsFriend && toInFrontLine);
            
            // 如果不能攻击，则不允许
            if (!canAttack)
            {
                GD.Print($"Attack failed: {from} (Infantry/Tank) can only attack adjacent lines");
                AllowControl();
                ResumeDeathCheck(); // 恢复死亡检查
                return;
            }
        }

        // 检查目标是否具有烟幕特性
        if (to.HasSmokeScreenActive())
        {
            GD.Print($"Attack failed: Target {to} has smoke screen active");
            AllowControl();
            ResumeDeathCheck(); // 恢复死亡检查
            return;
        }

        // 检查目标是否被守护
        if (IsTargetProtectedByGuardian(to, from))
        {
            AllowControl();
            ResumeDeathCheck(); // 恢复死亡检查
            return;
        }

        // 触发被指向时点（目标被选为攻击对象）
        await TriggerUnitEffects("BePicked", to, new List<cardBase_> { from }, checkOnlySourceCard: true);

        // 同仇特性：被指向时友方同仇单位+1+1
        await TriggerSharedHatred(to);

        // 提前计算完整伤害和溢出，供 Attacking 效果中的 &overflow 使用
        int attackDamage = from.ReadAttack();
        if (to.HasTrait(UnitTraits.HeavyArmor)) attackDamage = Math.Max(0, attackDamage - 1);
        if (to.HasTrait(UnitTraits.Immunity)) attackDamage = 0;
        lastDamage = attackDamage;
        lastOverflowDamage = Math.Max(0, attackDamage - to.ReadDefence());

        // 触发攻击者的 Attacking 效果
        await TriggerUnitEffects("Attacking", from, new List<cardBase_> { to }, checkOnlySourceCard: true);

        // 触发被攻击者的 BeingAttacked 效果
        await TriggerUnitEffects("BeingAttacked", to, new List<cardBase_> { to, from }, checkOnlySourceCard: true);

        // 触发"成为敌方攻击的目标时"效果
        if (to.GetIsFriend() == IsFriend.friend && from.GetIsFriend() == IsFriend.enemy)
        {
            await TriggerUnitEffects("BecomingAttackTarget", to, new List<cardBase_> { from });
        }

        // 触发"对战步兵时"效果
        if (to.cardType == CardTypes.Infantry)
        {
            await TriggerUnitEffects("FightingInfantry", from, new List<cardBase_> { to });
        }

        // 触发"攻击总部时"效果
        if (to.isHq == HQ.hq)
        {
            await TriggerUnitEffects("AttackingHq", from, new List<cardBase_> { to });
        }

        //友方单位开火
        if(from.GetIsFriend() == IsFriend.friend)
        {
            await TriggerUnitEffects("FriendlyUnitAttacking", from, new List<cardBase_> { to, from });
        }
        //敌方单位开火
        else if(from.GetIsFriend() == IsFriend.enemy)
        {
            await TriggerUnitEffects("EnemyUnitAttacking", from, new List<cardBase_> { to, from });
        }

          // 触发友方单位的 FriendlyUnitBeingAttacked 效果（如果目标是友方）
        if (to.GetIsFriend() == IsFriend.friend)
        {
            await TriggerUnitEffects("FriendlyUnitBeingAttacked", from, new List<cardBase_> { to, from });
        }

        // 触发敌方单位的 EnemyUnitBeingAttacked 效果（如果目标是敌方）
        if (to.GetIsFriend() == IsFriend.enemy)
        {
            await TriggerUnitEffects("EnemyUnitBeingAttacked", from, new List<cardBase_> { to, from });
        }

    bool attackerHasShock = from.HasShockActive();
        if (attackerHasShock) from.RemoveShock();

        int counterDamage = to.ReadAttack();
        if (from.HasTrait(UnitTraits.HeavyArmor)) counterDamage = Math.Max(0, counterDamage - 1);
        if (from.HasTrait(UnitTraits.Immunity)) counterDamage = 0;

        // 远程单位（火炮、轰炸机）攻击时不受反击，也不会触发防守方的伏击
        bool receivesCounterAttack = from.cardType is CardTypes.Plane or CardTypes.Infantry or CardTypes.Tank;
        bool ambushTriggered = !attackerHasShock && receivesCounterAttack && to.HasAmbushActive();
        bool attackerKilledByAmbush = false;
        SceneTreeTimer attackPresentationTimer = null;

        if (ambushTriggered)
        {
            to.FlashTraitIcon("Ambush");
            to.UseAmbush();
            int defBefore = from.ReadDefence();
            await from.LoseDefence(counterDamage);
            lastOverflowDamage += Math.Max(0, counterDamage - defBefore);
            attackerKilledByAmbush = from.ReadDefence() <= 0;
            if (PlayAttackEffect(to, from))
                attackPresentationTimer ??= GetTree().CreateTimer(0.5);
        }

        if (!attackerKilledByAmbush)
        {
            await to.LoseDefence(attackDamage);
            if (PlayAttackEffect(from, to))
                attackPresentationTimer ??= GetTree().CreateTimer(0.5);
            if (attackDamage > 0)
            {
                await TriggerUnitEffects("TakingDamage", to, new List<cardBase_> { to }, checkOnlySourceCard: true);
                if (to.HasMobilizeActive()) to.RemoveMobilize();
            }
        }

        if (from.HasSmokeScreenActive()) from.RemoveSmokeScreen();

        bool canCounterAttack = to.cardType != CardTypes.Bomber;
        if (!attackerHasShock && !ambushTriggered && canCounterAttack && receivesCounterAttack)
        {
            int defBefore = from.ReadDefence();
            await from.LoseDefence(counterDamage);
            lastOverflowDamage += Math.Max(0, counterDamage - defBefore);
            if (PlayAttackEffect(to, from))
                attackPresentationTimer ??= GetTree().CreateTimer(0.5);
        }

        // 攻击声一共三种可能，优先级从上到下：
        // 1. 攻击特效里带了自定义音效（flying / airstrike / sfx(...)）——它自己会响，不放别的；
        // 2. 坦克与火炮打的是 TankAttack —— 换成对应口径的炮声（小的也用 medium，坦克没有小档素材）；
        // 3. 其余照旧放通用开火声（机枪「哒哒」）。
        // 注意这里只是**出膛**那一声；命中音等炮弹飞到目标由 BulletEffect 自己响。
        string resolvedAttackEffect = ResolveAttackEffect(from);
        if (!EffectRegistry.ReplacesFiringSound(resolvedAttackEffect))
        {
            string cannon = TankCannonSoundEffect(from, resolvedAttackEffect);
            if (cannon != null)
                StartEffect(cannon, new List<Vector2> { GetCardCenter(from) }, null, from);
            else
                PlayBattleSound(1);
        }

        // 标记单位已经攻击，减少可攻击次数
        from.HaveAttacked();
        from.IncrementAttackCountThisTurn();

        // trait触发闪烁
        if (from.HasTrait(UnitTraits.Determination))
            from.FlashTraitIcon("Determination");
        if (attackerHasShock)
            from.FlashTraitIcon("Shock");

        // 步兵、火炮、战斗机和轰炸机攻击后不能移动
        // 但奋战单位若还有剩余攻击次数则保留攻击能力
        bool stillHasDetermination = from.HasTrait(UnitTraits.Determination) && from.ReadAttackable() > 0;
        if (!stillHasDetermination && (from.cardType == CardTypes.Infantry || from.cardType == CardTypes.Artillery ||
            from.cardType == CardTypes.Plane || from.cardType == CardTypes.Bomber))
        {
            from.HaveMoved();
        }

        if (attackPresentationTimer != null)
            await ToSignal(attackPresentationTimer, SceneTreeTimer.SignalName.Timeout);
        ResumeDeathCheck(); // 恢复死亡检查
        await CheckIfAnyUnitDiedAsync(); // 统一检查死亡
        AllowControl();
    }

    /// <summary>
    /// 将一张卡牌从一个位置移动到另一个位置 注意这个函数检查合法性 必须是手牌到支援阵线 支援阵线到前线
    /// </summary>
    /// <param name="card"></param>
    /// <param name="position"></param>
    async Task Move(cardBase_ card, place_ position)
    {
        if (card == null || position == null)
        {
            return;
        }
        PauseDeathCheck(); // 暂停死亡检查

        if(position.GetMyCard()!= null)
        {
            ResumeDeathCheck();
            return;
        }
        
        // 记录是否从手上部署（用于判断是否触发deployed）
        bool isDeployedFromHand = card.GetMyPlace() == null;
        
        if(card.GetIsFriend() == IsFriend.friend)
        {
            if (supportLine.Contains(card.GetMyPlace()))
            {
                if(!frontLine.Contains(position) || CheckIfFrontLineIsFriend()== IsFriend.enemy||card.CheckIfCanMove() == false)
                {
                    ResumeDeathCheck();
                    return;
                }
            }
            else if(frontLine.Contains(card.GetMyPlace()))
            {
                ResumeDeathCheck();
                return;
            }
            else if( card.GetMyPlace()==null)
            {
                if(!supportLine.Contains(position))
                {
                    return;
                }
                else
                {
                    player1.RemoveFromHand(cardNowChoose);
                }
            }
        }
        if(card.GetIsFriend() == IsFriend.enemy)
        {
            if (enemySupprotLine.Contains(card.GetMyPlace()))
            {
                if(!frontLine.Contains(position))
                {
                    ResumeDeathCheck();
                    return;
                }
            }
            else if(frontLine.Contains(card.GetMyPlace()))
            {
                ResumeDeathCheck();
                return;
            }
        }
        
        card.SetMyPlace(position);
        await card.MoveToPosition(position.GetGlobalPosition());
        card.setState(CardState.placed);
        
        // 烟幕特性：单位第一次移动时失去烟幕
        if (!isDeployedFromHand && card.HasSmokeScreenActive())
        {
            card.RemoveSmokeScreen();
        }
        
        // 烟幕特性：单位进入前线时失去烟幕
        if (frontLine.Contains(position) && card.HasSmokeScreenActive())
        {
            card.RemoveSmokeScreen();
        }

        // 如果是从手上部署，触发 deployed 效果
        if (isDeployedFromHand)
        {
            PlayCardEffect(card);
            if(card.HasTrait(UnitTraits.Blitz)) card.RefreshUnit();
            await TriggerUnitEffects("Deployed", card, new List<cardBase_>(), checkOnlySourceCard: true);

            // 触发特定单位类型的部署效果
            if (card.GetIsFriend() == IsFriend.friend)
            {
                if (card.cardType == CardTypes.Tank)
                {
                    await TriggerUnitEffects("FriendlyTankDeployed", card, new List<cardBase_> { card });
                }
                else if (card.cardType == CardTypes.Infantry)
                {
                    await TriggerUnitEffects("FriendlyInfantryDeployed", card, new List<cardBase_> { card });
                }
            }

            // 触发友方单位入场效果
            if (card.GetIsFriend() == IsFriend.friend && card.isHq != HQ.hq)
            {
                await TriggerUnitEffects("FriendlyUnitEnteringField", card, new List<cardBase_> { card }, checkOnlySourceCard: true);
            }
        }
        else
        {
            // 如果不是从手上部署，则调用HaveMoved()方法
            if (!isDeployedFromHand)
            {
                card.HaveMoved();
            }
            // 在场上挪位置也响一声（卡上有 playEffect 就用它，飞机放「飞过」；
            // 入场那一声走 PlayCardEffect，两条互斥）
            PlayMoveEffect(card);
            // 否则触发移动单位的 Moving 效果
            await TriggerUnitEffects("Moving", card, new List<cardBase_> { card }, checkOnlySourceCard: true);
        }
        ResumeDeathCheck(); // 恢复死亡检查
        await CheckIfAnyUnitDiedAsync(); // 统一检查死亡
        RefreshAllBeGuardianedStatus(); // 移动后刷新被守护状态
    }

    IsFriend CheckIfFrontLineIsFriend()
    {
        foreach(var place in frontLine)
        {
            if(place.GetMyCard()!= null)
            {
                return place.GetMyCard().GetIsFriend();
            }
        }
        return IsFriend.neutral;

    }

    /// <summary>
    /// 检查卡牌从当前位置是否实际可移动（前线不可移，支援线需前线非敌方控制）
    /// </summary>
    public bool CanCardMoveFromPlace(cardBase_ card)
    {
        if (card == null) return false;
        var place = card.GetMyPlace();
        if (place == null) return false;
        if (frontLine.Contains(place)) return false;
        if (card.GetIsFriend() == IsFriend.friend && supportLine.Contains(place))
            return CheckIfFrontLineIsFriend() != IsFriend.enemy;
        if (card.GetIsFriend() == IsFriend.enemy && enemySupprotLine.Contains(place))
            return CheckIfFrontLineIsFriend() != IsFriend.friend;
        return true;
    }

    List<cardBase_> enemyDeck;

    /// <summary>
    /// 创建敌人池子 目前只是随机生成30张卡 之后会在这里写敌人卡组构建
    /// </summary>
    void EnemyInit()
    {
        enemyDeck = new List<cardBase_>();
        var cardRes = ResourceManager.Instance?.GetScene("res://bin/cardbase.tscn") ?? ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        for(int i = 0; i < 30; i++)
        {
            var card = cardRes.Instantiate() as cardBase_;
            card.SetCardInformation(GetCardMaganer().GetRandomCard());
            card.SetIsFriend(IsFriend.enemy);
            enemyDeck.Add(card);
        }
    }


    int turn = 0;
    private cardBase_ enemyHq;
    private PackedScene cardRes;



    /// <summary>
    /// 敌方回合 目前只是一个空函数 之后会在这里写敌方ai
    /// </summary>
    async Task EnemyTurnAsync()
    {
        turn++;
        ForbidControl();
        RefreshCardsInField(IsFriend.enemy);

        // 敌方回合开始时，对敌方单位应用动员等trait
        await ApplyEnemyTurnStartTraits();

        // 执行敌方行动队列
        await ExecuteEnemyActionQueue();

        // 敌人行动 AI：移动并攻击
        await EnemyPerformActionsAsync();

        // 敌方回合结束时，移除敌方单位的压制
        foreach (var unit in cardInPlaces.Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy))
        {
            if (unit.HasTrait(UnitTraits.Suppressed))
                unit.RemoveTrait(UnitTraits.Suppressed);
        }

        AllowControl();
    }

    /// <summary>
    /// 敌方执行效果
    /// </summary>
    /// <param name="effectString">效果字符串，语法同指令的效果</param>
    /// <param name="sourceCard">效果发起者（可选）</param>
    /// <param name="targetCards">效果目标列表（可选）</param>
    public async Task EnemyExecuteEffect(string effectString, cardBase_ sourceCard = null, List<cardBase_> targetCards = null)
    {
        if (string.IsNullOrEmpty(effectString))
        {
            return;
        }
        
        // 如果没有指定sourceCard，使用敌方HQ作为默认发起者
        if (sourceCard == null)
        {
            sourceCard = enemyHq;
        }
        
        await ParseAndExecuteEffect(effectString, sourceCard, targetCards);
        await CheckIfAnyUnitDiedAsync(); // 检查死亡
    }

    /// <summary>
    /// 从enemyTurn.ini文件加载敌方行动队列
    /// </summary>
    /// <param name="enemyHqName">敌方总部名称，用于确定读取哪个节</param>
    void LoadEnemyActionQueue(string enemyHqName)
    {
        GD.Print($"LoadEnemyActionQueue called with enemyHqName: {enemyHqName}");
        enemyActionQueue.Clear(); // 清空现有队列
        _battleStartEffect = "";  // 开局效果同理：换关卡不能沿用上一关的
        
        var iniPath = "res://cards/enemyTurn.ini";
        
        if (!Godot.FileAccess.FileExists(iniPath))
        {
            GD.Print($"Enemy turn config file not found: {iniPath}");
            return;
        }
        
        var configFile = new IniFile();
        
        // 使用Godot.FileAccess读取文件内容
        using (var file = Godot.FileAccess.Open(iniPath, Godot.FileAccess.ModeFlags.Read))
        {
            if (file == null)
            {
                GD.Print($"Failed to open file: {iniPath}");
                return;
            }
            
            string content = file.GetAsText();
            GD.Print($"File content:\n{content}");
            using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
            {
                configFile.Load(stream);
            }
        }
        
        // 检查是否存在指定节
        GD.Print($"Checking for section: {enemyHqName}");
        GD.Print($"HasSection result: {configFile.HasSection(enemyHqName)}");
        
        // 打印所有节
        GD.Print("All sections in config file:");
        foreach (var section in configFile)
        {
            GD.Print($"  - {section.Key}");
        }
        
        if (!configFile.HasSection(enemyHqName))
        {
            GD.Print($"Enemy HQ section not found in config: {enemyHqName}");
            return;
        }
        
        // 读取该节下的所有键值对
        var keys = configFile.GetSectionKeys(enemyHqName);
        GD.Print($"GetSectionKeys returned {keys.Count} keys");
        foreach (var key in keys)
        {
            GD.Print($"  Key: {key}");
        }
        
        foreach (var key in keys)
        {
            if (key == "name") continue;
            var value = configFile[enemyHqName][key].ToString().Trim();
            GD.Print($"Loading action: key={key}, value={value}");

            // 开局效果走独立字段，不进行动队列：队列里的每一条都会作为「敌方意图」
            // 显示在左侧面板上，而开局效果只结算一次、不是意图。
            if (key.Equals(BattleStartKey, StringComparison.OrdinalIgnoreCase))
            {
                _battleStartEffect = value;
                continue;
            }

            // 确定实际要添加到的队列
            string actualQueueKey = key;
            string actualValue = value;


            // 添加到对应的队列
            if (!enemyActionQueue.ContainsKey(actualQueueKey))
            {
                enemyActionQueue[actualQueueKey] = new List<string>();
            }
            GD.Print($"Adding to queue: {actualQueueKey} = {actualValue}");
            enemyActionQueue[actualQueueKey].Add(actualValue);
        }
        
        GD.Print($"Loaded enemy action queue for {enemyHqName}: {enemyActionQueue.Count} entries");
    }

    void CreateEnemyIntentPanel()
    {
        var panel = new Control();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        panel.Position = new Vector2(10, 200);
        panel.Size = new Vector2(300, 400);
        panel.ZIndex = 50;

        var bg = new ColorRect();
        bg.Color = new Color(0, 0, 0, 0.55f);
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(bg);

        // 面板高度固定，但 ADD: 队列只增不减（马马耶夫岗峰值 13 行，柏林 11 行），
        // 直接挂 VBoxContainer 时超出的行会画到面板底色之外、叠在战场上。
        // 套一层 ScrollContainer 把它们收进面板内滚动。
        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.OffsetLeft = 6;
        scroll.OffsetTop = 6;
        scroll.OffsetRight = -6;
        scroll.OffsetBottom = -6;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        // 纵向用 ShowNever 而非 Disabled：滚轮滚动必须保留（高行数关卡靠它收纳
        // 溢出行动行），只是不绘制滚动条——滚动条既在战场上显得杂乱，又占去约 12px 宽度
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        panel.AddChild(scroll);

        _enemyIntentContainer = new VBoxContainer();
        // 子节点撑满滚动区宽度，否则行宽会塌成内容宽度
        _enemyIntentContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _enemyIntentContainer.AddThemeConstantOverride("separation", 6);
        // 必须是 Ignore：行若拦截鼠标事件，滚轮就传不到 ScrollContainer
        _enemyIntentContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        scroll.AddChild(_enemyIntentContainer);

        AddChild(panel);
    }

    void RefreshEnemyIntentPanel()
    {
        if (_enemyIntentContainer == null) return;

        foreach (var child in _enemyIntentContainer.GetChildren())
            child.QueueFree();

        int nextTurn = turn + 1;
        var actions = GetNextTurnActions(nextTurn);

        foreach (var action in actions)
        {
            // 一行可由顶层逗号分隔的多个行动组成，每个自带元数据的行动各占一行。
            // 若整行只取末尾一个元数据块（旧写法用 LastIndexOf('[')），
            // “部署第1步兵团[icon=...],敌方总部获得5点防御力[icon=...]” 这类写法
            // 会把前一个行动的描述整个吞掉，界面上看不出敌人还部署了单位。
            foreach (string segment in SplitEffectString(action, ','))
            {
                var metadata = ParseActionMetadata(segment);
                if (string.IsNullOrEmpty(metadata.description)) continue;
                AddEnemyIntentRow(metadata.icon, metadata.description);
            }
        }
    }

    /// <summary>往意图面板追加一行：左侧图标，右侧描述。</summary>
    void AddEnemyIntentRow(string iconName, string description)
    {
        var row = new HBoxContainer();
        row.MouseFilter = Control.MouseFilterEnum.Ignore;

        var icon = new TextureRect();
        icon.Texture = IconCache.GetIcon(iconName) ?? IconCache.GetIcon("boss");
        icon.CustomMinimumSize = new Vector2(64, 64);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
        icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(icon);

        var label = new Label();
        label.Text = description;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.CustomMinimumSize = new Vector2(220, 64);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(label);

        _enemyIntentContainer.AddChild(row);
    }

    List<string> GetNextTurnActions(int nextTurn)
    {
        var actions = new List<string>();

        string key = $"t{nextTurn}";
        if (enemyActionQueue.ContainsKey(key))
            actions.AddRange(enemyActionQueue[key]);

        if (actions.Count == 0 && enemyActionQueue.ContainsKey("default"))
            actions.AddRange(enemyActionQueue["default"]);

        foreach (var k in enemyActionQueue.Keys)
        {
            if (k.StartsWith("every") && k.EndsWith("t"))
            {
                string numStr = k.Substring(5, k.Length - 6);
                if (int.TryParse(numStr, out int interval) && interval > 0 && nextTurn % interval == 0)
                    actions.AddRange(enemyActionQueue[k]);
            }
        }

        if (enemyActionQueue.ContainsKey("ADD"))
            actions.AddRange(enemyActionQueue["ADD"]);

        return actions;
    }

    string ParseActionDescription(string action)
    {
        return ParseActionMetadata(action).description;
    }

    (string icon, string description) ParseActionMetadata(string action)
    {
        int bracketIdx = action.LastIndexOf('[');
        if (bracketIdx == -1) return ("boss", "");
        int endIdx = action.LastIndexOf(']');
        if (endIdx <= bracketIdx) return ("boss", "");
        string meta = action.Substring(bracketIdx + 1, endIdx - bracketIdx - 1);
        string icon = "boss";
        string description = "";
        foreach (string field in meta.Split(','))
        {
            int separator = field.IndexOf('=');
            if (separator <= 0) continue;
            string key = field.Substring(0, separator).Trim();
            string value = field.Substring(separator + 1).Trim();
            if (key == "icon" && !string.IsNullOrEmpty(value)) icon = value;
            else if (key == "description") description = value;
        }
        return (icon, description);
    }

    /// <summary>
    /// 执行敌方行动队列中的行动
    /// </summary>
    async Task ExecuteEnemyActionQueue()
    {
        GD.Print($"ExecuteEnemyActionQueue called at turn {turn}");
        bool hasAction = false;
        
        // 执行当前回合的行动
        string currentTurnKey = $"t{turn}";
        if (enemyActionQueue.ContainsKey(currentTurnKey))
        {
            // 快照行动列表，避免执行期间列表被修改导致迭代异常
            var turnActions = enemyActionQueue[currentTurnKey].ToList();
            foreach (var action in turnActions)
            {
                await ExecuteEnemyAction(action);
                hasAction = true;
                await Task.Delay(200);
            }
        }

        // 如果当前回合没有行动，执行default行动
        if (!hasAction && enemyActionQueue.ContainsKey("default"))
        {
            // 快照行动列表，避免执行期间列表被修改导致迭代异常
            var defaultActions = enemyActionQueue["default"].ToList();
            foreach (var action in defaultActions)
            {
                await ExecuteEnemyAction(action);
                await Task.Delay(200);
            }
        }
        
        // 执行everyXXt格式的行动（如every5t、every10t等）
        // 快照键集合，避免ExecuteEnemyAction内部修改字典导致迭代异常
        var everyKeys = enemyActionQueue.Keys.Where(k => k.StartsWith("every") && k.EndsWith("t")).ToList();
        foreach (var key in everyKeys)
        {
            // 提取数字部分
            string numberStr = key.Substring(5, key.Length - 6); // "every"长度为5，"t"长度为1
            if (int.TryParse(numberStr, out int interval))
            {
                if (interval > 0 && turn % interval == 0)
                {
                    // 快照行动列表，避免执行期间列表被修改
                    var actions = enemyActionQueue[key].ToList();
                    foreach (var action in actions)
                    {
                        await ExecuteEnemyAction(action);
                        await Task.Delay(200);
                    }
                }
            }
        }
        
        // 执行ADD行动（每回合都执行）
        if (enemyActionQueue.ContainsKey("ADD"))
        {
            // 快照行动列表，避免执行期间列表被修改导致迭代异常
            var addActions = enemyActionQueue["ADD"].ToList();
            foreach (var action in addActions)
            {
                await ExecuteEnemyAction(action);
                await Task.Delay(200);
            }
        }
    }

    /// <summary>
    /// 执行单个敌方行动
    /// </summary>
    /// <param name="action">行动字符串，格式如：addToSupportLine(t70) 或 EXEC:enemyHq|heal(20)</param>
    async Task ExecuteEnemyAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return;
        }
        
        action = action.Trim();
        GD.Print($"ExecuteEnemyAction: {action}");
        
        // 检查是否是ADD:格式的行动（添加到每回合执行的列表）
        if (action.StartsWith("ADD:"))
        {
            // ADD:addToEnemySupportLine(t34_1943) 格式
            string effectString = action.Substring(4); // 移除"ADD:"前缀

            // 将效果添加到每回合执行的列表中
            if (!enemyActionQueue.ContainsKey("ADD"))
            {
                enemyActionQueue["ADD"] = new List<string>();
            }
            enemyActionQueue["ADD"].Add(effectString);
            GD.Print($"Added effect to ADD queue: {effectString}");
            return;
        }

        // 检查是否是EXEC:格式的行动
        if (action.StartsWith("EXEC:"))
        {
            // EXEC:enemyHq|heal(20) 格式
            string effectString = action.Substring(5); // 移除"EXEC:"前缀

                
                await EnemyExecuteEffect(effectString);
        }
        else
        {
            // 直接效果格式，如 addToSupportLine(t70)
            await EnemyExecuteEffect(action);
        }
    }

    void DarkenScreen()
    {
        var endLayer = GetNode<End>("end");
        endLayer.Dim();
    }

    /// <summary>
    /// 判断攻击者是否能一击摧毁目标（基于攻击值与目标当前防御）
    /// </summary>
    bool CanDestroyTarget(cardBase_ attacker, cardBase_ target)
    {
        if (attacker == null || target == null) return false;
        return attacker.ReadAttack() >= target.ReadDefence();
    }

    /// <summary>
    /// 返回攻击者可以选择的敌方目标（遵守阵线相邻规则，除非单位类型为 Plane/Bomber/Artillery）
    /// </summary>
    private List<cardBase_> GetAllowedTargets(cardBase_ attacker)
    {
        var results = new List<cardBase_>();
        if (attacker == null) 
        {
            GD.Print("GetAllowedTargets: Attacker is null");
            return results;
        }

        // 战斗机、火炮和轰炸机可以攻击任意位置的敌军
        if (attacker.cardType == CardTypes.Plane || attacker.cardType == CardTypes.Bomber || attacker.cardType == CardTypes.Artillery)
        {
            var allEnemyUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() != attacker.GetIsFriend()).ToList();
            
            // 应用烟幕和守护特性限制
            var filteredTargets = new List<cardBase_>();
            foreach (var unit in allEnemyUnits)
            {
                // 烟幕特性：具有烟幕的单位不能成为攻击的目标
                if (unit.HasSmokeScreenActive())
                {
                    continue;
                }
                
                // 守护特性：检查目标是否被守护
                bool isProtected = IsTargetProtectedByGuardian(unit, attacker);
                if (isProtected)
                {
                    continue;
                }
                
                filteredTargets.Add(unit);
            }
            
            return filteredTargets;
        }

        var myPlace = attacker.GetMyPlace();
        if (myPlace == null) return results;

        List<place_> allowedPlaces = new List<place_>();
        
        // 步兵和坦克：如果在支援阵线，只能攻击前线单位；反之亦然
        if (attacker.cardType == CardTypes.Infantry || attacker.cardType == CardTypes.Tank)
        {
            GD.Print($"GetAllowedTargets: Infantry/Tank unit. enemySupprotLine.Contains={enemySupprotLine.Contains(myPlace)}, supportLine.Contains={supportLine.Contains(myPlace)}, frontLine.Contains={frontLine.Contains(myPlace)}");

            if (enemySupprotLine.Contains(myPlace))
            {
                // 敌方支援阵线 -> 只能攻击前线
                GD.Print("  Enemy support line -> Can attack front line");
                allowedPlaces.AddRange(frontLine);
            }
            else if (supportLine.Contains(myPlace))
            {
                // 友方支援阵线 -> 只能攻击前线
                GD.Print("  Friend support line -> Can attack front line");
                allowedPlaces.AddRange(frontLine);
            }
            else if (frontLine.Contains(myPlace))
            {
                // 前线 -> 可以攻击敌方支援阵线（友方支援阵线）
                // 根据攻击者的阵营决定攻击哪个支援阵线
                if (attacker.GetIsFriend() == IsFriend.enemy)
                {
                    // 敌方单位在前线 -> 攻击友方支援阵线
                    GD.Print("  Enemy in front line -> Can attack friend support line");
                    allowedPlaces.AddRange(supportLine);
                }
                else
                {
                    // 友方单位在前线 -> 攻击敌方支援阵线
                    GD.Print("  Friend in front line -> Can attack enemy support line");
                    allowedPlaces.AddRange(enemySupprotLine);
                }
            }
            else
            {
                // 如果单位不在任何阵线中，添加调试信息
                GD.Print($"Warning: Unit {attacker} is not in any place. myPlace: {myPlace}");
            }
        }

        GD.Print($"GetAllowedTargets: Checking {allowedPlaces.Count} allowed places");
        foreach (var place in allowedPlaces)
        {
            var c = place.GetMyCard();
            GD.Print($"  Place: {place}, Card: {c}, State: {c?.getState()}, IsFriend: {c?.GetIsFriend()}");

            if (c != null && c.getState() == CardState.placed && c.GetIsFriend() != attacker.GetIsFriend())
            {
                // 烟幕特性：具有烟幕的单位不能成为攻击的目标
                if (c.HasSmokeScreenActive())
                {
                    continue;
                }
                
                // 守护特性：检查目标是否被守护
                if (IsTargetProtectedByGuardian(c, attacker))
                {
                    continue;
                }
                
                results.Add(c);
            }
        }

        GD.Print($"GetAllowedTargets: Returning {results.Count} targets");
        return results;
    }
    
    /// <summary>
    /// 检查目标是否被守护单位保护
    /// </summary>
    private bool IsTargetProtectedByGuardian(cardBase_ target, cardBase_ attacker)
    {
        if (target == null || attacker == null) return false;

        // 火炮与轰炸机越顶射击，守护拦不住它们。这里只认攻击方兵种，
        // 所以玩家侧 Attack() 与敌方 AI 选目标两条路径天然一致。
        if (cardBase_.IgnoresGuardian(attacker.cardType)) return false;

        // 具有烟幕的单位，守护不生效
        if (target.HasSmokeScreenActive())
        {
            return false;
        }
        
        // 具有守护的单位不能被守护
        if (target.HasTrait(UnitTraits.Guardian))
        {
            return false;
        }
        
        var targetPlace = target.GetMyPlace();
        if (targetPlace == null) return false;

        // 确定目标所在的阵线
        List<place_> targetLine = null;
        if (frontLine.Contains(targetPlace))
        {
            targetLine = frontLine;
        }
        else if (supportLine.Contains(targetPlace))
        {
            targetLine = supportLine;
        }
        else if (enemySupprotLine.Contains(targetPlace))
        {
            targetLine = enemySupprotLine;
        }
        
        if (targetLine == null) return false;
        
        // 检查目标左侧是否有守护单位（在同一阵线中）
        int targetIndex = targetLine.IndexOf(targetPlace);
        if (targetIndex > 0)
        {
            var leftPlace = targetLine[targetIndex - 1];
            var leftCard = leftPlace.GetMyCard();
            if (leftCard != null && leftCard.HasTrait(UnitTraits.Guardian))
            {
                return true;
            }
        }
        
        // 检查目标右侧是否有守护单位（在同一阵线中）
        if (targetIndex < targetLine.Count - 1)
        {
            var rightPlace = targetLine[targetIndex + 1];
            var rightCard = rightPlace.GetMyCard();
            if (rightCard != null && rightCard.HasTrait(UnitTraits.Guardian))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 获取指定位置左侧的位置
    /// </summary>
    private place_ GetLeftPlace(place_ place)
    {
        if (place == null) return null;
        
        // 在前线中查找左侧位置
        int index = frontLine.IndexOf(place);
        if (index > 0) return frontLine[index - 1];
        
        // 在支援阵线中查找左侧位置
        index = supportLine.IndexOf(place);
        if (index > 0) return supportLine[index - 1];
        
        // 在敌方支援阵线中查找左侧位置
        index = enemySupprotLine.IndexOf(place);
        if (index > 0) return enemySupprotLine[index - 1];
        
        return null;
    }
    
    /// <summary>
    /// 获取指定位置右侧的位置
    /// </summary>
    private place_ GetRightPlace(place_ place)
    {
        if (place == null) return null;
        
        // 在前线中查找右侧位置
        int index = frontLine.IndexOf(place);
        if (index >= 0 && index < frontLine.Count - 1) return frontLine[index + 1];
        
        // 在支援阵线中查找右侧位置
        index = supportLine.IndexOf(place);
        if (index >= 0 && index < supportLine.Count - 1) return supportLine[index + 1];
        
        // 在敌方支援阵线中查找右侧位置
        index = enemySupprotLine.IndexOf(place);
        if (index >= 0 && index < enemySupprotLine.Count - 1) return enemySupprotLine[index + 1];
        
        return null;
    }

    /// <summary>
    /// 敌方AI最多跑几轮「移动 → 攻击」。正常情况 2 轮就收敛（第 2 轮没人动得动），
    /// 这个数只是**保险丝**：万一以后有人改了 `HaveMoved()` 不再清 `moveAble`，
    /// 循环就会永远转下去、整局卡死。宁可少打一轮也不能死循环。
    /// </summary>
    private const int MaxEnemyActionRounds = 20;

    /// <summary>等尸体收走时的轮询间隔。</summary>
    private const double DeathSettlePollSeconds = 0.1;

    /// <summary>
    /// 等尸体收走的硬上限。超过就照常往下走——宁可这一轮不推进，
    /// 也不能因为一具没收走的尸体把整个敌方回合卡死。
    /// </summary>
    private const double DeathSettleTimeoutSeconds = 3.0;

    /// <summary>
    /// 敌方回合的行动循环：
    ///
    ///     1) 让所有**可移动**的敌方单位尝试移动（推进前线）
    ///     2) 让所有**可攻击**的敌方单位尝试攻击
    ///     3) 这一轮有人真的移动过 → 回到 1)；没有 → 结束
    ///
    /// 循环的意义在于**两阶段会互相创造条件**：攻击打死我方前线单位之后，
    /// 前线才空出格子，下一轮的移动阶段才推得上去。原先是一趟走完，
    /// 「打死了也没人补位」。
    ///
    /// 循环判据是「**这一轮场上有没有发生变化**」（有人移动 或 有人攻击），
    /// **不是**「场上还有没有能移动的单位」。后者会死循环：空军/炮兵/已经在后方的
    /// 单位永远满足 `CheckIfCanMove()`，但推进阶段永远不会动它们。
    ///
    /// **判据里必须有 `anybodyAttacked`**：前线被我方单位占住时推进阶段是空转的
    /// （见 `EnemyAdvancePhaseAsync` 的提前返回），如果只看「有没有人移动」，
    /// 第 1 轮攻击打死我方前线单位之后就立刻退出，前线空出来了也不会再推——
    /// 那正是这个循环要修的场景。
    ///
    /// 收敛性：移动会 `HaveMoved()` 清 `moveAble`、攻击会 `HaveAttacked()` 减
    /// `attackAble`，两者每回合都只在开头刷一次，所以「移动 + 攻击」的总次数有限，
    /// 每转一轮至少消耗掉其中一次。`MaxEnemyActionRounds` 只是保险丝。
    /// </summary>
    async Task EnemyPerformActionsAsync()
    {
        for (int round = 0; round < MaxEnemyActionRounds; round++)
        {
            bool anybodyMoved = await EnemyAdvancePhaseAsync();
            bool anybodyAttacked = await EnemyAttackPhaseAsync();

            // 这一轮谁都动不了也打不了：再转一圈也是白转，收工
            if (!anybodyMoved && !anybodyAttacked) return;

            // 刚打完就得等尸体收走。阵亡的单位只被立刻打上 destroyed 状态，
            // 节点还要在场上留一拍（DeathPresentationDelaySeconds ≈ 1s）才 RemoveCard，
            // 这一拍里 `frontLine` 的格子还被尸体占着。不等它，下一轮的推进阶段
            // 看到的仍然是「前线有我方单位」，这一轮等于白转——正是本循环要修的场景。
            if (anybodyAttacked) await WaitForCorpsesClearedAsync();
        }

        GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} battlefield_.cs: 敌方行动跑了 "
                     + $"{MaxEnemyActionRounds} 轮还没收敛，已强制结束（检查 HaveMoved 是否还在清 moveAble）");
    }

    /// <summary>
    /// 等场上所有「已经死了、但格子还没腾出来」的单位被收走。
    ///
    /// 阵亡有两个阶段（见 `ProcessDeadUnitAsync`）：状态立刻变 `destroyed`，
    /// 节点却要等 `PlayDeathPresentationAsync` 的那一拍结束才 `RemoveCard`
    /// 解绑格子。也就是说这一拍里存在「逻辑上已死、格子上还在」的中间态，
    /// 而 `frontLine`/`Move` 都只看格子有没有卡——敌方行动循环不等它就会误判。
    ///
    /// 上限 `DeathSettleTimeoutSeconds` 是必需的：`PlayDeathPresentationAsync`
    /// 在节点已失效时会直接 return、不执行 `RemoveCard`，尸体可能永远留在
    /// `cardInPlaces` 里；没有上限就会把整个敌方回合拖死。
    /// </summary>
    private async Task WaitForCorpsesClearedAsync()
    {
        for (double waited = 0; waited < DeathSettleTimeoutSeconds; waited += DeathSettlePollSeconds)
        {
            bool corpseLeft = ReadCardInPlaces()
                .Any(c => c != null && c.getState() == CardState.destroyed);
            if (!corpseLeft) return;

            await ToSignal(GetTree().CreateTimer(DeathSettlePollSeconds), SceneTreeTimer.SignalName.Timeout);
        }

        GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} battlefield_.cs: 等了 "
                     + $"{DeathSettleTimeoutSeconds}s 还有尸体没从格子上收走，敌方行动先继续");
    }

    /// <summary>
    /// 推进阶段：前线没有我方单位时，把敌方支援线上的非空军/非炮兵单位推到前线。
    ///
    /// **返回值是「这一轮有没有人真的移动过」**。注意它返回 false 有两种含义——
    /// 「前线被占住，压根没试」和「试了但都动不了」，调用方不能把 false 当成收敛
    /// 信号（前线被占住时攻击阶段可能马上把它清空）。见 `EnemyPerformActionsAsync`。
    /// </summary>
    private async Task<bool> EnemyAdvancePhaseAsync()
    {
        bool frontHasFriend = frontLine.Any(p => p.GetMyCard() != null && p.GetMyCard().GetIsFriend() == IsFriend.friend);
        if (frontHasFriend) return false;

        bool anybodyMoved = false;
        var enemyUnits = ReadCardInPlaces()
            .Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy)
            .ToList();

        foreach (var eCard in enemyUnits)
        {
            if (eCard.isHq == HQ.hq) continue;              // 总部不能移动
            if (!eCard.CheckIfCanMove()) continue;          // CheckIfCanMove 是只读检查

            // 空军与炮兵不会主动上前线
            if (eCard.cardType is CardTypes.Plane or CardTypes.Bomber or CardTypes.Artillery) continue;
            if (frontLine.Contains(eCard.GetMyPlace())) continue;

            // 找到第一个空的前线格子
            var place = frontLine.FirstOrDefault(p => p.GetMyCard() == null);
            if (place == null) break;

            await Move(eCard, place);
            // HaveMoved 已经是「本回合不能再移动 + 非坦克也不能再攻击」的唯一入口，
            // 这里不要再补一次 HaveAttacked()——那会把 attackAble 减成 -1，
            // 让「本回合额外 +1 次攻击」的效果白给（-1 + 1 = 0）。
            eCard.HaveMoved();
            anybodyMoved = true;

            await Task.Delay(300);
        }
        return anybodyMoved;
    }

    /// <summary>
    /// 攻击阶段：按优先级对每个敌方单位尝试攻击一次。
    /// 优先级：能一击摧毁总部 → 能一击摧毁某单位 → 打总部 → 随机打一个合法目标。
    ///
    /// **返回值是「这一轮有没有人真的打出去过」**，调用方靠它决定要不要再来一轮：
    /// 攻击可能清空我方前线，从而给下一轮的推进阶段腾出格子。`Attack` 每次成功
    /// 出击都会 `HaveAttacked()` 减 `attackAble`（bin/battlefield_.cs 的 Attack 里），
    /// 所以这个 true 最多出现「全场敌方单位攻击次数之和」次，不会没完没了。
    /// </summary>
    private async Task<bool> EnemyAttackPhaseAsync()
    {
        bool anybodyAttacked = false;

        // 2) 攻击阶段：按优先级对每个敌方单位尝试攻击
        var rnd = new Random();
        var attackers = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
        GD.Print($"Enemy attack phase: Found {attackers.Count} enemy units");

        for (int i = 0; i < attackers.Count; i++)
        {
            var attacker = attackers[i];
            if (attacker == null) continue;

            // 出击的唯一入口：转发给 Attack 并记账。五处优先级分支都走它，
            // 免得漏记某一处导致循环提前收敛（那样前线清空了也不会补位）。
            async Task Strike(cardBase_ target)
            {
                await Attack(attacker, target);
                anybodyAttacked = true;
            }

            await Task.Delay(500);
            GD.Print($"Processing enemy unit: {attacker}, Type: {attacker.cardType}, Attack: {attacker.ReadAttack()}, Place: {attacker.GetMyPlace()}");

            // 如果是总部或攻击力为0则不主动攻击
            if (attacker.isHq == HQ.hq)
            {
                GD.Print($"  Skipped: Is HQ");
                continue;
            }

            // 如果攻击次数已用完，则不能攻击
            if (attacker.ReadAttackable() <= 0)
            {
                continue;
            } 
            if (attacker.ReadAttack() <= 0) 
            {
                continue;
            }

            // 获取允许的目标（遵守阵线限制，某些单位类型除外）
            var allowedTargets = GetAllowedTargets(attacker);
            GD.Print($"  Allowed targets count: {allowedTargets?.Count ?? 0}");
            if (allowedTargets == null || allowedTargets.Count == 0) 
            {
                GD.Print($"  Skipped: No valid targets");
                continue;
            }

            // 优先：如果能破坏总部且总部在允许目标中，攻击总部
            GD.Print($"  Checking HQ attack: myHq={myHq}, inTargets={allowedTargets.Contains(myHq)}, canDestroy={myHq != null && CanDestroyTarget(attacker, myHq)}");
            if (myHq != null && allowedTargets.Contains(myHq) && CanDestroyTarget(attacker, myHq))
            {
                GD.Print($"  Attacking HQ");
                await Strike(myHq);
                continue;
            }

            // 其次：能破坏任一单位则攻击该单位（仅在允许目标中寻找）
            var killable = allowedTargets.FirstOrDefault(u => CanDestroyTarget(attacker, u));
            if (killable != null)
            {
                await Strike(killable);
                continue;
            }

            // 其后：能攻击总部（非必定破坏，且总部在允许目标中）
            if (myHq != null && allowedTargets.Contains(myHq))
            {
                await Strike(myHq);
                continue;
            }

            // 最后：随机攻击一个允许的单位
            var targetRandom = allowedTargets[rnd.Next(allowedTargets.Count)];
            if (targetRandom != null)
            {
                await Strike(targetRandom);
                continue;
            }
        }

        return anybodyAttacked;
    }
async Task enemySummonAsync()
    {
        var card = enemyDeck[0];
        enemyDeck.RemoveAt(0);
        var place = GetTheFirstValidEnemyPlace();
        if(place == null)        {
            return;
        }
        await AddCardToPlace(card,place);
        await Task.Delay(500);
    }

    async Task enemySummonAsync(string id)
    {
        var place = GetTheFirstValidEnemyPlace();
        if(place == null)        {
            return;
        }
        var card = cardRes.Instantiate() as cardBase_;
            card.SetCardInformation(GetCardMaganer().GetCard(id));
            card.SetIsFriend(IsFriend.enemy);
        
        await AddCardToPlace(card,place);
        await Task.Delay(500);
    }


    place_ GetTheFirstValidEnemyPlace()
    {
        foreach(var place in enemySupprotLine)
        {
            if(place.GetMyCard()== null)
            {
                return place;
            }
        }
        return null;
    }





/// <summary>
/// 检查场上是否有没血的单位 如果有就让它去死!!!
/// </summary>
    async Task CheckIfAnyUnitDiedAsync()
    {
        if (pauseDeathCheck == 1)
        {
            return;
        }

        if (deathCheckRunning)
        {
            deathCheckRequested = true;
            return;
        }

        deathCheckRunning = true;
        try
        {
            do
            {
                deathCheckRequested = false;
                await ProcessDeadUnitsOnceAsync();
            } while (deathCheckRequested && pauseDeathCheck == 0);
        }
        finally
        {
            deathCheckRunning = false;
        }
    }

    private async Task ProcessDeadUnitsOnceAsync()
    {
        var allCards = ReadCardInPlaces().ToList();
        foreach (var card in allCards)
        {
            if (card != null) await card.ExecChangeList();
        }

        var deadUnits = allCards.Where(IsDeadPlacedUnit).ToList();
        foreach (var deadUnit in deadUnits)
        {
            await ProcessDeadUnitAsync(deadUnit);
        }

        // 待弃置的单位：先一次性关掉全部战斗能力，再错开起飞播动画。
        // 关能力要在起飞前统一做完——动画期间它们还挂在场上，不能有任何一张
        // 处在「已宣布弃置、却仍可被选去攻击」的中间态。
        var discardUnits = allCards.Where(x => x?.shouldBeRemoved == 1).ToList();
        foreach (var card in discardUnits)
        {
            card.DisableCombatAbility();
        }

        // 待弃置的单位即使**没有任何效果点名要弃它**，也会在这里被无条件移除——
        // 只要它的 shouldBeRemoved 是 1。这条日志是为了让「卡莫名被弃」当场可查：
        // 打出来的 id / 阵营 / 是不是总部，足以判断是哪个效果点名的、还是状态泄漏带进来的。
        foreach (var card in discardUnits)
        {
            GD.Print($"[Discard] 待弃置移除: id={card.id} 阵营={card.GetIsFriend()} "
                   + $"isHq={card.isHq} cardType={card.cardType} 状态={card.getState()}");
        }

        await DiscardUnitsWithStagger(discardUnits);

        deathCheckRequested |= deadUnits.Count > 0;
    }

    /// <summary>
    /// 弃置一批单位卡：卡与卡之间错开 `DiscardStaggerSeconds` 起飞，动画互相重叠，
    /// 而不是一张播完再播下一张。单张时不平白多等，行为与从前一致。
    ///
    /// 全部动画播完本函数才返回，调用方的时序（死亡检查、控制权）不受影响。
    /// </summary>
    private async Task DiscardUnitsWithStagger(List<cardBase_> cards)
    {
        var tasks = new List<Task>(cards.Count);

        // 每批重置一次层级基数：同一批里后弃的压在前面的上面，但整批都待在
        // [DiscardZBase, DiscardZMax] 这段区间里——也就是**始终在手牌下面**。
        ResetDiscardZCounter();

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            // isDiscarding 让刷新显示顺序时跳过这张卡，避免动画期间 ZIndex 被打回默认值
            card.isDiscarding = true;
            card.ZIndex = NextDiscardZIndex();
            tasks.Add(FinishUnitDiscard(card));

            if (i < cards.Count - 1)
            {
                await ToSignal(GetTree().CreateTimer(DiscardStaggerSeconds), SceneTreeTimer.SignalName.Timeout);
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>等一张单位卡的弃置动画播完后把它移出战场。</summary>
    private async Task FinishUnitDiscard(cardBase_ card)
    {
        await card.DiscardCard();
        RemoveCard(card);
    }

    private static bool IsDeadPlacedUnit(cardBase_ card)
    {
        return card != null && card.getState() == CardState.placed && card.ReadDefence() <= 0;
    }

    /// <summary>
    /// 阵亡表现：让卡在场上**停留一拍**，再消失并冒烟、爆炸。
    ///
    /// 纯表现，**不参与任何结算**——调用方不 await 它，阵亡检查的时序
    /// （`ResumeDeathCheck` / `AllowControl`）不该被一段动画拖住；
    /// 多个单位同时阵亡时也各算各的，不会一个等一个。
    ///
    /// 位置在调用时就取好了（`deathCenter`），因为下面这一步会把卡回收掉。
    /// </summary>
    private async Task PlayDeathPresentationAsync(cardBase_ card, Vector2 deathCenter)
    {
        if (DeathPresentationDelaySeconds > 0f)
            await ToSignal(GetTree().CreateTimer(DeathPresentationDelaySeconds), SceneTreeTimer.SignalName.Timeout);

        if (!IsInstanceValid(card)) return;

        RemoveCard(card);
        StartEffect("smoke", new List<Vector2> { deathCenter });
        PlayDeadSound(1);
    }

    private async Task ProcessDeadUnitAsync(cardBase_ deadUnit)
    {
        // 阵亡统计的唯一入口。上游 ProcessDeadUnitsOnceAsync 已用 IsDeadPlacedUnit
        // (state == placed 且防御 <= 0) 筛选过，因此这里出现的才是真正阵亡的单位。
        // 总部不计入战果/阵亡，其结算在 RemoveCard 中另行处理。
        if (deadUnit.isHq != HQ.hq)
        {
            if (deadUnit.GetIsFriend() == IsFriend.enemy)
            {
                if (deadUnit.cardType == CardTypes.Plane || deadUnit.cardType == CardTypes.Bomber)
                    _battleEnemyAirKilled++;
                else
                    _battleEnemyLandKilled++;
            }
            else if (deadUnit.GetIsFriend() == IsFriend.friend)
            {
                _battleFriendlyDead++;
            }
        }

        if (deadUnit.GetIsFriend() == IsFriend.friend &&
            deadUnit.cardType is CardTypes.Infantry or CardTypes.Tank or CardTypes.Artillery)
        {
            lastDeadFriendlyLandUnitId = deadUnit.id ?? "";
        }

        await TriggerUnitEffects("Dead", deadUnit, checkOnlySourceCard: true);
        IsFriend side = deadUnit.GetIsFriend();
        Vector2 deathCenter = GetCardCenter(deadUnit);

        // 立刻把状态打成 destroyed，但节点先在场上留一拍才消失（见 PlayDeathPresentationAsync）。
        // 这一步是延迟的关键前提，不是可有可无的标记：
        //   · IsDeadPlacedUnit 要求 state == placed，所以不会被下一轮死亡检查重复统计；
        //   · TriggerUnitEffects 只挑 state == placed 的单位（battlefield_.cs:1937），
        //     所以下面那两条死亡触发不会点到这具「还没消失的尸体」。
        // 战斗能力也一并关掉：这一拍里它已经没有防御力了，不该还能被选去做什么。
        deadUnit.setState(CardState.destroyed);
        deadUnit.DisableCombatAbility();

        _ = PlayDeathPresentationAsync(deadUnit, deathCenter);

        if (side == IsFriend.friend) await TriggerUnitEffects("FriendlyUnitDead", deadUnit);
        else if (side == IsFriend.enemy) await TriggerUnitEffects("EnemyUnitDead", deadUnit);
    }


            /// <summary>
            /// 暂时用作测试
            /// </summary>
    public async void OnNextTurnButtonPressed()
    {
        if (turnTransitionRunning) return;
        turnTransitionRunning = true;
        ForbidControl();
        try
        {
            await RunTurnTransitionAsync();
        }
        finally
        {
            turnTransitionRunning = false;
            AllowControl();
        }
    }


/// <summary>
/// 切换回合.这个函数主要用作点击next turn按钮点击后从己方回合->敌方回合->己方回合的全过程
/// </summary>
/// <returns></returns>
    private async Task RunTurnTransitionAsync()
    {
        // 触发友方回合结束时点
        await TriggerUnitEffects("FriendlyTurnEnd", null);

        // 友方回合结束时，移除友方单位的压制
        foreach (var unit in cardInPlaces.Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend))
        {
            if (unit.HasTrait(UnitTraits.Suppressed))
                unit.RemoveTrait(UnitTraits.Suppressed);
        }

        // 触发双方回合结束时点
        await TriggerUnitEffects("TurnEnd", null);
        await CheckIfAnyUnitDiedAsync(); // 检查死亡
        
        
        // 触发敌方回合开始时点
        await TriggerUnitEffects("EnemyTurnBegin", null);
        
        // 增加所有已部署单位的存活回合数
        foreach(var card in cardInPlaces.Where(x=>x.getState()==CardState.placed).Where(x=>x.isFriend==IsFriend.enemy).ToList())
        {
            card.IncrementLifeTime();
        }
        
        // 触发双方回合开始时点
        await TriggerUnitEffects("TurnBegin", null);
        await EnemyTurnAsync();
        
        // 触发友方回合开始时点
        // 友方回合开始：刷新友方单位。
        // 放在 FriendlyTurnBegin 时点之前，理由有二：
        // ① 与从前「敌方回合开头刷全部」的时序一致，时点里读到的 attackCountThisTurn 仍是 0；
        // ② 敌方回合里被效果刷进场的单位，到这里才终于获得行动能力。
        RefreshCardsInField(IsFriend.friend);

        await TriggerUnitEffects("FriendlyTurnBegin", null);
        await TriggerUnitEffects("TurnBegin", null);

        // 回合开始时trait处理
        await ApplyTurnStartTraits();

        // 增加所有已部署单位的存活回合数
        foreach(var card in cardInPlaces.Where(x=>x.getState()==CardState.placed).Where(x=>x.isFriend==IsFriend.friend).ToList())
        {
            card.IncrementLifeTime();
        }
        
        await CheckIfAnyUnitDiedAsync(); // 检查死亡

        
        
        await player1.DrawCard();
        player1.AddPointMaxNatural();

        int pendingLoss = ReadMemory("pendingPointLoss");
        if (pendingLoss > 0)
        {
            player1.AddPoint(-pendingLoss);
            SetMemory("pendingPointLoss", 0);
        }

        RefreshEnemyIntentPanel();
    }

    /// <summary>
    /// 刷新场上一方的单位，恢复其移动与攻击次数。
    ///
    /// **必须按阵营分开刷，且各自只在「自己的回合开始」刷自己那一方。**
    /// 从前这里是一次性刷全部、而且只在 `EnemyTurnAsync()` 开头调一次，
    /// 于是敌方回合里被效果刷进场的单位（如近卫步兵272团亡计拉出的 IS-2）
    /// 撑到友方回合开始时还没被刷过——`cardBase_._Ready()` 给的是
    /// `attackAble = 0 / moveAble = 0`，只有闪击会在入场时补刷一次，
    /// 所以它整回合动弹不得。
    ///
    /// 改成各刷各的之后：友方回合入场的单位，下个友方回合开始才能行动；
    /// 敌方回合入场的单位，紧接着的友方回合开始就能行动。
    /// 从手牌部署的召唤失调不受影响——部署发生在本次刷新之后，当回合依然不能动。
    /// </summary>
    void RefreshCardsInField(IsFriend side)
    {
        foreach (var card in cardInPlaces
                     .Where(x => x != null && x.getState() == CardState.placed && x.GetIsFriend() == side)
                     .ToList())
        {
            card.RefreshUnit();
        }
    }

    /// <summary>
    /// 回合开始时处理trait效果：动员buff、伏击重置、烟幕前线检查、被守护刷新
    /// </summary>
    private async Task ApplyTurnStartTraits()
    {
        foreach (var card in cardInPlaces.Where(x => x.getState() == CardState.placed).ToList())
        {
            if (card == null) continue;

            // 动员：友方回合开始时+1攻击+1防御
            if (card.GetIsFriend() == IsFriend.friend && card.HasMobilizeActive())
            {
                card.AddChange(ChangeType.GetAttack, 1);
                card.AddChange(ChangeType.GetDefence, 1);
            }
        }

        // 执行动员buff
        await ExecuteChangeLists();

        // 烟幕前线检查：前线的单位无法具有烟幕
        foreach (var place in frontLine)
        {
            var card = place.GetMyCard();
            if (card != null && card.HasSmokeScreenActive())
            {
                card.RemoveSmokeScreen();
            }
        }

        // 刷新被守护状态
        RefreshAllBeGuardianedStatus();
    }

    /// <summary>
    /// 敌方回合开始时处理trait效果：敌方动员buff
    /// </summary>
    private async Task ApplyEnemyTurnStartTraits()
    {
        foreach (var card in cardInPlaces.Where(x => x.getState() == CardState.placed).ToList())
        {
            if (card == null) continue;

            // 动员：敌方回合开始时+1攻击+1防御
            if (card.GetIsFriend() == IsFriend.enemy && card.HasMobilizeActive())
            {
                card.AddChange(ChangeType.GetAttack, 1);
                card.AddChange(ChangeType.GetDefence, 1);
            }
        }

        await ExecuteChangeLists();
    }

    /// <summary>
    /// 刷新所有单位的被守护状态
    /// </summary>
    private void RefreshAllBeGuardianedStatus()
    {
        foreach (var card in cardInPlaces.Where(x => x.getState() == CardState.placed && x.isHq != HQ.hq))
        {
            if (card == null) continue;
            bool guarded = IsUnitProtectedByGuardian(card);
            card.SetBeGuardianed(guarded);
        }
    }

    /// <summary>
    /// 检查单个单位是否被守护（左或右有守护单位）
    /// </summary>
    private bool IsUnitProtectedByGuardian(cardBase_ unit)
    {
        if (unit == null) return false;
        if (unit.HasSmokeScreenActive()) return false;
        if (unit.HasTrait(UnitTraits.Guardian)) return false;

        var myPlace = unit.GetMyPlace();
        if (myPlace == null) return false;

        // 确定所在阵线
        List<place_> line = null;
        if (frontLine.Contains(myPlace)) line = frontLine;
        else if (supportLine.Contains(myPlace)) line = supportLine;
        else if (enemySupprotLine.Contains(myPlace)) line = enemySupprotLine;
        if (line == null) return false;

        int idx = line.IndexOf(myPlace);
        // 检查左侧
        if (idx > 0)
        {
            var leftCard = line[idx - 1].GetMyCard();
            if (leftCard != null && leftCard.HasTrait(UnitTraits.Guardian))
                return true;
        }
        // 检查右侧
        if (idx < line.Count - 1)
        {
            var rightCard = line[idx + 1].GetMyCard();
            if (rightCard != null && rightCard.HasTrait(UnitTraits.Guardian))
                return true;
        }
        return false;
    }

    public void RemoveCard(cardBase_ card)
    {
        if (card == null) return;
        if (card.GetIsFriend() == IsFriend.friend && card.isHq == HQ.hq && !defeatTransitionStarted)
        {
            defeatTransitionStarted = true;

            // 战役模式下失败不再立刻结束本局：先按规则扣血（area7 清零 / boss 战 -2 /
            // 其余 -1，规则集中在 BattleStateManager.LoseHpOnBattleDefeat），
            // 血还有剩就回世界地图继续，归零才走「游戏结束」回主菜单并重置进度。
            // 非战役模式（直接跑战场场景调试）没有血量概念，维持原行为。
            if (BattleStateManager.IsCampaignMode)
            {
                int hpBefore = BattleStateManager.Hp;
                int hpLeft = BattleStateManager.LoseHpOnBattleDefeat();
                GD.Print($"[Battle] 战斗失败：area={BattleStateManager.SelectedArea} "
                       + $"关卡={BattleStateManager.SelectedEnemy} boss={BattleStateManager.IsBossBattle()} "
                       + $"血量 {hpBefore}→{hpLeft}");

                if (hpLeft > 0) _ = ReturnToWorldMapAfterDefeat(hpBefore - hpLeft, hpLeft);
                else _ = ReturnToStartMenuAfterDefeat();
            }
            else
            {
                _ = ReturnToStartMenuAfterDefeat();
            }
        }
        if(card.GetIsFriend()== IsFriend.enemy && card.isHq == HQ.hq)
        {
            CalculateMaterialPoints();
            DarkenScreen();
            if (BattleStateManager.IsCampaignMode)
            {
                _ = ReturnToWorldMapAfterVictory();
            }
        }

        // 阵亡统计不在此处进行：RemoveCard 是通用的卡牌移除函数，弃牌、指令卡
        // 结算、ShowCardChoice 清理选项卡、手牌溢出等路径都会调用它。若在此计数，
        // 这些非阵亡的移除会被一并算作战果/阵亡，并连带影响 CalculateMaterialPoints。
        // 真正的计数在 ProcessDeadUnitAsync（死亡流程的唯一入口）。

        cardInPlaces.Remove(card);
        card.Dead();
        RefreshAllBeGuardianedStatus();
        _displayOrderDirty = true;
    }

    // ============================ 暂停菜单 ============================

    /// <summary>
    /// 打开暂停菜单（ESC 或左上角的设置按钮，两个入口都走这里）。
    ///
    /// 打开期间 `ForbidControl()` 锁住战场输入——菜单盖在上面，底下还能拖牌就乱套了。
    /// 关掉时 `AllowControl()` 解锁，两个必须成对（`ForbidControl` 是计数的，
    /// 所以这里只会抵消自己那一次）。
    /// </summary>
    private void OpenPauseMenu()
    {
        if (PauseMenu.IsOpen) return;

        // 结算/失败面板已经盖住了就不叠第二层（那时 ForbidControl 也早被调过，
        // 再锁一次会让回合结束时的解锁算错账）。
        var endNode = GetNodeOrNull<End>("end");
        if (endNode != null && endNode.Visible) return;

        ForbidControl();
        PauseMenu.Show(this,
            action1: new PauseAction
            {
                Text = "认输",
                ConfirmText = "认输会立刻让总部沦陷，并按规则扣血。确定吗？",
                OnPressed = SurrenderAsync,
            },
            action2: new PauseAction
            {
                Text = "保存并退出",
                OnPressed = SaveAndQuitAsync,
            },
            onClosed: AllowControl);
    }

    /// <summary>
    /// **认输**：把玩家总部防御打到 0，然后走**既有**的死亡判定。
    ///
    /// 不另写一套「失败」——`RemoveCard(myHq)` 里那条链路已经管着扣血规则
    /// （area7 清零 / boss -2 / 其余 -1）、结算面板、以及血尽时的进度重置。
    /// 复制一套的话，以后改血量规则就会漏掉认输这条路。
    ///
    /// 烈度是在**战斗结束时**才消耗的（见 `ReturnToWorldMapAfterVictory`），
    /// 所以认输之后这一场照样算数，不会被白打。
    /// </summary>
    private async Task SurrenderAsync()
    {
        if (myHq == null || !IsInstanceValid(myHq) || defeatTransitionStarted) return;

        GD.Print($"[Battle] 认输：{BattleStateManager.SelectedEnemy} 玩家总部防御归零");
        await myHq.LoseDefence(myHq.ReadDefence());
        await CheckIfAnyUnitDiedAsync();
    }

    /// <summary>
    /// **保存并退出**：把当前进度写盘，然后回主菜单。
    ///
    /// 存的是「这一场是哪一场」（`SelectedEnemy`）+ 整个世界地图进度。
    /// **战斗内的棋盘不存**——读档是重新打这一场，不是从半途接着下（见 `SaveManager`）。
    /// </summary>
    private async Task SaveAndQuitAsync()
    {
        SaveManager.Save();
        await SceneLoader.ChangeSceneAsync(this, "res://bin/start_menu.tscn");
    }

    // ============================ 转换 ============================

    /// <summary>`EffectRegistry` 里转换特效的名字。卡上写 `convert(卡id)` 就是这个。</summary>
    private const string ConvertEffectName = "convert";

    /// <summary>
    /// **播放一次转换**：抬起 → 翻面（露卡背）→ 再翻面（换成新单位）→ 落回。
    ///
    /// 这里**必须 await**，不能像攻击特效那样 fire-and-forget：
    /// 卡牌数据是在**第二次翻面那一刻**才换掉的（见 `ConvertEffect.ApplyNewUnit`），
    /// 不 await 的话，`convert(x)|GetAttack(2)` 这种写法会把 +2 加在**旧卡**上，
    /// 然后被转换整个盖掉——不报任何错，只是数值凭空少了一截。
    ///
    /// 用 `EffectRegistry.PlayOnceAsync` 而不是 `StartEffect`：后者是「不等」的版本。
    /// </summary>
    private Task ConvertCardAsync(cardBase_ card, string newCardId)
    {
        if (card == null || !IsInstanceValid(card)) return Task.CompletedTask;
        if (card.isHq == HQ.hq)
        {
            GD.PushWarning($"{Time.GetDatetimeStringFromSystem()} battlefield_.cs: 总部不能被转换，已跳过");
            return Task.CompletedTask;
        }

        return EffectRegistry.PlayOnceAsync(this, ConvertEffectName, newCardId,
                                            new List<Vector2> { GetCardCenter(card) }, card);
    }

    private async Task ReturnToStartMenuAfterDefeat()
    {
        ForbidControl();
        var endNode = GetNodeOrNull<End>("end");
        if (endNode != null) await endNode.ShowDefeat();
        // 整局结束：回主菜单前重置本局进度，避免下一局继承卡组与区域状态
        BattleStateManager.ResetCampaignProgress();
        await SceneLoader.ChangeSceneAsync(this, "res://bin/start_menu.tscn");
    }

    private void CalculateMaterialPoints()
    {
        int hqCurrentDef = myHq != null ? myHq.ReadDefence() : 0;
        int hqLost = _battleHqDefenceStart - hqCurrentDef;
        if (hqLost < 0) hqLost = 0;

        // 对敌方总部累计造成的伤害。取自卡牌自身的 LoseDefence 累计值，
        // 治疗不会抵消它——加里宁那种"边打边回血"的关卡才能如实计入。
        int enemyHqDamage = enemyHq != null ? enemyHq.ReadTotalDefenceLost() : 0;

        // 系数集中在 BattleScore，结算面板用同一组函数渲染明细，
        // 避免两处各写一遍导致明细与总额对不上
        int gained = BattleScore.Total(_battleEnemyLandKilled, _battleEnemyAirKilled, _battleFriendlyDead, hqLost, enemyHqDamage);

        BattleStateManager.MaterialPoints += gained;
        BattleStateManager.LastBattleLandKilled = _battleEnemyLandKilled;
        BattleStateManager.LastBattleAirKilled = _battleEnemyAirKilled;
        BattleStateManager.LastBattleFriendlyDead = _battleFriendlyDead;
        BattleStateManager.LastBattleHqDefenceLost = hqLost;
        BattleStateManager.LastBattleEnemyHqDamage = enemyHqDamage;
        BattleStateManager.LastBattlePointsGained = gained;

        GD.Print($"[MaterialPoints] land={_battleEnemyLandKilled} air={_battleEnemyAirKilled} dead={_battleFriendlyDead} hqLost={hqLost} enemyHqDmg={enemyHqDamage} gained={gained} total={BattleStateManager.MaterialPoints}");
    }

    /// <summary>
    /// 战役模式下敌方总部被摧毁后，标记区域已完成并返回世界地图
    /// </summary>
    /// <summary>
    /// 战败但血量未归零：显示撤退面板，扣掉本次的区域烈度，回世界地图继续本局。
    ///
    /// 与战胜路径的差别有三处，都是刻意的：
    ///   1. 不发战后奖励（PostBattleReward 只在战胜后走）；
    ///   2. 不结算物资点（CalculateMaterialPoints 只由敌方总部阵亡触发）；
    ///   3. 烈度**照常消耗 1 点**（需求方确认）——所以归零时同样解锁下一区域，
    ///      否则玩家可以靠一直输来无限重试、区域永远推不下去。
    /// </summary>
    private async Task ReturnToWorldMapAfterDefeat(int hpLost, int hpLeft)
    {
        ForbidControl();

        var endNode = GetNodeOrNull<End>("end");
        if (endNode != null) await endNode.ShowRetreat(hpLost, hpLeft);

        BattleStateManager.ConsumeAreaIntensity(BattleStateManager.SelectedArea);
        BattleStateManager.IsCampaignMode = false;

        await SceneLoader.ChangeSceneAsync(this, "res://bin/worldMap.tscn");
    }

    private async System.Threading.Tasks.Task ReturnToWorldMapAfterVictory()
    {
        var endNode = GetNodeOrNull<End>("end");
        if (endNode != null)
            await endNode.ShowSettlement(
                BattleStateManager.LastBattleLandKilled,
                BattleStateManager.LastBattleAirKilled,
                BattleStateManager.LastBattleFriendlyDead,
                BattleStateManager.LastBattleHqDefenceLost,
                BattleStateManager.LastBattleEnemyHqDamage,
                BattleStateManager.LastBattlePointsGained);

        // 战斗胜利消耗1点区域烈度；归零时解锁下一区域
        string clearedArea = BattleStateManager.SelectedArea;
        bool areaCleared = BattleStateManager.ConsumeAreaIntensity(clearedArea);
        bool campaignCompleted = areaCleared && BattleStateManager.IsFinalArea(clearedArea);

        await PostBattleReward.Show(this, player1);

        BattleStateManager.IsCampaignMode = false;

        if (campaignCompleted)
            await CampaignVictory.ShowAndReturnToMenu(this);
        else
            await SceneLoader.ChangeSceneAsync(this, "res://bin/worldMap.tscn");
    }

    /// <summary>
    /// 同仇特性：当拥有同仇的单位被指向时，使所有其他友方同仇单位获得+1攻击+1防御
    /// </summary>
    private async Task TriggerSharedHatred(cardBase_ targetedUnit)
    {
        if (targetedUnit == null) return;
        if (!targetedUnit.HasTrait(UnitTraits.SharedHatred)) return;

        var sameSide = targetedUnit.GetIsFriend();
        var sharedHatredUnits = ReadCardInPlaces()
            .Where(x => x.getState() == CardState.placed
                     && x.GetIsFriend() == sameSide
                     && x.HasTrait(UnitTraits.SharedHatred)
                     && x != targetedUnit)
            .ToList();

        if (sharedHatredUnits.Count == 0) return;

        foreach (var unit in sharedHatredUnits)
        {
            unit.AddChange(ChangeType.GetAttack, 1);
            unit.AddChange(ChangeType.GetDefence, 1);
        }

        await ExecuteChangeLists();
    }

    private async Task ResolveTargetedCommandAsync(cardBase_ source, cardBase_ target)
    {
        if (source == null || target == null) return;
        await TriggerUnitEffects("BePicked", target, new List<cardBase_> { source }, checkOnlySourceCard: true);
        await TriggerSharedHatred(target);
        await ParseAndExecuteEffect(source.effect, source, new List<cardBase_> { target });
        await CheckIfAnyUnitDiedAsync();
    }

    /// <summary>
    /// 阵亡/弃置动画期间用的层级区间。**下限要高于场上卡（10），上限必须低于手牌（20）。**
    ///
    /// 从前这里是 `private int _discardZCounter = 50;`——从 50 起一路递增，
    /// 于是弃牌动画整段都压在**手牌上面**：玩家正要点的牌被一张正在飞走的牌盖住。
    /// 卡牌层级的总约定见 `docs/NOTICE.md`。
    /// </summary>
    private const int DiscardZBase = 11;
    private const int DiscardZMax = 19;

    private int _discardZCounter = DiscardZBase;

    /// <summary>
    /// 取下一个弃置动画用的层级。超出区间就**封顶**——宁可几张同层，
    /// 也不许因为"后弃的要在上面"而爬到手牌之上。
    /// </summary>
    private int NextDiscardZIndex() => Math.Min(_discardZCounter++, DiscardZMax);

    /// <summary>一批弃置开始前重置，让同一批里的先后顺序从下往上排。</summary>
    private void ResetDiscardZCounter() => _discardZCounter = DiscardZBase;

    /// <summary>
    /// 播放弃牌动画并在完成后移除（fire-and-forget，Z-index递增确保后弃置的在上方）
    /// </summary>
    public async Task CardDiscardAndRemove(cardBase_ card)
    {
        card.isDiscarding = true;
        card.ZIndex = NextDiscardZIndex();
        await card.DiscardCard();
        RemoveCard(card);
    }

    /// <summary>
    /// 高亮合法的攻击目标，其他单位变成灰色
    /// </summary>
    public void HighlightValidAttackTargets(cardBase_ attacker)
    {
        var validTargets = GetAllowedTargets(attacker);
        foreach (var card in cardInPlaces.Where(x=>x.getState()==CardState.placed).ToList())
        {
            if (validTargets.Contains(card))
            {
                card.RestoreColor();
            }
            else
            {
                card.SetGrayscale();
            }
        }
    }

        void TestButtonPressed()
    {
        player1.AddCardToHand(cardMaganer.GetCard("i18"));
        player1.AddCardToHand(cardMaganer.GetCard("血红的镰刀"));
    }

        void OnButtonTest2Pressed()
    {
        BattleStateManager.Deck = player1.ReadMyDeck();
        BattleStateManager.battlefield = this;
        // 以叠加方式加载卡组展示界面，CanvasLayer确保渲染在所有元素之上
        var canvasLayer = new CanvasLayer();
        canvasLayer.Layer = 1;
        AddChild(canvasLayer);
        var displayScene = ResourceLoader.Load<PackedScene>("res://bin/display_card.tscn");
        var displayCard = displayScene.Instantiate();
        canvasLayer.AddChild(displayCard);
    }

    /// <summary>
    /// 根据指定的TargetType高亮符合条件的单位，其他单位变成灰色
    /// </summary>
    public void HighlightValidTargets(TargetType targetType)
    {
        // 阵亡后暂留在场上的卡（destroyed，但节点还在）也要一起变灰：
        // 它已经不是合法目标，单独留着原色会被看成「这个能打」。
        foreach (var card in cardInPlaces
                     .Where(x => x.getState() is CardState.placed or CardState.destroyed).ToList())
        {
            if (IsValidTarget(card, targetType))
            {
                card.RestoreColor();
            }
            else
            {
                card.SetGrayscale();
            }
        }
    }

/// <summary>
/// 自动计算这张卡在当前的场上有多少个合法的目标
/// </summary>
/// <param name="t"></param>
/// <returns></returns>
    private int GetHowManyCardIsValid(TargetType t)
    {
        int counter = 0;
        foreach (var a in allPlaces)
        {
            if(a.GetMyCard()!= null)
            {
                if (IsValidTarget(a.GetMyCard(), t))
                {
                    counter++;
                }
            }
        }
        return counter;
    }

    /// <summary>
    /// 这张卡还能不能被玩家选中 / 当成目标。
    ///
    /// 阵亡的卡会在场上**停留 `DeathPresentationDelaySeconds` 秒才消失**
    /// （见 `PlayDeathPresentationAsync`）：那一拍里它的状态已经是 `destroyed`，
    /// 但节点**还挂在 `cardInPlaces` 和它自己的格子上**。手快就能点中它——
    /// 能当攻击目标、能当指令目标，还会被算进「合法目标有几个」。
    /// 它实际上已经死了，不该再参与任何选择。
    ///
    /// 判据只写这一处：`IsValidTarget`、`CheckCardClick`、攻击落点校验、
    /// 目标高亮全都引用它，免得同一个规则在四个地方各写一遍（规范 E）。
    /// </summary>
    private static bool CanBeSelected(cardBase_ card)
        => card != null && card.getState() != CardState.destroyed;

    /// <summary>
    /// 检查卡牌是否匹配指定的TargetType。
    ///
    /// 「能不能被选中」也归这里管（第一句）：高亮、目标计数、指令落点校验
    /// 全都走本函数，那是唯一一处能一次堵住三个口子的地方。
    /// </summary>
    private bool IsValidTarget(cardBase_ card, TargetType targetType)
    {
        if (!CanBeSelected(card)) return false;

        switch (targetType)
        {
            case TargetType.aPlace:
                return true; // 任何在场上的卡
            case TargetType.aUnit:
                return card.isHq == HQ.normalCard;
            case TargetType.aFriendlyUnit:
                return card.GetIsFriend() == IsFriend.friend && card.isHq == HQ.normalCard;
            case TargetType.anEnemyUnit:
                return card.GetIsFriend() == IsFriend.enemy && card.isHq == HQ.normalCard;
            case TargetType.aFriendlyTank:
                return card.GetIsFriend() == IsFriend.friend && card.cardType == CardTypes.Tank;
            case TargetType.aFriendlyInfantry:
                return card.GetIsFriend() == IsFriend.friend && card.cardType == CardTypes.Infantry;
            case TargetType.aFriendlyPlane:
                return card.GetIsFriend() == IsFriend.friend && card.cardType == CardTypes.Plane;
            case TargetType.aFriendlyBomber:
                return card.GetIsFriend() == IsFriend.friend && card.cardType == CardTypes.Bomber;
            case TargetType.aFriendlyArtillery:
                return card.GetIsFriend() == IsFriend.friend && card.cardType == CardTypes.Artillery;
            case TargetType.anEnemyTank:
                return card.GetIsFriend() == IsFriend.enemy && card.cardType == CardTypes.Tank;
            case TargetType.anEnemyInfantry:
                return card.GetIsFriend() == IsFriend.enemy && card.cardType == CardTypes.Infantry;
            case TargetType.anEnemyPlane:
                return card.GetIsFriend() == IsFriend.enemy && card.cardType == CardTypes.Plane;
            case TargetType.anEnemyBomber:
                return card.GetIsFriend() == IsFriend.enemy && card.cardType == CardTypes.Bomber;
            case TargetType.anEnemyArtillery:
                return card.GetIsFriend() == IsFriend.enemy && card.cardType == CardTypes.Artillery;
            case TargetType.aFriendlyLandUnit:
                return card.GetIsFriend() == IsFriend.friend && (card.cardType == CardTypes.Infantry || card.cardType == CardTypes.Tank || card.cardType == CardTypes.Artillery);
            case TargetType.anEnemyLandUnit:
                return card.GetIsFriend() == IsFriend.enemy && (card.cardType == CardTypes.Infantry || card.cardType == CardTypes.Tank || card.cardType == CardTypes.Artillery);
            case TargetType.aFriendlyAirUnit:
                return card.GetIsFriend() == IsFriend.friend && (card.cardType == CardTypes.Plane || card.cardType == CardTypes.Bomber);
            case TargetType.anEnemyAirUnit:
                return card.GetIsFriend() == IsFriend.enemy && (card.cardType == CardTypes.Plane || card.cardType == CardTypes.Bomber);
            case TargetType.anEnemyDamagedUnit:
                return card.GetIsFriend() == IsFriend.enemy && card.isHq == HQ.normalCard && card.ReadDefence() < card.ReadMaxHistoryDefence();
            case TargetType.myHq:
                return card.GetIsFriend() == IsFriend.friend && card.isHq == HQ.hq;
            case TargetType.enemyHq:
                return card.GetIsFriend() == IsFriend.enemy && card.isHq == HQ.hq;
            case TargetType.anyTarget:
                return true; // 任何目标都有效
            case TargetType.friendlyTarget:
                return card.GetIsFriend() == IsFriend.friend;
            case TargetType.enemyTarget:
                return card.GetIsFriend() == IsFriend.enemy;
            case TargetType.aFrontLineUnit:
                // 前线单位（任意阵营）。高亮与目标计数都委托本函数，故只需在此加一处
                return card.isHq == HQ.normalCard && card.GetMyPlace() != null
                       && frontLine.Contains(card.GetMyPlace());
            default:
                return false;
        }
    }

    /// <summary>
    /// 撤销高亮变化，恢复所有单位的原始颜色
    /// </summary>
    public void RestoreAllTargetsColor()
    {
        foreach (var card in cardInPlaces)
        {
            card.RestoreColor();
        }
    }

    /// <summary>
    /// 解析并执行卡牌效果
    /// 效果字符串格式: [时间前缀:]指令|指令|..., 指令|指令|...
    /// 逗号分割优先级大于竖线 逗号用于分割多个独立的效果段
    /// 示例: deployed:Heal(3) 或 setResult(3)|drawCard 或 damage(6)|setTarget
    ///      或 addToSupportLine(t70), addToSupportLine(t70)
    /// 
    /// 支持的指令:
    /// - Heal(n) - 增加目标防御力n点 使用AddChange
    /// - damage(n) - 减少目标防御力n点 使用AddChange
    /// - addDefence(n) - 增加目标防御力n点 使用AddChange
    /// - SetMemory(s,n) - 设置自定义变量s的值
    /// - damage - 根据result值减少防御力 使用AddChange
    /// - drawCard - 抽result张卡
    /// - setResult(n) - 设置result值为n
    /// - getCount(${selector}) - 获取符合条件的单位数量 设置为result
    /// - setTarget - 设置targets为传入的targetCard
    /// - addToSupportLine(cardId) - 向友方支援阵线添加卡
    /// - addToDeck(cardId) - 将指定卡牌洗入卡组
    /// - this - 指代执行效果的单位本身（目标列表）
    /// - target - 指代触发此效果的单位或目标（可以是列表）
    /// - if(条件)标签 - 条件跳转，满足条件时跳转到指定标签处执行
    /// - 标签: - 定义跳转标签（如 A:）
    ///
    /// 支持的条件格式:
    /// - result>n, result>=n, result<n, result<=n, result==n, result!=n
    /// - targets.count>n, targets.count>=n, targets.count<n, targets.count<=n, targets.count==n, targets.count!=n
    /// - target.isFriend, target.isEnemy
    /// - target.type==类型名 - 判断目标类型
    /// - target.cost>n, target.cost>=n, target.cost<n, target.cost<=n, target.cost==n, target.cost!=n - 判断目标费用
    /// - target.attack>n, target.attack>=n, target.attack<n, target.attack<=n, target.attack==n, target.attack!=n - 判断目标攻击力
    /// - target.defence>n, target.defence>=n, target.defence<n, target.defence<=n, target.defence==n, target.defence!=n - 判断目标防御力
    /// - source.isFriend, source.isEnemy
    /// - &variableName - 读取自定义变量，未定义时自动初始化为0
    ///
    /// 条件跳转示例:
    /// - setResult(3)|if(result>2)A|damage(5)|A:|Heal(10)
    ///   如果result大于2，跳过damage(5)直接执行Heal(10)
    /// </summary>
    /// <param name="effectString">效果字符串</param>
    /// <param name="sourceCard">效果发起者（this - 执行效果的单位）</param>
    /// <param name="targetCards">效果目标列表（target - 触发此效果的单位列表）</param>
    /// <param name="targetCard">单个目标（向后兼容，可选）</param>
    public async Task ParseAndExecuteEffect(string effectString, cardBase_ sourceCard, List<cardBase_> targetCards = null, cardBase_ targetCard = null)
    {
        if (string.IsNullOrEmpty(effectString))
        {
            return;
        }

        // 入口处统一剥离所有引号外的[...]元数据，后续无需处理[]
        effectString = StripBracketsOutsideQuotes(effectString);

        // 移除时间前缀 如 "deployed:"，但要考虑引号内的冒号
        if (effectString.Contains(":"))
        {
            bool inQuotes = false;
            char quoteChar = '\0';
            int colonIndex = -1;
            for (int i = 0; i < effectString.Length; i++)
            {
                char c = effectString[i];
                if ((c == '"' || c == '\'') && !inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (c == quoteChar && inQuotes)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                }
                else if (c == ':' && !inQuotes)
                {
                    colonIndex = i;
                    break;
                }
            }
            if (colonIndex != -1)
            {
                effectString = effectString.Substring(colonIndex + 1);
            }
        }

        // 首先用逗号分割 逗号分割优先级更高，但要忽略括号内的逗号
        var effectSegments = SplitEffectString(effectString);

        // 在循环外初始化targets，使得setTarget指令设置的targets可以在后续的segment中保留
        List<cardBase_> targets = [];
        int result = 0;
        
        // 初始化目标列表：优先使用 targetCards（新的单位效果参数），否则使用 targetCard（向后兼容参数）
        if (targetCards != null && targetCards.Count > 0)
        {
            targets = new List<cardBase_>(targetCards);
        }
        else if (targetCard != null)
        {
            targets = new List<cardBase_> { targetCard };
        }
        
        foreach (var segment in effectSegments)
        {
            // 重置result，但不重置targets，使得setTarget指令设置的targets可以在后续的segment中保留
            result = 0;

            // 对每个逗号分割的效果段 再用 "|" 分割多个指令，但要考虑引号和括号内的内容
            var parts = SplitEffectString(segment, '|');
            
            // 收集所有标签及其索引
            Dictionary<string, int> labels = new Dictionary<string, int>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.EndsWith("&"))
                {
                    string label = part.Substring(0, part.Length - 1);
                    labels[label] = i;
                }
            }

            // foreach/End& 结构支持（可嵌套）
            var foreachStack = new Stack<(List<cardBase_> savedTargets, int currentIndex, int loopStartIndex)>();
            List<cardBase_> preForeachTargets = null;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                string instruction = part.Trim();
                string ins = instruction.ToLowerInvariant(); // 大小写不敏感

                // 跳过标签定义（End& 需要被处理以支持 foreach 结构）
                // if(condition)label& 不是标签定义，是条件跳转指令
                if (instruction.EndsWith("&") && ins != "end&" && !instruction.StartsWith("if(", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // foreach - 进入循环并保存当前 targets
                if (ins == "foreach")
                {
                    var savedTargets = new List<cardBase_>(targets);

                    // 空列表时跳过整个循环体，避免循环体内条件错误求值
                    if (savedTargets.Count == 0)
                    {
                        int endIndex = FindMatchingEndAndForIndex(parts, i);
                        if (endIndex > i)
                        {
                            // 跳转到End&之后（循环自增后为endIndex+1）
                            i = endIndex;
                        }
                        continue;
                    }

                    if (foreachStack.Count == 0)
                    {
                        // 记录进入第一层循环前的 targets，以便循环结束后恢复
                        preForeachTargets = new List<cardBase_>(targets);
                    }

                    foreachStack.Push((savedTargets, 0, i + 1));
                    targets = new List<cardBase_> { savedTargets[0] };

                    continue;
                }

                // End& - 结束当前循环或跳转到上一个仍有元素的循环
                if (ins == "end&")
                {
                    if (foreachStack.Count > 0)
                    {
                        bool jumped = false;

                        while (foreachStack.Count > 0 && !jumped)
                        {
                            var ctx = foreachStack.Peek();
                            ctx.currentIndex += 1;

                            if (ctx.currentIndex < ctx.savedTargets.Count)
                            {
                                // 还有元素，设置为下一个目标并回到该循环开始
                                targets = new List<cardBase_> { ctx.savedTargets[ctx.currentIndex] };
                                // 更新栈顶值
                                foreachStack.Pop();
                                foreachStack.Push(ctx);
                                i = ctx.loopStartIndex - 1; // for循环会自增
                                jumped = true;
                                break;
                            }

                            // 当前循环结束，尝试外层循环
                            foreachStack.Pop();
                        }

                        if (!jumped)
                        {
                            // 所有循环结束后恢复为进入第一层循环前的 targets（如果有）
                            targets = preForeachTargets ?? new List<cardBase_>();
                        }
                    }

                    continue;
                }

                // 替换所有 & 开头的变量为其实际值
                instruction = ReplaceVariables(part, result, targets, sourceCard);
                
                // 处理if条件跳转
                if (instruction.StartsWith("if(") && instruction.Contains(")"))
                {
                    // 格式: if(条件)标签
                    int closeParenIndex = instruction.IndexOf(")");
                    string condition = instruction.Substring(3, closeParenIndex - 3);
                    string label = instruction.Substring(closeParenIndex + 1).Trim();
                    if (label.EndsWith("&")) label = label.Substring(0, label.Length - 1);
                    
                    // 评估条件
                    bool conditionResult = EvaluateCondition(condition, result, targets, sourceCard);
                    
                    // 如果条件满足且标签存在，跳转到标签位置
                    if (conditionResult && labels.ContainsKey(label))
                    {
                        i = labels[label];
                    }
                    continue;
                }

                // this - 指代执行效果的单位本身
                if (ins == "this")
                {
                    if (sourceCard != null)
                    {
                        targets = new List<cardBase_> { sourceCard };
                    }
                }
                // target - 指代传入的目标列表
                else if (ins == "target")
                {
                    if (targetCards != null && targetCards.Count > 0)
                    {
                        targets = new List<cardBase_>(targetCards);
                    }
                    else if (targetCard != null)
                    {
                        targets = new List<cardBase_> { targetCard };
                    }
                }
                // GetCardBeingAddToSupportLine - 获取上一个加入支援阵线的卡
                else if (ins == "getcardbeingaddtosupportline")
                {
                    var card = GetCardBeingAddToSupportLine();
                    if (card != null)
                    {
                        targets = new List<cardBase_> { card };
                    }
                }
                // GetCardBeingAddToHand - 获取上一张被加入手中的卡
                else if (ins == "getcardbeingaddtohand")
                {
                    if (lastCardAddedToHand != null)
                    {
                        targets = new List<cardBase_> { lastCardAddedToHand };
                    }
                }
                // GetLeftTarget - 获取当前目标的左侧相邻单位
                else if (ins == "getlefttarget")
                {
                    if (targets.Count > 0 && targets[0] != null)
                    {
                        var myPlace = targets[0].GetMyPlace();
                        if (myPlace != null)
                        {
                            var leftPlace = GetLeftPlace(myPlace);
                            if (leftPlace != null && leftPlace.GetMyCard() != null && leftPlace.GetMyCard().getState() == CardState.placed)
                            {
                                targets = new List<cardBase_> { leftPlace.GetMyCard() };
                            }
                        }
                    }
                }
                // GetRightTarget - 获取当前目标的右侧相邻单位
                else if (ins == "getrighttarget")
                {
                    if (targets.Count > 0 && targets[0] != null)
                    {
                        var myPlace = targets[0].GetMyPlace();
                        if (myPlace != null)
                        {
                            var rightPlace = GetRightPlace(myPlace);
                            if (rightPlace != null && rightPlace.GetMyCard() != null && rightPlace.GetMyCard().getState() == CardState.placed)
                            {
                                targets = new List<cardBase_> { rightPlace.GetMyCard() };
                            }
                        }
                    }
                }
                // Fight - 使当前targets与原始选择目标(命令卡点击目标)战斗
                else if (ins == "fight")
                {
                    await ExecuteChangeLists();
                    var attackers = targets.ToList();
                    var defenders = targetCards;
                    if (defenders != null && defenders.Count > 0)
                    {
                        foreach (var attacker in attackers)
                        {
                            if (attacker != null && attacker.getState() == CardState.placed)
                            {
                                // 驻守/压制单位不能被「强制参战」绕过——下面会把 attackAble
                                // 临时抬到 1，不挡一下「无法攻击」就形同虚设。
                                if (attacker.IsActionForbidden()) continue;

                                int savedMoveAble = attacker.moveAble;
                                int savedAttackAble = attacker.attackAble;
                                int savedAttackCount = attacker.attackCountThisTurn;
                                if (attacker.attackAble < 1) attacker.attackAble = 1;
                                var savedCardType = attacker.cardType;
                                attacker.cardType = CardTypes.Command;
                                await Attack(attacker, defenders[0]);
                                attacker.cardType = savedCardType;
                                attacker.moveAble = savedMoveAble;
                                attacker.attackAble = savedAttackAble;
                                attacker.attackCountThisTurn = savedAttackCount;
                                attacker.UpdateMoveableLight();
                            }
                        }
                    }
                }
                else if (instruction.StartsWith("GetTargetByIndex", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int index = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        if (index >= 0 && index < targets.Count)
                        {
                            targets = new List<cardBase_> { targets[index] };
                        }
                    }
                }

                if(ins == "myhq")
                {
                    targets = [myHq];
                }
                else if(ins == "enemyhq")
                {
                    targets = [enemyHq];
                }

                // Heal(n) - 使用AddChange增加防御力
                if (instruction.StartsWith("Heal", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int healAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.GetDefence, healAmount);
                            }
                        }
                    }
                }

                // convert(id) - 把目标**原地转换**成 id 所指代的单位
                if (instruction.StartsWith("convert", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        string newCardId = match.Groups[1].Value.Trim().Trim('"', '\'');
                        foreach (var target in targets)
                        {
                            if (target != null) await ConvertCardAsync(target, newCardId);
                        }
                    }
                }

                if (instruction.StartsWith("GetAttack", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int GetAttackAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.GetAttack, GetAttackAmount);
                            }
                        }
                    }
                }

                if (instruction.StartsWith("SetDefence", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int setDefenceAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.SetDefence, setDefenceAmount);
                            }
                        }
                    }
                }

                if (instruction.StartsWith("setCost", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int setCostAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.SetCost, setCostAmount);
                            }
                        }
                    }
                }

                if (instruction.StartsWith("addCost", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int addCostAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.AddCost, addCostAmount);
                            }
                        }
                    }
                }

                if (instruction.StartsWith("subCost", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int subCostAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.ReduceCost(subCostAmount);
                            }
                        }
                    }
                }

                // LoseAttack(n) - 减少目标攻击力
                if (instruction.StartsWith("LoseAttack", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int loseAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.LoseAttack(loseAmount);
                            }
                        }
                    }
                }

                // damage(n) - 使用AddChange减少防御力
                if (instruction.StartsWith("damage", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int damageAmount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.LoseDefence, damageAmount);
                            }
                        }
                    }
                    else if (instruction == "damage" && result > 0)
                    {
                        // damage 不带参数时 使用result值
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.LoseDefence, result);
                            }
                        }
                    }
                }

                // drawCard - 抽卡
                if (ins == "drawcard")
                {
                    if (sourceCard?.GetIsFriend() == IsFriend.friend)
                    {
                        await player1.DrawCard(result > 0 ? result : 1);
                    }
                    else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                    {
                        // 敌方抽卡逻辑（可选）
                        await player2.DrawCard(result > 0 ? result : 1);
                    }
                }

                // DrawACard(string,int) - 尝试从卡组中抽出i张名字含s字符串的单位
                if (instruction.StartsWith("DrawACard", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction,  @"\(([^,]*),([^)]*)\)");
                    if (match.Success)
                    {
                        string namePattern = match.Groups[1].Value;
                        int count = EvaluateExpression(match.Groups[2].Value, result, targets, sourceCard);

                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            await player1.DrawCardsWithName(namePattern, count);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            await player2.DrawCardsWithName(namePattern, count);
                        }
                    }
                }

                // DrawACardWithType(unitType,int) - 从卡组中抽出i张类型为u的卡
                if (instruction.StartsWith("DrawACardWithType", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction,  @"\(([^,]*),([^)]*)\)");
                    if (match.Success)
                    {
                        string unitType = match.Groups[1].Value;
                        int count = EvaluateExpression(match.Groups[2].Value, result, targets, sourceCard);

                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            await player1.DrawCardsWithType(unitType, count);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            await player2.DrawCardsWithType(unitType, count);
                        }
                    }
                }

                // DrawUnitCards(i) - 从卡组中抽取i张单位卡（非指挥卡）
                if (instruction.StartsWith("DrawUnitCards", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int count = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            await player1.DrawUnitCards(count);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            await player2.DrawUnitCards(count);
                        }
                    }
                }

                // DiscardPlayerRandomly(i) - 随机弃置**玩家**i张手牌
                //
                // 为什么不复用 DiscardRandomly：那条是按「效果来源卡的阵营」派发的。
                // 写在 enemyTurn.ini 里的战役行动，来源卡是敌方总部，于是它会去弃敌方
                // 自己的手牌——那是空的，对玩家毫无影响（t3/t6/t9/t12/t15 曾因此全部失效）。
                // 而写在玩家卡上的 DiscardRandomly 确实是「自己弃自己」（如「抽3张弃1张」），
                // 两种语义都得留着，所以给「敌方让玩家弃牌」单开一条，语义一目了然。
                if (instruction.StartsWith("DiscardPlayerRandomly", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int count = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        _ = player1.DiscardRandomly(count);
                    }
                }

                // DiscardRandomly(i) - 随机弃置i张卡（来源方自己的手牌）
                if (instruction.StartsWith("DiscardRandomly", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int count = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            player1.DiscardRandomly(count);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            player2.DiscardRandomly(count);
                        }
                    }
                }

                // GetCardsBeingTreated - 获得刚刚被抽到的卡的引用
                if (ins == "getcardsbeingtreated")
                {
                    if (sourceCard?.GetIsFriend() == IsFriend.friend)
                    {
                        targets = player1.GetLastDrawnCards();
                    }
                    else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                    {
                        targets = player2.GetLastDrawnCards();
                    }
                }

                // GetCardsShuffledIntoDeck - 获得刚才洗入卡组的卡
                if (ins == "getcardsshuffledintodeck")
                {
                    targets = new List<cardBase_>(lastCardsShuffledIntoDeck);
                }

                // AddToHand(string) 或 AddToHand(string,int) - 将名字为s的卡加入手牌，可以指定数量
                if (instruction.StartsWith("AddToHand", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((.*)\)");
                    if (match.Success)
                    {
                        string[] parameters = match.Groups[1].Value.Split(',');
                        string cardName = parameters[0].Trim();
                        int count = 1; // 默认添加1张
                        
                        // 如果有两个参数，第二个参数是数量，使用EvaluateExpression解析
                        if (parameters.Length > 1)
                        {
                            count = EvaluateExpression(parameters[1].Trim(), result, targets, sourceCard);
                            if (count < 1) count = 1; // 确保至少添加1张
                        }
                        
                        var cardData = GetCardMaganer().GetCard(cardName);
                        if (cardData != null)
                        {
                            List<cardBase_> addedCards = new List<cardBase_>();
                            PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");

                            // 根据数量添加多张卡
                            for (int j = 0; j < count; j++)
                            {
                                var card = cardRes.Instantiate() as cardBase_;
                                card.SetCardInformation(cardData);
                                card.SetIsFriend(sourceCard?.GetIsFriend() ?? IsFriend.friend);
                                addedCards.Add(card);

                                if (sourceCard?.GetIsFriend() == IsFriend.friend)
                                {
                                    await player1.AddCardToHand(card);
                                }
                                else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                                {
                                    await player2.AddCardToHand(card);
                                }
                            }

                            // 设置最后添加的卡牌列表
                            if (sourceCard?.GetIsFriend() == IsFriend.friend)
                            {
                                RecordCardsObtained(addedCards, IsFriend.friend);
                            }
                            else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                            {
                                RecordCardsObtained(addedCards, IsFriend.enemy);
                            }
                        }
                    }
                }

                // GetEffect(string) - 使targets获得指定的effect
                if (instruction.StartsWith("GetEffect", StringComparison.OrdinalIgnoreCase))
                {
                    // 从原始part提取（避免ReplaceVariables腐蚀内层&变量和&标签）
                    string raw = part.Trim();
                    int startIndex = raw.IndexOf('(');
                    int endIndex = raw.LastIndexOf(')');
                    if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                    {
                        string effectToGive = raw.Substring(startIndex + 1, endIndex - startIndex - 1);
                        // 移除外层的引号（如果有）
                        if (effectToGive.StartsWith('"') && effectToGive.EndsWith('"'))
                        {
                            effectToGive = effectToGive.Substring(1, effectToGive.Length - 2);
                        }
                        foreach (var target in targets)
                        {
                            // 为每个target添加effect
                            if (!string.IsNullOrEmpty(target.effect))
                            {
                                target.effect = target.effect + "," + effectToGive;
                            }
                            else
                            {
                                target.effect = effectToGive;
                            }
                            target.RefreshState();
                        }
                        }

                }

                // DiscardWithName(string,i) - 弃置i张名字含s的卡(若可能)
                if (instruction.StartsWith("DiscardWithName", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction,  @"\((.*),(\d+)\)");
                    if (match.Success)
                    {
                        string namePattern = match.Groups[1].Value;
                        int count = int.Parse(match.Groups[2].Value);

                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            player1.DiscardCardsWithName(namePattern, count);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            player2.DiscardCardsWithName(namePattern, count);
                        }
                    }
                }

                // setResult(n) - 设置结果值
                if (instruction.StartsWith("setResult", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int value = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        result = value;
                    }
                }

                // SetMemory(s,n) - 设置自定义变量值
                if (instruction.StartsWith("SetMemory", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^,]+),([^)]*)\)");
                    if (match.Success)
                    {
                        string memoryName = match.Groups[1].Value.Trim();
                        int value = EvaluateExpression(match.Groups[2].Value, result, targets, sourceCard);
                        SetMemory(memoryName, value);
                    }
                }

                // addDefence(n) - 增加目标防御力
                if (instruction.StartsWith("addDefence", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int amount = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.GetDefence, amount);
                            }
                        }
                    }
                }

                // Retreat(list) - 使单位撤退
                if (instruction.StartsWith("Retreat", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var target in targets)
                    {
                        if (target != null)
                        {
                            await RetreatUnit(target);
                        }
                    }
                }

                // DiscardWithTarget - 弃置当前targets（支持手牌）
                if (instruction.StartsWith("DiscardWithTarget", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var target in targets.ToList())
                    {
                        if (target == null) continue;
                        if (target.getState() == CardState.inHand)
                        {
                            target.ResetVisualsInstant();
                            if (target.GetIsFriend() == IsFriend.friend)
                                player1.RemoveFromHand(target);
                            else
                                player2.RemoveFromHand(target);
                            _ = CardDiscardAndRemove(target);
                        }
                        else
                        {
                            target.AddPendingDiscard();
                        }
                    }
                    continue;
                }

                // Discard(list) - 挂起弃置操作
                if (instruction.StartsWith("Discard", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var target in targets)
                    {
                        if (target != null)
                        {
                            target.AddPendingDiscard();
                        }
                    }
                }

                // getCount(${selector}) - 获取符合条件的单位数量
                if (instruction.StartsWith("getCount", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\$\{([^}]*)\}");
                    if (match.Success)
                    {
                        var selector = match.Groups[1].Value;
                        var selectedTargets = GetTargetsFromSelector(selector);
                        result = selectedTargets?.Count ?? 0;
                    }
                }

                // setTarget - 设置目标为传入的targetCard
                if (ins == "settarget")
                {
                    if (targetCard != null)
                    {
                        targets = [targetCard];
                    }
                }

                // addToSupportLine(cardId) - 添加卡到支援阵线
                if (instruction.StartsWith("addToSupportLine", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        string cardId = match.Groups[1].Value.Trim();
                        var newCardData = GetCardMaganer().GetCard(cardId);
                        if (newCardData != null)
                        {
                            lastCardAddedToSupportLine = null;
                            var place = GetTheFirstValidFriendlyPlace();
                            if (place != null)
                            {
                                var newCard = cardRes.Instantiate() as cardBase_;
                                newCard.SetCardInformation(newCardData);
                                newCard.SetIsFriend(IsFriend.friend);
                                await AddCardToPlace(newCard, place);
                                lastCardAddedToSupportLine = newCard;
                            }
                        }
                    }
                }

                // addToEnemySupportLine(cardId) - 添加卡到敌方支援阵线
                if (instruction.StartsWith("addToEnemySupportLine", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        string cardId = match.Groups[1].Value.Trim();
                        var newCardData = GetCardMaganer().GetCard(cardId);
                        if (newCardData != null)
                        {
                            var place = GetTheFirstValidEnemySupportPlace();
                            if (place != null)
                            {
                                var newCard = cardRes.Instantiate() as cardBase_;
                                newCard.SetCardInformation(newCardData);
                                newCard.SetIsFriend(IsFriend.enemy);
                                await AddCardToPlace(newCard, place);
                                lastCardAddedToSupportLine = newCard;
                            }
                        }
                    }
                }

                // addToDeck(cardId) - 将指定卡牌洗入卡组
                if (instruction.StartsWith("addToDeck", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        string cardId = match.Groups[1].Value.Trim();
                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            player1.AddCardToDeck(cardId);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            player2.AddCardToDeck(cardId);
                        }
                    }
                }

                // ShuffleIntoDeck - 将当前targets洗入卡组
                if (ins == "shuffleintodeck")
                {
                    var shuffled = new List<cardBase_>();
                    foreach (var target in targets.ToList())
                    {
                        if (target == null || target.isHq == HQ.hq) continue;
                        target.ClearMyPlace();
                        cardInPlaces.Remove(target);
                        target.DisableCombatAbility();
                        if (target.GetIsFriend() == IsFriend.friend)
                            player1.AddExistingCardToDeck(target);
                        else if (target.GetIsFriend() == IsFriend.enemy)
                            player2.AddExistingCardToDeck(target);
                        shuffled.Add(target);
                    }
                    if (shuffled.Any(c => c.GetIsFriend() == IsFriend.friend))
                        player1.ShuffleDeck();
                    if (shuffled.Any(c => c.GetIsFriend() == IsFriend.enemy))
                        player2.ShuffleDeck();
                    lastCardsShuffledIntoDeck = shuffled;
                    RefreshAllBeGuardianedStatus();
                }

                // addANewUnitToBattlefieldWithCostAndType(type, cost) - 从非Unobtainable卡中按类型和费用选卡加入战场
                if (instruction.StartsWith("addANewUnitToBattlefieldWithCostAndType", StringComparison.OrdinalIgnoreCase))
                {
                    // 第二个参数是**表达式**而不是字面量：`&result`（经 ReplaceVariables 后已经
                    // 是数字）、`&targetCost+3`、`4` 都要能用。原先的正则写死 `(\d+)`，实参只要
                    // 不是纯数字就整体不匹配、整条指令静默失效——「卡打出去了但什么都没发生」
                    // 很难查。所以这里改成抓出表达式交给 EvaluateExpression 求值。
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^,()]+),\s*([^()]+)\)");
                    if (!match.Success)
                    {
                        GD.Print($"[SpawnByCost] 参数解析失败，已跳过：{instruction}");
                    }
                    else
                    {
                        string typeStr = match.Groups[1].Value.Trim();
                        int targetCost = EvaluateExpression(match.Groups[2].Value.Trim(), result, targets, sourceCard);
                        var cardType = CardParser.GetTypes(typeStr);
                        var allCards = GetCardMaganer().GetAllCards()
                            .Where(c => c.Rarity != Rarity.Unobtainable && c.CardType == cardType && c.IsHq != HQ.hq)
                            .ToList();

                        if (allCards.Count == 0)
                        {
                            GD.Print($"[SpawnByCost] 卡池里没有可用的 {typeStr}，已跳过");
                        }
                        else
                        {
                            var sorted = allCards.OrderBy(c => Math.Abs(c.Cost - targetCost)).ToList();
                            int minDiff = Math.Abs(sorted[0].Cost - targetCost);
                            var candidates = sorted.Where(c => Math.Abs(c.Cost - targetCost) == minDiff).ToList();
                            var chosen = candidates[new Random().Next(candidates.Count)];
                            bool isFriend = sourceCard?.GetIsFriend() == IsFriend.friend;
                            var place = isFriend ? GetTheFirstValidFriendlyPlace() : GetTheFirstValidEnemySupportPlace();

                            if (place == null)
                            {
                                // 支援阵线满员是这条指令唯一会「什么都不做」的正当理由，
                                // 但它看不出来，必须留日志，否则和「效果写错了」分不开。
                                GD.Print($"[SpawnByCost] {(isFriend ? "友方" : "敌方")}支援阵线已满，"
                                       + $"{chosen.Name}({chosen.Id}, {chosen.Cost}费) 无法入场");
                            }
                            else
                            {
                                var newCard = cardRes.Instantiate() as cardBase_;
                                newCard.SetCardInformation(chosen);
                                newCard.SetIsFriend(isFriend ? IsFriend.friend : IsFriend.enemy);
                                await AddCardToPlace(newCard, place);
                                lastCardAddedToSupportLine = newCard;
                            }
                        }
                    }
                }

                // GetPoint() - 获得指挥点
                if (ins == "getpoint")
                {
                    if (sourceCard?.GetIsFriend() == IsFriend.friend)
                    {
                        result = player1.ReadPoint();
                    }
                    else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                    {
                        result = player2.ReadPoint();
                    }
                }

                // GetPointMax() - 获得指挥点槽
                if (ins == "getpointmax")
                {
                    if (sourceCard?.GetIsFriend() == IsFriend.friend)
                    {
                        result = player1.ReadPointMax();
                    }
                    else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                    {
                        result = player2.ReadPointMax();
                    }
                }

                // GetHandMax() - 获得手牌上限
                // 「抽牌直到手牌已满」这类效果需要它；上限是 Player 的常量，
                // 脚本读不到，若写死数字就违反「禁止魔鬼数字」
                // 同时接受 GetHandMax 与 GetHandMax() 两种写法。
                // 项目约定无参指令裸写（Retreat、HealAllTargets 都是），但 LOGIC.md 的
                // 指令表是按带括号列的，作者很容易照着文档写成带括号的版本；而 ins 不做
                // 去括号处理，带括号时会静默不匹配。
                if (ins == "gethandmax" || ins.StartsWith("gethandmax("))
                {
                    if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        result = player2.ReadHandMax();
                    else
                        result = player1.ReadHandMax();
                }

                // AddPointMax() - 增加指挥点槽
                if (instruction.StartsWith("AddPointMax", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int value = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            player1.AddPointMax(value);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            player2.AddPointMax(value);
                        }
                    }
                }

                // AddPoint() - 增加指挥点
                if (instruction.StartsWith("AddPoint", StringComparison.OrdinalIgnoreCase)
                    && !instruction.StartsWith("AddPointMax", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int value = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        if (sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            player1.AddPoint(value);
                        }
                        else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            player2.AddPoint(value);
                        }
                    }
                }

                // losePointAtNextTurnBegin(n) - 下回合开始时失去n点指挥点
                if (instruction.StartsWith("losePointAtNextTurnBegin", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        int value = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        SetMemory("pendingPointLoss", value);
                    }
                }

                // GetRandomFriendUnit() - 随机获得一个友方单位
                if (ins == "getrandomfriendunit")
                {
                    var friendUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend && x.isHq != HQ.hq).ToList();
                    if (friendUnits.Count > 0)
                    {
                        var rnd = new Random();
                        targets = new List<cardBase_> { friendUnits[rnd.Next(friendUnits.Count)] };
                    }
                    else
                    {
                        targets = [];
                    }
                }

                // GetHighestAttackFriendUnit(type1,type2,...) - 从友方指定类型单位中取攻击最高者随机选一
                if (instruction.StartsWith("GetHighestAttackFriendUnit", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        var typeNames = match.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList();
                        var candidates = ReadCardInPlaces()
                            .Where(x => x.getState() == CardState.placed
                                     && x.GetIsFriend() == IsFriend.friend
                                     && x.isHq != HQ.hq
                                     && typeNames.Any(tn => CardParser.GetTypes(tn) == x.cardType))
                            .ToList();
                        if (candidates.Count > 0)
                        {
                            int maxAttack = candidates.Max(c => c.ReadAttack());
                            var highest = candidates.Where(c => c.ReadAttack() == maxAttack).ToList();
                            var rnd = new Random();
                            targets = new List<cardBase_> { highest[rnd.Next(highest.Count)] };
                        }
                        else
                        {
                            targets = [];
                        }
                    }
                }

                // setTargets(selector) - 使用选择器设置目标列表
                if (instruction.StartsWith("setTargets", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\$\{([^}]*)\}");
                    if (match.Success)
                    {
                        string selector = match.Groups[1].Value;
                        targets = GetTargetsFromSelector(selector);
                    }
                }

                // GetRandomEnemyUnit() - 随机获得一个敌方单位
                if (ins == "getrandomenemyunit")
                {
                    var enemyUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy && x.isHq != HQ.hq).ToList();
                    if (enemyUnits.Count > 0)
                    {
                        var rnd = new Random();
                        targets = new List<cardBase_> { enemyUnits[rnd.Next(enemyUnits.Count)] };
                    }
                    else
                    {
                        targets = [];
                    }
                }

                // GetRandomFriendTarget() - 随机获得一个友方目标(目标=单位+总部)
                if (ins == "getrandomfriendtarget")
                {
                    var friendTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend).ToList();
                    if (myHq != null && myHq.getState() == CardState.placed)
                    {
                        friendTargets.Add(myHq);
                    }
                    if (friendTargets.Count > 0)
                    {
                        var rnd = new Random();
                        targets = new List<cardBase_> { friendTargets[rnd.Next(friendTargets.Count)] };
                    }
                    else
                    {
                        targets = [];
                    }
                }

                // GetRandomEnemyTarget() - 随机获得一个敌方目标(目标=单位+总部)
                if (ins == "getrandomenemytarget")
                {
                    var enemyTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
                    if (enemyHq != null && enemyHq.getState() == CardState.placed && !enemyTargets.Contains(enemyHq))
                    {
                        enemyTargets.Add(enemyHq);
                    }
                    if (enemyTargets.Count > 0)
                    {
                        var rnd = new Random();
                        targets = new List<cardBase_> { enemyTargets[rnd.Next(enemyTargets.Count)] };
                    }
                    else
                    {
                        targets = [];
                    }
                }

                // GetRandomNumber() - 随机获得一个数字(包括两个参数,min,max)
                if (instruction.StartsWith("GetRandomNumber", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^,]*),([^)]*)\)");
                    if (match.Success)
                    {
                        int min = EvaluateExpression(match.Groups[1].Value, result, targets, sourceCard);
                        int max = EvaluateExpression(match.Groups[2].Value, result, targets, sourceCard);
                        var rnd = new Random();
                        result = rnd.Next(min, max + 1);
                    }
                }

                // KillAllTargets() / KillAllTarget() - 消灭列表上的所有单位
                if (ins == "killalltargets" || ins == "killalltarget")
                {
                    foreach (var target in targets)
                    {
                        if (target != null && target.getState() == CardState.placed)
                        {
                            target.LoseDefence(target.ReadDefence());
                        }
                    }
                }

                // FightRandomEnemy() - 使targets各自随机攻击一个敌方非总部单位，不计入攻击次数
                if (instruction.StartsWith("FightRandomEnemy", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteChangeLists();
                    var targetsSnapshot = targets.ToList();
                    foreach (var t in targetsSnapshot)
                    {
                        if (t == null || t.getState() != CardState.placed) continue;
                        // 同上：本指令同样会临时抬高 attackAble，驻守/压制单位必须排除
                        if (t.IsActionForbidden()) continue;
                        var allTargets = GetAllowedTargets(t);
                        var validTargets = allTargets.Where(v => v.isHq != HQ.hq).ToList();
                        if (validTargets.Count > 0)
                        {
                            int savedMoveAble = t.moveAble;
                            int savedAttackAble = t.attackAble;
                            int savedAttackCount = t.attackCountThisTurn;
                            if (t.attackAble < 1) t.attackAble = 1;
                            var rnd = new Random();
                            await Attack(t, validTargets[rnd.Next(validTargets.Count)]);
                            t.moveAble = savedMoveAble;
                            t.attackAble = savedAttackAble;
                            t.attackCountThisTurn = savedAttackCount;
                            t.UpdateMoveableLight();
                        }
                    }
                }

                // Play 或 Play(u) - 打出一张卡，不消耗指挥点
                if (instruction.StartsWith("Play", StringComparison.OrdinalIgnoreCase))
                {
                    // 解析参数，如果有的话
                    string targetType = "";
                    if (instruction.Contains("(") && instruction.Contains(")"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                        if (match.Success)
                        {
                            targetType = match.Groups[1].Value.Trim();
                        }
                    }

                    // 获取要打出的卡牌列表：优先使用&selector指定的，否则使用targets
                    List<cardBase_> cardsToPlay = new List<cardBase_>();
                    
                    if (instruction.Contains("&"))
                    {
                        // 解析&selector 或 &targets
                        var selectorMatch = System.Text.RegularExpressions.Regex.Match(instruction, @"&([A-Za-z0-9_\.]+)");
                        if (selectorMatch.Success)
                        {
                            string selector = selectorMatch.Groups[1].Value.Trim();
                            if (selector.Equals("targets", StringComparison.OrdinalIgnoreCase))
                            {
                                cardsToPlay = new List<cardBase_>(targets);
                            }
                            else if (selector.StartsWith("$"))
                            {
                                cardsToPlay = GetTargetsFromSelector(selector.Substring(1));
                            }
                            else
                            {
                                cardsToPlay = GetTargetsFromSelector(selector);
                            }
                        }
                        else
                        {
                            cardsToPlay = new List<cardBase_>(targets);
                        }
                    }
                    else
                    {
                        // 使用targets
                        cardsToPlay = new List<cardBase_>(targets);
                    }

                    // 从手牌中找到这些卡并打出
                    var handCards = player1.GetCardsInHand();
                    foreach (var cardToPlay in cardsToPlay)
                    {
                        var handCard = handCards.Find(c => c.id == cardToPlay.id && c.name == cardToPlay.name);
                        if (handCard != null)
                        {
                            // 从手牌移除
                            player1.RemoveFromHand(handCard);
                            
                            // 不消耗指挥点，直接打出
                            await PlayCardWithoutCost(handCard, targetType);
                        }
                    }
                }

                // Choose(cardA,cardB) - 显示两张卡让玩家选择
                if (instruction.StartsWith("Choose", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^,]+),([^)]+)\)");
                    if (match.Success)
                    {
                        string cardAName = match.Groups[1].Value.Trim();
                        string cardBName = match.Groups[2].Value.Trim();

                        var cardA = GetCardMaganer().GetCard(cardAName);
                        var cardB = GetCardMaganer().GetCard(cardBName);

                        if (cardA != null && cardB != null)
                        {
                            // 创建卡牌实例
                            var cardAInstance = ResourceManager.Instance.AcquireEmptyCard();
                            var cardBInstance = ResourceManager.Instance.AcquireEmptyCard();
                            
                            cardAInstance.SetCardInformation(cardA);
                            cardBInstance.SetCardInformation(cardB);
                            
                            cardAInstance.SetIsFriend(sourceCard?.GetIsFriend() ?? IsFriend.friend);
                            cardBInstance.SetIsFriend(sourceCard?.GetIsFriend() ?? IsFriend.friend);

                            // 显示选择界面
                            var selectedCard = await ShowCardChoice(new List<cardBase_> { cardAInstance, cardBInstance }, true);
                            
                            // 执行选中卡牌的效果
                            if (selectedCard != null)
                            {
                                await ParseAndExecuteEffect(selectedCard.effect, selectedCard, null);
                            }

                            // 释放卡牌回到池中
                            ResourceManager.Instance.ReleaseEmptyCard(cardAInstance);
                            ResourceManager.Instance.ReleaseEmptyCard(cardBInstance);
                        }
                    }
                }

                // Develop 或 Develop($selector) 或 Develop(name1,name2,...) - 开发效果
                if (instruction.StartsWith("Develop", StringComparison.OrdinalIgnoreCase))
                {
                    List<cardBase_> cardsToShow = new List<cardBase_>();

                    if (instruction.Contains("(") && instruction.Contains(")"))
                    {
                        var paramMatch = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                        if (paramMatch.Success)
                        {
                            string param = paramMatch.Groups[1].Value.Trim();
                            
                            if (param.StartsWith("$"))
                            {
                                // 使用选择器，从池中随机选择3张
                                var selector = param.Substring(1);
                                var candidateCards = GetTargetsFromSelector(selector);
                                
                                var randomCards = new List<cardBase_>();
                                var rnd = new Random();
                                var shuffled = candidateCards.OrderBy(x => rnd.Next()).ToList();
                                for (int idx = 0; idx < Math.Min(3, shuffled.Count); idx++)
                                {
                                    randomCards.Add(shuffled[idx]);
                                }
                                cardsToShow = randomCards;
                            }
                            else
                            {
                                // 使用名字列表，直接实例化这些卡，如果多于3张则随机选择3张
                                var names = param.Split(',');
                                var candidateCards = new List<cardBase_>();
                                foreach (var name in names)
                                {
                                    var cardData = GetCardMaganer().GetCard(name.Trim());
                                    if (cardData != null)
                                    {
                                        var cardInstance = ResourceManager.Instance.AcquireEmptyCard();
                                        cardInstance.SetCardInformation(cardData);
                                        cardInstance.SetIsFriend(sourceCard?.GetIsFriend() ?? IsFriend.friend);
                                        candidateCards.Add(cardInstance);
                                    }
                                }
                                
                                // 如果多于3张，随机选择3张
                                if (candidateCards.Count > 3)
                                {
                                    var rnd = new Random();
                                    candidateCards = candidateCards.OrderBy(x => rnd.Next()).Take(3).ToList();
                                }
                                
                                cardsToShow = candidateCards;
                            }
                        }
                    }
                    else
                    {
                        // 默认使用targets
                        var candidateCards = targets.Select(t =>
                        {
                            var cardData = GetCardMaganer().GetCard(t.id);
                            if (cardData != null)
                            {
                                PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
                                var newCard = cardRes.Instantiate() as cardBase_;
                                newCard.SetCardInformation(cardData);
                                newCard.SetIsFriend(t.GetIsFriend());
                                return newCard;
                            }
                            return null;
                        }).Where(c => c != null).ToList();
                        
                        // 如果多于3张，随机选择3张
                        if (candidateCards.Count > 3)
                        {
                            var rnd = new Random();
                            candidateCards = candidateCards.OrderBy(x => rnd.Next()).Take(3).ToList();
                        }
                        
                        cardsToShow = candidateCards;
                    }

                    // 候选卡来自牌堆/手牌/场上的真实对象：既不能改动它们的状态，
                    // 也不能把原对象交给选择界面与手牌（否则同一个对象会同时存在于两处）。
                    // 故一律先复制成独立对象，用复制品去显示与入手牌——原卡原封不动。
                    cardsToShow = cardsToShow.Select(c => Copy(c)).Where(c => c != null).ToList();

                    if (cardsToShow.Count > 0)
                    {
                        // 显示选择界面
                        var selectedCard = await ShowCardChoice(cardsToShow, false);
                        
                        // 将选中卡加入手牌
                        if (selectedCard != null && sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            await player1.AddCardToHand(selectedCard);
                            // Develop 也是「让卡入手」的效果路径，必须和「加入手牌」指令一样
                            // 记下刚入手的卡。否则紧跟其后的 GetCardsBeingTreated 会指向
                            // 上一次抽到的卡，`[紧急投产]` 的 setCost(0) 就减不到开发出的卡上。
                            RecordCardsObtained(new List<cardBase_> { selectedCard }, IsFriend.friend);
                        }
                        else if (selectedCard != null && sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            await player2.AddCardToHand(selectedCard);
                            RecordCardsObtained(new List<cardBase_> { selectedCard }, IsFriend.enemy);
                        }

                        // 释放未选择的卡牌回到池中
                        //foreach (var card in cardsToShow)
                        //{
                        //    if (card != selectedCard)
                        //    {
                        //        RemoveCard(card);
                        //    }
                        //}
                    }
                }

                // HealAllTargets() - 完全修复单位，将列表上所有单位的防御力设置为历史最大值
                if (ins == "healalltargets")
                {
                    foreach (var target in targets)
                    {
                        if (target != null && target.getState() == CardState.placed)
                        {
                            target.GetDefence(target.ReadMaxHistoryDefence() - target.ReadDefence());
                        }
                    }
                }

                // GetAllEnemyUnits() - 获得所有敌方单位
                if (ins == "getallenemyunits")
                {
                    var enemyUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy && x.isHq != HQ.hq).ToList();
                    targets = enemyUnits;
                }

                // GetAllFriendUnits() - 获得所有友方单位
                if (ins == "getallfriendunits")
                {
                    var friendUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend && x.isHq != HQ.hq).ToList();
                    targets = friendUnits;
                }

                // GetAllEnemyTargets() - 获得所有敌方目标(目标=单位+总部)
                if (ins == "getallenemytargets")
                {
                    var enemyTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
                    if (enemyHq != null && enemyHq.getState() == CardState.placed && !enemyTargets.Contains(enemyHq))
                    {
                        enemyTargets.Add(enemyHq);
                    }
                    targets = enemyTargets;
                }

                // GetAllFriendTargets() - 获得所有友方目标(目标=单位+总部)
                if (ins == "getallfriendtargets")
                {
                    var friendTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend).ToList();
                    if (myHq != null && myHq.getState() == CardState.placed && !friendTargets.Contains(myHq))
                    {
                        friendTargets.Add(myHq);
                    }
                    targets = friendTargets;
                }

                // displayAllCardState - 输出场上所有单位的状态信息
                if (ins == "displayallcardstate")
                {
                    var units = ReadCardInPlaces().Where(x => x.getState() == CardState.placed).ToList();
                    ConsolePrint("=== 场上单位状态 ===");
                    foreach (var u in units)
                    {
                        ConsolePrint($"  [{u.name}] atk={u.attack} def={u.defence} traits={u.traits} effect=\"{u.effect}\"");
                    }
                    ConsolePrint($"共 {units.Count} 个单位");
                }

                // GetEnemyHq() - 获得敌方总部
                if (ins == "getenemyhq")
                {
                    if (enemyHq != null && enemyHq.getState() == CardState.placed)
                    {
                        targets = new List<cardBase_> { enemyHq };
                    }
                }

                // GetFriendHq() - 获得友方总部
                if (ins == "getfriendhq")
                {
                    if (myHq != null && myHq.getState() == CardState.placed)
                    {
                        targets = new List<cardBase_> { myHq };
                    }
                }

                // Refresh - 刷新目标单位，使其回到可以移动和攻击的状态
                if (ins == "refresh")
                {
                    foreach (var target in targets)
                    {
                        if (target != null)
                        {
                            target.RefreshUnit();
                        }
                    }
                }

                // AddTrait(trait) - 为目标单位添加指定的特性
                if (instruction.StartsWith("AddTrait", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        string traitName = match.Groups[1].Value.Trim();
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddTrait(GetUnitTraits(traitName));
                            }
                        }
                    }
                }

                // RemoveTrait(trait) - 从目标单位移除指定的特性
                if (instruction.StartsWith("RemoveTrait", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        string traitName = match.Groups[1].Value.Trim();
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.RemoveTrait(GetUnitTraits(traitName));
                            }
                        }
                    }
                }
            }
        }

        // 执行所有挂起的弃置操作
        await ExecuteChangeLists();

    }


    /// <summary>
    /// 使单位撤退
    /// 如果单位在前线，尝试将其移动到对应阵营的支援阵线
    /// 如果单位在支援阵线，直接返回手牌（友方）或弃置（敌方）
    /// </summary>
    /// <param name="unit">要撤退的单位</param>
    private async Task RetreatUnit(cardBase_ unit)
    {

        //如果不是已经部署的单位 那么无法被撤退
        if (unit == null || unit.getState() != CardState.placed)
            return;

        // 总部不能撤退。
        //
        // 这是本函数唯一能把一张**场上卡**变成**手牌卡**的出口，而总部一旦进了手牌就
        // 彻底失控：它会被手牌布局引擎当成普通手牌摆放，会被 DiscardRandomly 之类
        // 「随机弃一张手牌」的效果抽中，然后 RemoveCard(myHq) 直接把这一局判负。
        // 控制台是 `ParseAndExecuteEffect(cmd, myHq, null, myHq)`——targets 就是友方总部，
        // 所以在那儿敲一句 retreat 就能把总部送进手牌。
        // 卡牌那条路（aUnit / aFriendlyUnit / aFrontLineUnit / ${allTargets.unit}）
        // 本来就在 IsValidTarget 里排除了总部，这里补上总闸。
        if (unit.isHq == HQ.hq)
        {
            GD.Print($"[Retreat] 总部不能被撤退，已忽略: {unit.id}");
            return;
        }

        var unitPlace = unit.GetMyPlace();
        if (unitPlace == null)
            return;

        bool isInFrontLine = frontLine.Contains(unitPlace);
        bool isFriendly = unit.GetIsFriend() == IsFriend.friend;

        // 如果在前线，尝试移动到支援阵线
        if (isInFrontLine)
        {
            {
                // 移动到支援阵线
                unitPlace.UnbondCard();
                var validPos = (isFriendly)?GetTheFirstValidFriendlyPlace():GetTheFirstValidEnemySupportPlace();
                if(validPos != null)
                {
                    unit.SetMyPlace(validPos);
                    unit.MoveToPosition(validPos.GetGlobalPosition());
                    unit.setState(CardState.placed);
                    return;
                } 
                
            }
        }

        // 如果在前线但无法移动到支援阵线，或者在支援阵线
        // 对于友方单位，尝试返回手牌
        if (isFriendly)
        {
            // 尝试返回手牌
            if (player1.GetCardsInHand().Count < 9)
            {
                unitPlace.UnbondCard();
                unit.ClearMyPlace();
                // 禁用单位的战斗能力
                unit.DisableCombatAbility();
                await player1.AddCardToHand(unit);
                return;
            }
        }

        // 无法返回手牌或敌方单位，直接弃置。
        //
        // 这里必须**立刻**关掉战斗能力，理由和友方回手牌那条分支一样：这张卡要到
        // 死亡检查时才真正 RemoveCard，在那之前它仍绑在 place 上、state 也还是 placed，
        // 光押一个待弃置标记拦不住敌方 AI 把它选去攻击（表现为「已经在播撤退/弃置动画了
        // 还照样打你一下」）。友方分支上面已经调过，敌方这条对齐。
        unit.DisableCombatAbility();
        unit.AddChange(ChangeType.DiscardCard,1);

        await Task.Delay(RetreatSettleDelayMs);

        return;
    }

    /// <summary>
    /// 记录「刚刚通过效果入手的卡」，供后续的 GetCardsBeingTreated / GetCardBeingAddToHand 取用。
    ///
    /// 这两条指令读的是**两套互相独立的指针**：
    ///   - GetCardsBeingTreated     → Player.lastDrawnCards      （走 Player.GetLastDrawnCards()）
    ///   - GetCardBeingAddToHand    → battlefield_.lastCardAddedToHand
    /// 必须两个都写，否则其中一条会拿到**上一次入手的卡**（或空列表）。
    ///
    /// 凡是让卡进入手牌的**效果路径**都要调一次本函数。漏调不会报任何错，
    /// 只表现为「后续指令作用到了别的卡上」——「紧急投产」的 setCost(0) 就是这么
    /// 减不到开发出来的那张卡上的。
    ///
    /// 注意边界：DrawCard / DrawACard 一系只写 Player.lastDrawnCards（抽牌语义），
    /// 不属于本函数的调用范围。
    /// </summary>
    private void RecordCardsObtained(List<cardBase_> cards, IsFriend side)
    {
        if (cards == null || cards.Count == 0) return;

        lastCardAddedToHand = cards[cards.Count - 1];

        if (side == IsFriend.friend) player1.SetLastDrawnCards(cards);
        else if (side == IsFriend.enemy) player2.SetLastDrawnCards(cards);
    }

    /// <summary>
    /// 复制一张卡：得到一个与 source **没有任何引用关系**的新对象，数值保持 source 当前状态。
    ///
    /// 用途：Develop 从牌堆/手牌/场上取出的候选是真实对象，既不能改动它们的状态，
    /// 也不能把原对象交给选择界面与手牌（否则同一对象会同时存在于两处），故先复制。
    ///
    /// 只做「建对象 + 搬数值」，**不调用任何会跑动画的 setter**：
    /// SetCostValue 内部有 FlashAttributeWithColor + AnimateCostRoll、SetDefence 内部有
    /// FlashAttributeWithColor，而那些都依赖节点已在场景树中。数值直接写字段。
    /// </summary>
    private cardBase_ Copy(cardBase_ source)
    {
        // 三处提前返回都会让候选卡**静默变少**——调用方用 .Where(c => c != null)
        // 过滤，候选全空时连选择界面都不弹，画面上只会表现为「开发没反应」。
        // 故每一处都留日志（只在真出问题时打印，正常路径无输出）。
        if (source == null) { GD.Print("[Copy] 中止：source 为 null"); return null; }

        var data = GetCardMaganer().GetCard(source.id);
        if (data == null) { GD.Print($"[Copy] 中止：GetCard(\"{source.id}\") 取不到 CardData"); return null; }

        var copy = ResourceManager.Instance.AcquireEmptyCard();
        if (copy == null) { GD.Print("[Copy] 中止：AcquireEmptyCard 返回 null"); return null; }

        // 与项目里既有的建卡流程（InitializeDeckFromIni）保持一致：cardbase.tscn 的根
        // Control 是锚点布局，尺寸由父节点 rect 算出。ShowCardChoice 会把这批候选卡
        // Reparent 到 choiceLayer（一个 CanvasLayer）下，尺寸若不钉死就会跟着换父节点
        // 重算——而 HandleChoiceCardClick 判定「点到哪张卡」用的正是 GetGlobalRect()。
        copy.PinDesignSize();

        copy.SetCardInformation(data);            // 卡图、文本、初始数值
        copy.SetIsFriend(source.GetIsFriend());

        // 搬运当前数值——直接写字段，绕开带动画的 setter
        copy.cost    = source.cost;
        copy.attack  = source.attack;
        copy.defence = source.defence;

        // effect 也必须搬：GetEffect(...) 是在运行时往目标卡的 effect 上追加字符串的
        // （battlefield_.cs 里 `target.effect = target.effect + "," + effectToGive`），
        // 所以被「炮火准备」这类卡加过效果的单位，其 effect 已与 CardData 里的不同。
        // 只靠 SetCardInformation 会拿回按 id 读到的初始效果串，把追加的部分丢掉。
        copy.effect  = source.effect;

        // traits 同样会在运行时被 AddTrait / RemoveTrait 改。
        // 这里用 AddTrait 而不是直接写 copy.traits——它除了 |= traits，还会初始化
        // 配套的运行时状态标志（hasSmokeScreen / hasShock / hasMobilize /
        // hasAmbushActive，Suppressed 还会关掉行动），只写字段会让这些标志缺失，
        // 例如复制出的伏击单位 hasAmbushActive 为 false，伏击直接失效。
        copy.AddTrait(source.traits);

        // 上面是直接写字段，绕开了 SetCostValue / SetDefence 的刷新副作用，
        // 三个数值 Label 还停在 SetCardInformation 写进去的初始值上。
        // RefreshState 是项目自己的公开刷新入口，照着字段重写一遍 Label。
        copy.RefreshState();

        return copy;
    }

    /// <summary>
    /// 执行指令卡效果。效果结算在打出瞬间完成，弃牌动画独立播放不阻塞玩家操作。
    /// </summary>
    private async void ExecuteCommandAndDiscard(cardBase_ commandCard, List<cardBase_> targets, bool needRestoreColor = false)
    {
        PlayCardEffect(commandCard);
        commandCard.ResetVisualsInstant();
        player1.RemoveFromHand(commandCard);

        if (needRestoreColor)
        {
            cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
            cardBase.Visible = false;
            RestoreAllTargetsColor();
        }

        // 弃牌动画独立播放，不阻塞玩家操作
        _ = CardDiscardAndRemove(commandCard);

        // 对每个目标触发被指向时点和同仇特性（不阻塞动画）
        if (targets != null)
        {
            foreach (var target in targets)
            {
                if (target != null)
                {
                    _ = TriggerUnitEffects("BePicked", target, new List<cardBase_> { commandCard }, checkOnlySourceCard: true);
                    _ = TriggerSharedHatred(target);
                }
            }
        }

        // 效果结算
        await ParseAndExecuteEffect(commandCard.effect, commandCard, targets);
        await CheckIfAnyUnitDiedAsync();

        // 友方指令打出时点（如「参谋总部」：友方打出指令时抽1张卡）。
        // 必须放在自身效果结算之后——结算完毕才算真正「打出过」这张指令。
        // 两处打出入口（_Input 的无目标/有目标分支）都经由本函数，故只需挂这里一处。
        await TriggerUnitEffects("FriendlyCommandPlayed", commandCard);

        // 上面那次死亡检查只覆盖了**指令自身**的效果。时点里的效果一样能打死人
        // （「女狙击手」的 FriendlyCommandPlayed:GetRandomEnemyTarget|damage(2) 就在这条
        // 路径上），而 damage(n) 是缓存型变更，跑完时点后目标防御已经是 0 了。
        // 这里不再查一次，单位就会顶着 0 防杵在场上不死。
        await CheckIfAnyUnitDiedAsync();
    }

    /// <summary>
    /// 打出一张卡但不消耗指挥点
    /// </summary>
    private async Task PlayCardWithoutCost(cardBase_ card, string targetType = "")
    {
        if (card == null) return;

        // 如果是单位卡，尝试部署
        if (card.cardType != CardTypes.Command)
        {
            var place = GetTheFirstValidFriendlyPlace();
            if (place != null)
            {
                await AddCardToPlace(card, place);
            }
        }
        else
        {
            // 如果是指令卡，直接执行效果
            PlayCardEffect(card);
            await ParseAndExecuteEffect(card.effect, card, null);
        }

        // 如果需要指定目标且有目标类型，随机选择
        if (!string.IsNullOrEmpty(targetType) && card.targetType != TargetType.NOTarget)
        {
            List<cardBase_> possibleTargets = new List<cardBase_>();
            
            switch (targetType.ToLower())
            {
                case "enemy":
                    possibleTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
                    break;
                case "friend":
                    possibleTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend).ToList();
                    break;
                case "unit":
                    possibleTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.isHq != HQ.hq).ToList();
                    break;
                case "hq":
                    if (enemyHq != null && enemyHq.getState() == CardState.placed)
                        possibleTargets.Add(enemyHq);
                    break;
                default:
                    possibleTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed).ToList();
                    break;
            }

            if (possibleTargets.Count > 0)
            {
                var rnd = new Random();
                var randomTarget = possibleTargets[rnd.Next(possibleTargets.Count)];
                await ParseAndExecuteEffect(card.effect, card, new List<cardBase_> { randomTarget });
            }
            else
            {
                await ParseAndExecuteEffect(card.effect, card, null);
            }
        }
    }


    /// <summary>
    /// 根据字符串解析单位特性
    /// </summary>
    /// <param name="input">由多个trait组成的字符串，用逗号分隔，如 "Blitz,HeavyArmor"</param>
    /// <returns>解析出的单位特性</returns>
    UnitTraits GetUnitTraits(string input)
    {
        UnitTraits result = UnitTraits.None;

        if (string.IsNullOrEmpty(input))
        {
            return result;
        }

        // 分割输入字符串，处理可能的逗号分隔
        string[] traitNames = input.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string traitName in traitNames)
        {
            string trimmedName = traitName.Trim();

            // 尝试将字符串转换为 UnitTraits 枚举值
            if (Enum.TryParse<UnitTraits>(trimmedName, true, out UnitTraits trait))
            {
                result |= trait; // 使用位或运算组合多个特性
            }
        }

        return result;
    }

    /// <summary>
    /// 根据选择器获取目标列表
    /// 格式: allTargets.unit.friend 或 allTargets.enemy 等
    /// </summary>
    private List<cardBase_> GetTargetsFromSelector(string selector)
    {
        List<cardBase_> results = new List<cardBase_>();
        var parts = selector.Split(".");
        int startIdx = 0;

        if (parts.Length > 0 && parts[0] == "allCardInHand")
        {
            results.AddRange(player1.GetCardsInHand());
            results.AddRange(player2.GetCardsInHand());
            startIdx = 1;
        }
        else if (parts.Length > 0 && parts[0] == "deck")
        {
            // 当前牌堆。与 allCardInHand 同属「非场上卡」根，故同样不走下面的场上填充
            results.AddRange(player1.GetCardsInDeck());
            startIdx = 1;
        }
        else
        {
            results.AddRange(cardInPlaces.Where(x => x.getState() == CardState.placed).ToList());
            if (myHq != null && myHq.getState() == CardState.placed && !results.Contains(myHq))
                results.Add(myHq);
            if (enemyHq != null && enemyHq.getState() == CardState.placed && !results.Contains(enemyHq))
                results.Add(enemyHq);
        }

        for (int i = startIdx; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part == "allTargets" || part == "allCardInHand")
            {
                // 已经是所有单位
                continue;
            }
            else if (part == "unit")
            {
                // 只保留非HQ单位
                results = results.Where(x => x.isHq == HQ.normalCard).ToList();
            }
            else if (part == "command")
            {
                results = results.Where(x => x.cardType == CardTypes.Command).ToList();
            }
            else if (part == "friend")
            {
                // 只保留友方单位
                results = results.Where(x => x.GetIsFriend() == IsFriend.friend).ToList();
            }
            else if (part == "enemy")
            {
                // 只保留敌方单位
                results = results.Where(x => x.GetIsFriend() == IsFriend.enemy).ToList();
            }
            else if (part == "hq")
            {
                results = results.Where(x => x.isHq == HQ.hq).ToList();
            }
            else if (part == "land")
            {
                results = results.Where(x => x.cardType == CardTypes.Infantry || x.cardType == CardTypes.Tank || x.cardType == CardTypes.Artillery).ToList();
            }
            else if (part == "air")
            {
                results = results.Where(x => x.cardType == CardTypes.Plane || x.cardType == CardTypes.Bomber).ToList();
            }
            else if (part == "frontLine" || part == "supportLine" || part == "enemySupportLine")
            {
                // 三条阵线：前线(Place6-10)、友方支援阵线(Place11-15)、敌方支援阵线(Place1-5)。
                // 注意代码里的字段名拼错了(enemySupprotLine)，但选择器片段按正确拼写对外。
                var lane = part == "frontLine" ? frontLine
                         : part == "supportLine" ? supportLine
                         : enemySupprotLine;
                results = results.Where(x => x.GetMyPlace() != null && lane.Contains(x.GetMyPlace())).ToList();
            }
            else if (part == "damaged")
            {
                results = results.Where(x => x.ReadDefence() < x.ReadMaxHistoryDefence()).ToList();
            }
            else if (Enum.TryParse<UnitTraits>(part, true, out UnitTraits traitFilter)
                     && traitFilter != UnitTraits.None)
            {
                // 特性筛选，如 ${allTargets.unit.Ambush} 取场上所有伏击单位。
                // 必须先试特性、认得出才当特性用；认不出才交给下面的卡牌类型解析，
                // 否则会把 Attack 之类解析失败的名字误当成「筛选掉全部」
                results = results.Where(x => x.HasTrait(traitFilter)).ToList();
            }
            else
            {
                var cardType = ParseCardTypeFromName(part);
                if (cardType.HasValue)
                    results = results.Where(x => x.cardType == cardType.Value).ToList();
            }
        }

        return results;
    }

    /// <summary>
    /// 执行所有挂起的变更操作
    /// </summary>
    private async Task ExecuteChangeLists()
    {
        // 遍历所有场上的卡牌，让每个卡牌执行自己的挂起变更操作
        foreach (var card in cardInPlaces)
        {
            if (card != null)
            {
                await card.ExecChangeList();
            }
        }
    }

    /// <summary>
    /// 获取第一个有效的友方支援阵线格子
    /// </summary>
    place_ GetTheFirstValidFriendlyPlace()
    {
        foreach (var place in supportLine)
        {
            if (place.GetMyCard() == null)
            {
                return place;
            }
        }
        return null;
    }

    /// <summary>
    /// 获取第一个有效的敌方支援阵线位置
    /// </summary>
    place_ GetTheFirstValidEnemySupportPlace()
    {
        foreach (var place in enemySupprotLine)
        {
            if (place.GetMyCard() == null)
            {
                return place;
            }
        }
        return null;
    }

    /// <summary>
    /// 评估if条件表达式
    /// 支持的条件格式:
    /// - result>n, result>=n, result<n, result<=n, result==n, result!=n
    /// - targets.count>n, targets.count>=n, targets.count<n, targets.count<=n, targets.count==n, targets.count!=n
    /// - target.isFriend, target.isEnemy
    /// - target.type==类型名 - 判断目标类型
    /// - target.cost>n, target.cost>=n, target.cost<n, target.cost<=n, target.cost==n, target.cost!=n - 判断目标费用
    /// - target.attack>n, target.attack>=n, target.attack<n, target.attack<=n, target.attack==n, target.attack!=n - 判断目标攻击力
    /// - target.defence>n, target.defence>=n, target.defence<n, target.defence<=n, target.defence==n, target.defence!=n - 判断目标防御力
    /// - source.isFriend, source.isEnemy
    /// </summary>
    private bool EvaluateCondition(string condition, int result, List<cardBase_> targets, cardBase_ sourceCard)
    {
        if (string.IsNullOrEmpty(condition))
        {
            return false;
        }

        condition = condition.Trim();

        // 先替换表达式中的变量（如 &result、&targetsCount、&sourceAttack 等）
        condition = ReplaceVariables(condition, result, targets, sourceCard);

        // 将常用的 target/source 数值字段展开为具体数字，方便表达式计算
        if (targets != null && targets.Count > 0)
        {
            var target = targets[0];
            condition = condition.Replace("target.cost", target.cost.ToString());
            condition = condition.Replace("target.attack", target.attack.ToString());
            condition = condition.Replace("target.defence", target.defence.ToString());
        }

        if (sourceCard != null)
        {
            condition = condition.Replace("source.attack", sourceCard.ReadAttack().ToString());
            condition = condition.Replace("source.defence", sourceCard.ReadDefence().ToString());
            condition = condition.Replace("source.cost", sourceCard.ReadCost().ToString());
        }

            condition = condition.Replace("result", result.ToString());

        // 处理布尔型快捷条件
        if (condition == "target.isFriend" && targets != null && targets.Count > 0)
        {
            return targets[0].GetIsFriend() == IsFriend.friend;
        }
        if (condition == "target.isEnemy" && targets != null && targets.Count > 0)
        {
            return targets[0].GetIsFriend() == IsFriend.enemy;
        }

        if (condition == "target.isHq" && targets != null && targets.Count > 0)
        {
            return targets[0].isHq == HQ.hq;
        }

        if (condition == "source.isFriend" && sourceCard != null)
        {
            return sourceCard.GetIsFriend() == IsFriend.friend;
        }
        if (condition == "source.isEnemy" && sourceCard != null)
        {
            return sourceCard.GetIsFriend() == IsFriend.enemy;
        }

        // 否定形式
        if (condition == "!target.isFriend" && targets != null && targets.Count > 0)
            return targets[0].GetIsFriend() != IsFriend.friend;
        if (condition == "!target.isEnemy" && targets != null && targets.Count > 0)
            return targets[0].GetIsFriend() != IsFriend.enemy;
        if (condition == "!target.isHq" && targets != null && targets.Count > 0)
            return targets[0].isHq != HQ.hq;
        if (condition == "!source.isFriend" && sourceCard != null)
            return sourceCard.GetIsFriend() != IsFriend.friend;
        if (condition == "!source.isEnemy" && sourceCard != null)
            return sourceCard.GetIsFriend() != IsFriend.enemy;

        // 处理 target.cardType ==/!= 类型比较（支持 Tank 等枚举）
        if (targets != null && targets.Count > 0)
        {
            if (condition.StartsWith("target.cardType=="))
            {
                string type = condition.Substring("target.cardType==".Length).Trim();
                return targets[0].cardType.ToString() == type;
            }
            if (condition.StartsWith("target.cardType!="))
            {
                string type = condition.Substring("target.cardType!=".Length).Trim();
                return targets[0].cardType.ToString() != type;
            }
            
            // 处理 target.name ==/!= 名称比较
            if (condition.StartsWith("target.name=="))
            {
                string name = condition.Substring("target.name==".Length).Trim();
                return targets[0].id == name;
            }
            if (condition.StartsWith("target.name!="))
            {
                string name = condition.Substring("target.name!=".Length).Trim();
                return targets[0].id != name;
            }
        }

        // 处理数值比较表达式，支持表达式计算（包括加减乘除/括号/变量）
        // 支持操作符: >=, <=, ==, !=, >, <
        string[] operators = new[] { ">=", "<=", "==", "!=", ">", "<" };
        foreach (var op in operators)
        {
            int idx = condition.IndexOf(op, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var left = condition.Substring(0, idx).Trim();
                var right = condition.Substring(idx + op.Length).Trim();

                int leftValue = EvaluateExpression(left, result, targets, sourceCard);
                int rightValue = EvaluateExpression(right, result, targets, sourceCard);

                switch (op)
                {
                    case ">=": return leftValue >= rightValue;
                    case "<=": return leftValue <= rightValue;
                    case "==": return leftValue == rightValue;
                    case "!=": return leftValue != rightValue;
                    case ">": return leftValue > rightValue;
                    case "<": return leftValue < rightValue;
                }

                break; // 只处理第一个匹配的比较符号
            }
        }

        return false;
    }

    /// <summary>
    /// 查找与指定foreach指令匹配的End&指令索引，支持嵌套
    /// </summary>
    /// <param name="parts">已分割的效果指令数组</param>
    /// <param name="foreachIndex">foreach指令在parts中的索引</param>
    /// <returns>匹配的End&索引，若未找到则返回foreachIndex</returns>
    private int FindMatchingEndAndForIndex(string[] parts, int foreachIndex)
    {
        int depth = 0;
        for (int j = foreachIndex + 1; j < parts.Length; j++)
        {
            string p = parts[j].Trim().ToLowerInvariant();
            if (p == "foreach")
            {
                depth++;
            }
            else if (p == "end&")
            {
                if (depth == 0)
                {
                    return j;
                }
                depth--;
            }
        }
        return foreachIndex; // 未找到匹配的End&
    }

}

/// <summary>
/// 玩家 手牌和状态记录在这里
/// </summary>
public class Player
{

    /// <summary>
    /// 卡组
    /// </summary>
    List<cardBase_> deck;
    List<cardBase_> cardsInHand = [];
    // 当前鼠标悬停的手牌索引（用于悬停效果）
    private int hoveredHandIndex = -1;

    // 布局参数（可调）
    private float handSpacing = 120f;           // 卡牌间距（可根据手牌数动态变化）
    private float handArcHeight = 20f;          // 卡牌排列时的弧线高度（中心向上）
    private float hoverRaise = 60f;             // 悬停时抬起高度
    private float hoverSidePush = 45f;          // 悬停时其他卡向两侧让开的距离（最大值）
    private float hoverScale = 1.1f;            // 悬停时缩放倍数

    int maxHandSize = 9;

    /// <summary>
    /// 牌库里待抽卡牌的停放位置：屏幕外的固定点。
    /// 牌库中的卡靠"停在屏幕外"来隐藏，而不是靠 Visible=false
    /// ——后者一旦设上就没有任何路径会恢复，会导致抽到手上却是空位。
    /// </summary>
    public static readonly Godot.Vector2 DeckParkPosition = new(-2000, 800);
    IsFriend isFriend;
    MeterLabel pointLabel;
    MeterLabel pointMaxLabel;

    int      point              = 1;
    int      pointMax           = 1;
    readonly int pointMaxMax    = 12;
    readonly int pointMaxMaxMax = 24;

    public int ReadPoint()
    {
        return point;
    }

    public int ReadPointMax()
    {
        return pointMax;
    }

    public void AddPoint(int i = 1)
    {
        int oldPoint = point;
        // 上限取 pointMaxMaxMax 而非 pointMax：AddPoint 表达的是"获得指挥点"，
        // 效果来源（喀秋莎攻击、近卫步兵第4团抽牌、无产者联合起来弃牌）需要能
        // 把点数攒到本回合上限之上；若封顶在 pointMax，而回合开始又会 RefreshPoint
        // 刷满，这些效果就永远无效。逐回合的预算约束由 RefreshPoint 提供。
        if(point + i >= pointMaxMaxMax) {point = pointMaxMaxMax;}
        else if(point+i<=0) point=0;
        else point += i;

        _ = pointLabel.AnimateTo(point);
    }


    public void AddPointMax(int i = 1)
    {
        int oldMax = pointMax;
        if(pointMax + i >= pointMaxMaxMax) {pointMax = pointMaxMaxMax;}
        else pointMax += i;
        _ = pointMaxLabel.AnimateTo(pointMax);
    }

    public Boolean UsePoint(int x)
    {
        if(point >= x)
        {
            int oldPoint = point;
            point -= x;
            _ = pointLabel.AnimateTo(point);
            return true;
        }
        else
        {
            return false;
        }

    }

    /// <summary>
    /// 检查当前点数是否足够支付 x 点（不改变点数）
    /// </summary>
    public bool HasPoint(int x)
    {
        return point >= x;
    }

    /// <summary>
    /// 返还点数（当指令目标不合法时调用）
    /// </summary>
    public void RestorePoint(int x)
    {
        int oldPoint = point;
        point += x;
        if (point > pointMax) point = pointMax;
        _ = pointLabel.AnimateTo(point);
    }

    public void RefreshPoint()
    {
        int oldPoint = point;
        point = pointMax;
        _ = pointLabel.AnimateTo(point);
    }


    public void AddPointMaxNatural()
    {
        if (pointMax + 1 <= pointMaxMax)
        {
            int oldMax = pointMax;
            pointMax += 1;
            _ = pointMaxLabel.AnimateTo(pointMax);
        }
        RefreshPoint();

    }

    public List<cardBase_> GetCardsInHand()
    {
        return cardsInHand;
    }

    public int ReadDeckCount()
    {
        return deck?.Count ?? 0;
    }

    /// <summary>当前牌堆的卡（供选择器 ${deck} 使用）</summary>
    public List<cardBase_> GetCardsInDeck()
    {
        return deck;
    }

    /// <summary>手牌上限（供指令 GetHandMax() 使用，避免在效果脚本里写死数字）</summary>
    public int ReadHandMax()
    {
        return maxHandSize;
    }

    /// <summary>
    /// 获取当前悬停的手牌索引
    /// </summary>
    public int GetHoveredHandIndex()
    {
        return hoveredHandIndex;
    }

    /// <summary>
    /// 将卡牌添加到手牌
    /// </summary>
    /// <param name="card"></param>
    public async Task AddCardToHand(cardBase_ card)
    {
        // 总部不该出现在手牌里。这是「进手牌」的唯一入口，在这里喊一声，
        // 任何把总部送进手牌的路径都会在控制台留下痕迹（含它当时的状态）。
        if (card != null && card.isHq == HQ.hq)
        {
            GD.Print($"[AddCardToHand] 总部 {card.id} 被加入手牌！"
                   + $"状态={card.getState()} 我方={card.GetIsFriend()}——"
                   + "总部只应待在支援阵线，请查调用方");
        }

        // 如果手牌已满，直接弃掉（动画后台播放，不阻塞效果结算）
        if (cardsInHand.Count >= maxHandSize)
        {
            battlefield.AddToBattleField(card);
            _ = battlefield.CardDiscardAndRemove(card);
            return;
        }
        if(!cardsInHand.Contains(card))
    {
            cardsInHand.Add(card);
        }
        if(!battlefield.ReadCardInPlaces().Contains(card))
        {
            battlefield.AddToBattleField(card);
        }
        else if (card.GetParent() != battlefield)
        {
            // 卡已在追踪中但被Reparent到其他节点（如ShowCardChoice的choiceLayer）
            card.Reparent(battlefield);
        }
        card.setState(CardState.inHand);
        RefreshMyHand();
    }

    public async Task AddCardToHand(CardData card)
    {
        // 如果手牌已满，直接弃掉（动画后台播放，不阻塞效果结算）
        if (cardsInHand.Count >= maxHandSize)
        {
            var cardRes = ResourceManager.Instance?.GetScene("res://bin/cardbase.tscn")
                          ?? ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
            var _card = cardRes?.Instantiate() as cardBase_;
            if (_card == null)
                return;

            _card.SetCardInformation(card);
            _card.SetIsFriend(isFriend);
            battlefield.AddToBattleField(_card);
            _ = battlefield.CardDiscardAndRemove(_card);
            return;
        }

        // 尝试从资源池获取一个空卡牌（减少实例化开销）
        var newCard = ResourceManager.Instance?.AcquireEmptyCardSync();
        if (newCard == null)
        {
            var cachedCardRes = ResourceManager.Instance?.GetScene("res://bin/cardbase.tscn")
                                ?? ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
            newCard = cachedCardRes?.Instantiate() as cardBase_;
        }

        if (newCard == null)
            return;

        newCard.SetCardInformation(card);
        newCard.SetIsFriend(isFriend);
        cardsInHand.Add(newCard);
        battlefield.AddToBattleField(newCard);
        newCard.setState(CardState.inHand);
        RefreshMyHand();
        return;
    }

    public void RemoveFromHand(cardBase_ card)
    {
        cardsInHand.Remove(card);
        RefreshMyHand();
    }


    Godot.Vector2 initPos;
    battlefield_ battlefield;
    public Player(Godot.Vector2 initPos,battlefield_ battlefield,IsFriend _isFriend)
    {
        //初始化 设置手的位置
        this.initPos     = initPos;
        this.battlefield = battlefield;
        isFriend         = _isFriend;
        
        PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        //temp
        // 从deck.ini文件初始化卡组
        deck = new List<cardBase_>();
        InitializeDeckFromIni();
        
        pointLabel = battlefield.GetNode<MeterLabel>("pointMeter");
        pointMaxLabel = battlefield.GetNode<MeterLabel>("pointMaxMeter");
        // 初始化点数显示（无需动画）
        pointLabel.DisplayImmediate(point);
        pointMaxLabel.DisplayImmediate(pointMax);
    }
    
    /// <summary>
    /// 初始化卡组：首次从deck.ini加载并持久化DeckCardIds，后续从DeckCardIds重建。
    /// 保证跨战役的奖励卡牌不会丢失。
    /// </summary>
    private void InitializeDeckFromIni()
    {
        // 如果已初始化，从持久化的ID列表重建卡组
        if (BattleStateManager.IsDeckInitialized)
        {
            GD.Print("[Deck] 从持久化DeckCardIds重建卡组，卡片数: " + BattleStateManager.DeckCardIds.Count);
            var template = battlefield.GetCardMaganer().GetCardTemplate();
            foreach (var cardId in BattleStateManager.DeckCardIds)
            {
                var cardData = battlefield.GetCardMaganer().GetCard(cardId);
                if (cardData != null)
                {
                    var card = template.Duplicate() as cardBase_;
                    card.PinDesignSize();
                    card.SetCardInformation(cardData);
                    card.SetIsFriend(isFriend);
                    deck.Add(card);
                }
            }
            ShuffleDeck();
            return;
        }

        PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");

        // 首次加载：从deck.ini读取并持久化
        var deckIniPath = "res://bin/deck.ini";
        if (!Godot.FileAccess.FileExists(deckIniPath))
        {
            GD.PushError($"Deck INI file not found: {deckIniPath}");
            return;
        }

        using var file = Godot.FileAccess.Open(deckIniPath, Godot.FileAccess.ModeFlags.Read);
        string deckIniContent = file.GetAsText();
        file.Close();

        var tempPath = OS.GetUserDataDir() + "/temp_deck.ini";
        using (var writer = System.IO.File.CreateText(tempPath))
            writer.Write(deckIniContent);

        var deckIni = new IniFile();
        deckIni.Load(tempPath);

        if (!deckIni.HasSection("deck"))
        {
            GD.PushError("Deck INI file does not contain [deck] section");
            return;
        }

        var deckKeys = deckIni.GetSectionKeys("deck");
        foreach (var key in deckKeys)
        {
            string cardIdValue = deckIni["deck"][key].ToString().Trim();
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
            {
                var cardData = battlefield.GetCardMaganer().GetCard(actualCardId);
                if (cardData != null && cardData.Rarity != Rarity.Unobtainable)
                {
                    var card = cardRes.Instantiate() as cardBase_;
                    card.PinDesignSize();
                    card.SetCardInformation(cardData);
                    card.SetIsFriend(isFriend);
                    deck.Add(card);
                    // 持久化卡牌ID
                    BattleStateManager.DeckCardIds.Add(actualCardId);
                }
            }
        }

        BattleStateManager.IsDeckInitialized = true;
        GD.Print($"[Deck] 首次加载完成，共{deck.Count}张卡，DeckCardIds已持久化");
        ShuffleDeck();
    }

/// <summary>
/// 重新排列所有手牌 每次改变自己的手牌的时候都要刷新
/// </summary>
    public void RefreshMyHand()
    {
        if (hoveredHandIndex >= cardsInHand.Count)
        {
            hoveredHandIndex = -1;
        }
        battlefield._displayOrderDirty = true;

        if (cardsInHand.Count == 0)
            return;

        // 动态调整间距（卡牌数量较多时自动收缩）
        float spacing = handSpacing;
        if (cardsInHand.Count > 8)
            spacing = Mathf.Max(80f, handSpacing - (cardsInHand.Count - 8) * 6f);

        float centerIndex = (cardsInHand.Count - 1) / 2f;
        float baseX = initPos.X - centerIndex * spacing;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            float referenceIndex = hoveredHandIndex >= 0 ? hoveredHandIndex : centerIndex;
            float distFromReference = i - referenceIndex;

            // 计算最大距离，用于归一化（防止边缘超出 1 导致所有角度一致）
            float maxDist = hoveredHandIndex >= 0
                ? MathF.Max(hoveredHandIndex, cardsInHand.Count - 1 - hoveredHandIndex)
                : centerIndex;

            float norm = maxDist == 0f ? 0f : distFromReference / maxDist;
            norm = Mathf.Clamp(norm, -1f, 1f);

            // 弧线布局：中间位置为基线，两侧逐渐向下（越远越低）
            // (当没有选中卡牌时，弧度会减小以避免卡牌两侧过高)
            float arcHeightScale = 1f;// (hoveredHandIndex >= 0) ? 1f : 0.35f;
            // 更平缓的弧度：用非线性函数减小靠近中间时的抬升
            float arcOffset = handArcHeight * Mathf.Pow(Mathf.Abs(norm), 1.45f) * arcHeightScale;

            // 悬停时抬起的额外高度（仅悬停的卡片）
            float hoverOffset = (hoveredHandIndex == i) ? -hoverRaise : 0f;

            // 侧向推开：距离越远推开越多（形成扇形效果）
            float sidePush = 0f;
            if (hoveredHandIndex >= 0)
                sidePush = MathF.Sign(norm) * hoverSidePush * Mathf.Abs(norm);

            Vector2 target = new Vector2(baseX + i * spacing + sidePush, initPos.Y + arcOffset + hoverOffset);

            // 跳过已临时Reparent到其他节点的卡（如换牌界面期间）。
            // 补抽会触发本方法，若不跳过，屏幕上正在展示的起手牌会被拉回手牌区。
            if (cardsInHand[i].GetParent() != battlefield)
                continue;

            bool isHovered = (i == hoveredHandIndex);
            bool isDragging = cardsInHand[i].getState() == CardState.caught || cardsInHand[i].getState() == CardState.inplaceAndCaught;

            // 拖动状态下，不让自动布局移动该卡（避免与鼠标跟随冲突）
            if (isDragging)
                continue;

            // 计算倾斜角度：距离越远倾斜越大，中心保持正直
            float maxTiltDeg = 18f;
            float tiltPerIndex = maxTiltDeg / MathF.Max(1f, maxDist);
            float targetRotationDeg = Mathf.Clamp(distFromReference * tiltPerIndex, -maxTiltDeg, maxTiltDeg);

            // 悬停的卡牌始终正直
            if (isHovered)
                targetRotationDeg = 0f;

            float targetScale = isHovered ? hoverScale : 1f;
            cardsInHand[i].SetHover(isHovered, targetScale, targetRotationDeg);

            // 仅启动移动动画，不等待完成，避免在增加手牌时出现卡顿
            // 缩短动画时长让响应更迅速
            _ = cardsInHand[i].MoveToPosition(target, 0.12f);
        }
    }

    /// <summary>
    /// 更新当前鼠标悬停的手牌索引，并在变化时刷新手牌布局。
    /// </summary>
    public void UpdateHover(Vector2 mousePosition)
    {
        if (isFriend != IsFriend.friend)
            return;

        if (cardsInHand.Count == 0)
            return;

        float handTopY = initPos.Y - 100;
        if (mousePosition.Y < handTopY)
        {
            if (hoveredHandIndex != -1)
            {
                hoveredHandIndex = -1;
                RefreshMyHand();
            }
            return;
        }

        int newHover = -1;
        for (int i = 0; i < cardsInHand.Count; i++)
        {
            var card = cardsInHand[i];
            if (card != null && card.GetGlobalRect().HasPoint(mousePosition))
            {
                newHover = i;
                break;
            }
        }

        if (newHover != hoveredHandIndex)
        {
            hoveredHandIndex = newHover;
            RefreshMyHand();
        }
    }


/// <summary>
/// 我的回合抽卡! 抽卡 默认1张
/// </summary>
/// <param name="number"></param>
    public async Task DrawCard()
    {
        if (deck.Count > 0)
        {
            var card = deck[0];
            deck.RemoveAt(0);
            battlefield.PlayDrawSound();
            if (cardsInHand.Count >= maxHandSize)
            {
                battlefield.AddToBattleField(card);
                _ = battlefield.CardDiscardAndRemove(card);
                return;
            }
            card.SetPosition(DeckParkPosition);
            // 进手牌的卡必须可见：牌库中的卡靠停在屏幕外隐藏，
            // 但别处（如换牌界面）可能把 Visible 设成 false，这里统一兜底恢复。
            card.Visible = true;
            await AddCardToHand(card);
            SetLastDrawnCards(new List<cardBase_> { card });
            await battlefield.TriggerFriendlyCardDrawn(card);
        }
        
    }



    /// <summary>
    /// 我的回合抽卡! 抽卡 x张
    /// </summary>
    /// <param name="number"></param>
    public async Task DrawCard(int number)
    {
        for(int i = 0; i < number; i++)
        {
            await DrawCard();
        }
    }

    /// <summary>
    /// 起手换牌：把选中的手牌洗回牌库，再补抽等量张。
    /// 按炉石做法先整副洗牌再抽，因此理论上可能抽回刚换掉的牌。
    /// 返回新抽到的牌，供换牌界面在原位展示。
    /// </summary>
    public async Task<List<cardBase_>> MulliganAsync(List<cardBase_> returned)
    {
        var drawn = new List<cardBase_>();
        if (returned == null || returned.Count == 0) return drawn;

        int count = 0;
        foreach (var card in returned)
        {
            if (card == null || !cardsInHand.Remove(card)) continue;
            deck.Add(card);
            count++;
        }
        if (count == 0) return drawn;

        ShuffleDeck();

        var before = new HashSet<cardBase_>(cardsInHand);
        await DrawCard(count);
        foreach (var card in cardsInHand)
            if (!before.Contains(card)) drawn.Add(card);
        return drawn;
    }

    /// <summary>
    /// 将指定卡牌加入卡组
    /// </summary>
    /// <param name="cardId">卡牌ID</param>
    public void AddCardToDeck(string cardId)
    {
        var cardData = battlefield.GetCardMaganer().GetCard(cardId);
        if (cardData != null)
        {
            PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
            var card = cardRes.Instantiate() as cardBase_;
            card.SetCardInformation(cardData);
            card.SetIsFriend(isFriend);
            deck.Add(card);
            lastDrawnCards = [card];
        }
        ShuffleDeck();
    }

    public void AddExistingCardToDeck(cardBase_ card)
    {
        deck.Add(card);
    }

    // 存储最近抽到的卡牌引用
    private List<cardBase_> lastDrawnCards = new List<cardBase_>();

    /// <summary>
    /// 获得刚刚被抽到的卡的引用
    /// </summary>
    public List<cardBase_> GetLastDrawnCards()
    {
        return new List<cardBase_>(lastDrawnCards);
    }

    public void SetLastDrawnCards(List<cardBase_> c)
    {
        lastDrawnCards = c;
    }


    /// <summary>
    /// 从卡组中抽出i张名字含s字符串的卡
    /// </summary>
    public async Task DrawCardsWithName(string namePattern, int count)
    {
        lastDrawnCards.Clear();
        for (int i = 0; i < count; i++)
        {
            var card = deck.FirstOrDefault(c => c.id.Contains(namePattern));
            if (card != null)
            {
                deck.Remove(card);
                battlefield.PlayDrawSound();
                if (cardsInHand.Count >= maxHandSize)
                {
                    await card.DiscardCard();
                    battlefield.RemoveCard(card);
                }
                else
                {
                    card.SetPosition(DeckParkPosition);
                    await AddCardToHand(card);
                    lastDrawnCards.Add(card);
                }
            }
        }
    }

    /// <summary>
    /// 从卡组中抽出i张类型为u的卡
    /// </summary>
    public async Task DrawCardsWithType(string unitType, int count)
    {
        lastDrawnCards.Clear();
        if (Enum.TryParse<CardTypes>(unitType, out CardTypes targetType))
        {
            for (int i = 0; i < count; i++)
            {
                var card = deck.FirstOrDefault(c => c.cardType == targetType);
                if (card != null)
                {
                    deck.Remove(card);
                    battlefield.PlayDrawSound();
                    if (cardsInHand.Count >= maxHandSize)
                    {
                        battlefield.AddToBattleField(card);
                        await card.DiscardCard();
                        battlefield.RemoveCard(card);
                    }
                    else
                    {
                        card.SetPosition(DeckParkPosition);
                        await AddCardToHand(card);
                        lastDrawnCards.Add(card);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 从卡组中抽取i张单位卡（非指挥卡）
    /// </summary>
    /// <param name="count">抽取数量</param>
    public async Task DrawUnitCards(int count)
    {
        lastDrawnCards.Clear();
        for (int i = 0; i < count; i++)
        {
            // 只抽取非指挥卡（非Command类型）
            var card = deck.FirstOrDefault(c => c.cardType != CardTypes.Command);
            if (card != null)
            {
                deck.Remove(card);
                battlefield.PlayDrawSound();
                if (cardsInHand.Count >= maxHandSize)
                {
                    battlefield.AddToBattleField(card);
                    await card.DiscardCard();
                    battlefield.RemoveCard(card);
                }
                else
                {
                    card.SetPosition(DeckParkPosition);
                    await AddCardToHand(card);
                    lastDrawnCards.Add(card);
                }
            }
        }
    }

    /// <summary>
    /// 随机弃置i张卡
    /// </summary>
    public async Task DiscardRandomly(int count)
    {
        Random random = new Random();
        int actualCount = Math.Min(count, cardsInHand.Count);

        for (int i = 0; i < actualCount; i++)
        {
            if (cardsInHand.Count == 0) break;

            int index = random.Next(cardsInHand.Count);
            var card = cardsInHand[index];

            // 总部绝不该出现在手牌里。真出现了说明有别的路径破坏了
            // 「总部只待在支援阵线」这条不变量（已知的一条是 RetreatUnit，
            // 已在那边堵住）。这里兜一手，别让它被当普通手牌弃掉——
            // RemoveCard(myHq) 会直接判负。留下日志，便于回头追是谁放进去的。
            if (card.isHq == HQ.hq)
            {
                GD.Print($"[DiscardRandomly] 手牌里出现总部 {card.id}（isHq={card.isHq}，"
                       + $"状态={card.getState()}），已跳过不弃置。请查是哪条路径把它放进手牌的");
                continue;
            }

            RemoveFromHand(card);
            await card.DiscardCard();
            battlefield.RemoveCard(card);
        }
    }

    /// <summary>
    /// 弃置i张名字含s的卡(若可能)
    /// </summary>
    public async Task DiscardCardsWithName(string namePattern, int count)
    {
        var cardsToDiscard = cardsInHand.Where(c => c.id.Contains(namePattern)).Take(count).ToList();

        foreach (var card in cardsToDiscard)
        {
            RemoveFromHand(card);
            battlefield.AddToBattleField(card);
            await card.DiscardCard();
            battlefield.RemoveCard(card);
        }
    }

    /// <summary>
    /// 洗牌 - 使用Fisher-Yates算法随机打乱卡组
    /// </summary>
    public void ShuffleDeck()
    {
        Random random = new Random();
        int n = deck.Count;

        // 从后向前遍历
        for (int i = n - 1; i > 0; i--)
        {
            // 生成0到i之间的随机索引
            int j = random.Next(i + 1);

            // 交换deck[i]和deck[j]
            cardBase_ temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }

    }

    public List<cardBase_> ReadMyDeck()
    {
        return deck;
    }



}


/// <summary>
/// 管理所有卡牌 用于加载卡
/// </summary>
public class CardMaganer
{
    private Dictionary<string, CardData> _items = [];
    private Random random = new Random();
    private cardBase_ _cardTemplate; // 惰性缓存的卡牌模板，用于Duplicate()

    /// <summary>
    /// 随机获得一张卡的数据
    /// </summary>
    /// <returns></returns>
    public CardData GetRandomCard()
    { 
        CardData randomCard;
        do
        {
            randomCard = _items.Values.ToList()[random.Next(0, _items.Count)];

        } while (randomCard.IsHq == HQ.hq);

        return randomCard;
    }

    public void SetCardDictionary(Dictionary<string, CardData> items)
    {
        // 将传入的卡牌字典赋值给私有字段_items
        _items = items;
    }


/// <summary>
/// 用一个名字获得对应的数据
/// </summary>
/// <param name="id"></param>
/// <returns></returns>
    public CardData GetCard(string id)
    {
        if (_items.TryGetValue(id, out CardData value))
        {
            return value;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// 获取所有卡牌数据列表
    /// </summary>
    public List<CardData> GetAllCards()
    {
        return _items.Values.ToList();
    }

/// <summary>
/// 加载hq isfriend 0表示敌方 1表示我方
/// </summary>
/// <param name="isFriend"></param>
    public cardBase_ LoadHq(int isFriend)
    {
        PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
            var card = cardRes.Instantiate() as cardBase_;
            // 根据isFriend参数加载不同的HQ卡牌数据
            if(isFriend == 1)
            {
                card.SetCardInformation(GetCard("moscow"));
                card.SetIsFriend(IsFriend.friend);
            }
            else
            {
                card.SetCardInformation(GetCard("berlin"));
                card.SetIsFriend(IsFriend.enemy);
            }
            return card;

    }

    /// <summary>
    /// 获取卡牌模板实例（惰性创建），供外部通过Duplicate()复制卡牌。
    /// 模板不入场景树，避免不必要的渲染开销。
    /// </summary>
    public cardBase_ GetCardTemplate()
    {
        if (_cardTemplate == null)
        {
            PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
            _cardTemplate = cardRes.Instantiate() as cardBase_;
        }
        return _cardTemplate;
    }

}
