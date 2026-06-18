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
    /// 是否暂停死亡检查 0 不暂停 1 暂停
    /// </summary>
    int pauseDeathCheck = 0;

    /// <summary>
    /// 允许控制输入
    /// </summary>
    void AllowControl()
    {
        allowControl = 0;
        buttonNextTurn.Disabled = false;
    }
    
    /// <summary>
    /// 禁止控制输入
    /// </summary>
    void ForbidControl()
    {
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

    private CanvasLayer choiceLayer;
    private ColorRect choiceDim;
    private HBoxContainer choiceContainer;
    private List<cardBase_> choiceCards = new List<cardBase_>();

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

        if (cardNowChoose != null)
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

                // 弃牌动画中的卡不参与Z-index/子节点顺序重置
                if (card.isDiscarding)
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

        foreach (var card in player1.GetCardsInHand())
        {
            MoveChild(card,1);
            int cardIndex = player1.GetCardsInHand().IndexOf(card);
            card.ZIndex = (cardIndex == player1.GetHoveredHandIndex()) ? 30 : 20;
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
        ResumeDeathCheck(); // 恢复死亡检查
        CheckIfAnyUnitDiedAsync(); // 检查死亡
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
            if (card.GetGlobalRect().HasPoint(mousePosition))
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


List<Bullet> bullets;
AudioStreamPlayer2D battleSound;
AudioStreamPlayer2D deadSound;
TextureButton buttonNextTurn;

/// <summary>
/// 初始化
/// </summary>
    public override void _Ready()
    {
        GD.Print("_Ready method called");

        // 资源管理器：缓存纹理/场景并维护空卡牌池（同步初始化，一次性完成）
        var resourceManager = new ResourceManager();
        AddChild(resourceManager);
        resourceManager.Initialize();

        CreateChoiceOverlay();

        // 保证卡牌列表立刻可用，避免 Sort/Refresh 时出现 null
        cardInPlaces = new List<cardBase_>();
        //初始化各个阵线
        enemySupprotLine = [(place_)GetNode("Place1"), (place_)GetNode("Place2"), (place_)GetNode("Place3"), (place_)GetNode("Place4"), (place_)GetNode("Place5")];
        frontLine = [(place_)GetNode("Place6"), (place_)GetNode("Place7"), (place_)GetNode("Place8"), (place_)GetNode("Place9"), (place_)GetNode("Place10")];
        supportLine = [(place_)GetNode("Place11"), (place_)GetNode("Place12"), (place_)GetNode("Place13"), (place_)GetNode("Place14"), (place_)GetNode("Place15")];
        allPlaces = [.. enemySupprotLine, .. frontLine, .. supportLine];

        //子弹效果
        bullets = new List<Bullet>();
        
        for(int i=0; i < 10; i++)
        {
            var bulletScene = ResourceManager.Instance?.GetScene("res://bin/bullet.tscn");
            bullets.Add(((bulletScene ?? ResourceLoader.Load<PackedScene>("res://bin/bullet.tscn")).Instantiate() as Bullet));
            AddChild(bullets[i]);
            bullets[i].Visible = false;
        }
    

        deadSound = GetNode<AudioStreamPlayer2D>("deadSound");
        battleSound = GetNode<AudioStreamPlayer2D>("battleSound");
        
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
                card.CardType    = GetTypes(configFile[section.Key]["cardType"].ToString().Trim());
                card.Rarity      = GetRarity(configFile[section.Key]["rarity"].ToString().Trim());
                card.IconPath    = configFile[section.Key]["icon"].ToString().Trim();
                card.TargetType  = GetTargetType(configFile[section.Key]["targetType"].ToString().Trim());
                card.Traits      = GetTraitList(configFile[section.Key]["traits"].ToString().Trim());
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


        enemyHq = cardMaganer.LoadHq(2);
        AddCardToPlace(enemyHq,enemySupprotLine[2]);
        
        // 加载敌方行动队列（战役模式从BattleStateManager读取，否则默认berlin）
        string enemyPreset = BattleStateManager.IsCampaignMode
            ? BattleStateManager.SelectedEnemy
            : "berlin";
        LoadEnemyActionQueue(enemyPreset);
        GD.Print($"Loaded enemy preset: {enemyPreset}");

        //初始化敌人
        EnemyInit();
        
        GetNode<End>("end").Visible = false;

        player1.DrawCard(5);

        // 右上角"查看卡组"按钮
        CreateDeckViewButton();
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


    UnitTraits GetTraitList(string s)
    {
        try
        {
        var        tritList = s.Split(',').Select(x => (UnitTraits)Enum.Parse(typeof(UnitTraits), x.Trim())).ToList();
        UnitTraits outTrait = UnitTraits.None;
        foreach(var trait in tritList)
        {
            outTrait |= trait;
        }
        return outTrait;
        }
        catch(Exception e)
        {
            
        }
        return UnitTraits.None;
        
    }


    /// <summary>
    ///  将字符串转换为TargetType枚举值 如果转换失败则默认为anyTarget
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    private TargetType GetTargetType(string v)
    {
        if (System.Enum.TryParse<TargetType>(v, ignoreCase: true, out var result))
        {
            return result;
        }
        return TargetType.anyTarget;
    }




    async Task FlyBullets(cardBase_ from, cardBase_ to)
    {
        // 检查目标是否已被释放
        if (to == null || !IsInstanceValid(to))
        {
            return;
        }

        var rnd = new Random();
        foreach(var bullet in bullets)
        {
            // 检查子弹是否有效
            if (bullet == null || !IsInstanceValid(bullet))
            {
                continue;
            }

            // 检查目标和源是否仍然有效
            if (!IsInstanceValid(from) || !IsInstanceValid(to))
            {
                break;
            }

            if (!bullet.Visible)
            {
                bullet.Fly(from, to);
                await Task.Delay(rnd.Next(0,100));
            }
        }
    }

/// <summary>
/// 稀有度->string
/// </summary>
/// <param name="rare"></param>
/// <returns></returns>
    public static Rarity GetRarity(string rare)
    {
        switch (rare.ToLower())
        {
            case "common":
                return Rarity.Common;
            case "rare":
                return Rarity.Rare;
            case "epic":
                return Rarity.Epic;
            case "unobtainable":
                return Rarity.Unobtainable;
            default:
                return Rarity.Legendary;
        }
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
/// 播放死亡音效
/// </summary>
/// <param name="id"></param>
    void PlayDeadSound(int id)
    {
        //if (deadSound.Playing != true)
        {
            deadSound.Play();
        }
    }

/// <summary>
/// 转换卡牌的类型 -> string
/// </summary>
/// <param name="type"></param>
/// <returns></returns>
    public static CardTypes GetTypes(string type)
    {
        switch (type)
        {
            case "Tank":
                return CardTypes.Tank;
            case "Artillery":
                return CardTypes.Artillery;
            case "Plane":
                return CardTypes.Plane;
            case "Bomber":
                return CardTypes.Bomber;
            case "Command":
                return CardTypes.Command;
            default:
                return CardTypes.Infantry;
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
    private static readonly string[] ConsoleCommands = new[]
    {
        "myHq", "enemyHq", "this", "target", "GetCardBeingAddToSupportLine",
        "Heal()", "damage()", "GetAttack()", "LoseAttack()", "SetDefence()", "addDefence()",
        "setResult()", "setTarget", "drawCard", "DrawUnitCards()",
        "GetEffect()", "AddToHand()", "addToSupportLine()", "addToEnemySupportLine()",
        "addToDeck()", "SetMemory()", "AddPoint()", "AddPointMax()",
        "displayAllCardState", "GetAllFriendUnits", "GetAllEnemyUnits", "GetAllFriendTargets", "GetAllEnemyTargets",
        "GetEnemyHq", "GetFriendHq", "GetRandomFriendUnit", "GetRandomEnemyUnit",
        "GetRandomFriendTarget", "GetRandomEnemyTarget", "GetRandomNumber()",
        "KillAllTargets", "KillAllTarget", "HealAllTargets", "Refresh", "Retreat", "Discard",
        "foreach", "End&", "Develop", "Choose()", "Play",
        "AddTrait()", "RemoveTrait()", "DrawACard()", "GetCardsBeingTreated",
        "getCount()", "setTargets()", "DiscardRandomly()", "DiscardWithName()",
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
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.85f);
        _consolePanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_consolePanel);

        _consoleInput = new LineEdit();
        _consoleInput.Position = new Vector2(0, 0);
        _consoleInput.Size = new Vector2(600, 30);
        _consoleInput.AddThemeColorOverride("font_color", Colors.LimeGreen);
        _consoleInput.AddThemeFontSizeOverride("font_size", 14);
        _consoleInput.PlaceholderText = "输入效果指令，回车执行...";
        _consoleInput.TextSubmitted += OnConsoleSubmit;
        _consolePanel.AddChild(_consoleInput);

        _consoleOutput = new RichTextLabel();
        _consoleOutput.Position = new Vector2(4, 34);
        _consoleOutput.Size = new Vector2(592, 162);
        _consoleOutput.ScrollFollowing = true;
        _consoleOutput.BbcodeEnabled = true;
        _consoleOutput.AddThemeColorOverride("default_color", Colors.LimeGreen);
        _consoleOutput.AddThemeFontSizeOverride("normal_font_size", 12);
        _consolePanel.AddChild(_consoleOutput);
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
        ConsolePrint($"> {cmd}");
        // 以友方总部为sourceCard和targetCard执行，确保setTarget和drawCard等指令能正常工作
        await ParseAndExecuteEffect(cmd, myHq, null, myHq);
        CheckIfAnyUnitDiedAsync();
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

    public override void _Input(InputEvent @event)
    {
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

    //如果当前正处于无法操作状态 取消这一次操作
    if (ReadControlState() == 1) return;

    if (@event is InputEventMouseButton mouseButton)
    {
        // 处理卡牌选择界面的输入
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (isShowingChoiceUI)
                {
                    HandleChoiceCardClick(GetGlobalMousePosition());
                    return; // 选择界面中不要处理其他输入
                }
            }

            
            var mousePosition = GetGlobalMousePosition();
            if (mouseButton.Pressed)
        {
            var card = CheckCardClick(mousePosition);
            if(currentInputState != InputState.waitingForChoosingTarget) cardNowChoose = card;
            if (card == null) return; // 没有点击到卡牌，不处理
            // 点击时立即将卡牌提升到最上层
            RefreshAllCardDisplayOrder();
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

        if (mouseButton.Pressed==false)
            {
                
                
                if(cardNowChoose== null ) return; // 没有卡牌被拖动，不处理
                var result = GetPlaceWithPosition(mousePosition);
                

                switch (currentInputState)
                {
                    case InputState.P_InHandCommandNeedChooseTarget:
                    if(result == null || result.GetMyCard() == null ||result.GetMyCard() != null && IsValidTarget(result.GetMyCard(), cardNowChoose.targetType)== false)
                        {
                            
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
                    else if(result.GetMyCard().GetIsFriend() != cardNowChoose.GetIsFriend())
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
                            _ = TriggerUnitEffects("BePicked", result.GetMyCard(), new List<cardBase_> { cardNowChoose }, checkOnlySourceCard: true);
                            // 同仇特性：被指向时友方同仇单位+1+1
                            _ = TriggerSharedHatred(result.GetMyCard());
                             _ = ParseAndExecuteEffect(cardNowChoose.effect, cardNowChoose, [result.GetMyCard()]);
                            CheckIfAnyUnitDiedAsync(); // 结算单位变化
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
        
        RefreshAllCardDisplayOrder();
        
        CheckIfAnyUnitDiedAsync();

            
    }
}

    public override void _Process(double delta)
    {
        RefreshAllCardDisplayOrder();

        // 悬停时手牌浮起并让开（仅对友方手牌生效）
        if (player1 != null)
        {
            // 拖动时不更新悬停状态，以免与拖动位置冲突
            if (cardNowChoose == null || cardNowChoose.getState() != CardState.caught)
            {
                player1.UpdateHover(GetGlobalMousePosition());
            }
            else
            {
                player1.UpdateHover(new Vector2(-9999, -9999));
            }
        }

        // 跟踪卡牌拖动
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
                    case "lifeTime":
                        if (targets != null && targets.Count > 0)
                            sb.Append(targets[0].ReadLifeTime());
                        else if (sourceCard != null)
                            sb.Append(sourceCard.ReadLifeTime());
                        else
                            sb.Append(0);
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
    private async Task TriggerUnitEffects(string triggerPoint, cardBase_ sourceCard, List<cardBase_> targetCards = null, bool checkOnlySourceCard = false)
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
    /// 两军交战 注意攻击本身不会导致单位死亡 检查函数才会导致单位死亡
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param> 
    public async Task Attack(cardBase_ from,cardBase_ to)
    {
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

        // 计算攻击伤害
        int attackDamage = from.ReadAttack();
        // 重甲特性：单位受到的战斗伤害-1
        if (to.HasTrait(UnitTraits.HeavyArmor))
        {
            attackDamage = Math.Max(0, attackDamage - 1);
        }
        
        // 免疫特性：不受到战斗伤害
        if (to.HasTrait(UnitTraits.Immunity))
        {
            attackDamage = 0;
        }
        
        await to.LoseDefence(attackDamage);

        // 动员特性：受到伤害后消失
        if (to.HasMobilizeActive() && attackDamage > 0)
        {
            to.RemoveMobilize();
        }

        // 烟幕特性：单位第一次攻击时失去烟幕
        if (from.HasSmokeScreenActive())
        {
            from.RemoveSmokeScreen();
        }

        // 冲击特性：攻击时不受到反击，无视伏击
        bool attackerHasShock = from.HasShockActive();
        if (attackerHasShock)
        {
            from.RemoveShock(); // 攻击后失去冲击
        }

        // 判断是否进行反击（冲击特性完全免疫反击）
        if (!attackerHasShock)
        {
            // 战斗机、步兵、坦克、火炮可以反击敌人，轰炸机不能
            bool canCounterAttack = to.cardType != CardTypes.Bomber;

            // 战斗机、步兵、坦克在攻击后会受到反击，轰炸机、火炮不会
            bool willReceiveCounterAttack =
                from.cardType == CardTypes.Plane ||
                from.cardType == CardTypes.Infantry ||
                from.cardType == CardTypes.Tank;

            // 伏击特性：被攻击时先造成反击伤害（冲击无视伏击）
            bool defenderHasAmbush = to.HasAmbushActive();

            // 执行反击
            if (canCounterAttack && willReceiveCounterAttack)
            {
                int counterDamage = to.ReadAttack();

                // 重甲特性：单位受到的战斗伤害-1
                if (from.HasTrait(UnitTraits.HeavyArmor))
                {
                    counterDamage = Math.Max(0, counterDamage - 1);
                }

                // 免疫特性：不受到战斗伤害
                if (from.HasTrait(UnitTraits.Immunity))
                {
                    counterDamage = 0;
                }

                // 伏击特性：先造成反击伤害（一回合一次）
                if (defenderHasAmbush)
                {
                    to.FlashTraitIcon("Ambush");
                    to.UseAmbush(); // 标记伏击已被使用
                    await from.LoseDefence(counterDamage);
                    // 若敌方单位因此死亡，则不受到来自对方的伤害
                    if (from.ReadDefence() <= 0)
                    {
                        counterDamage = 0;
                    }
                }
                else
                {
                    await from.LoseDefence(counterDamage);
                }
            }
        }
        
        PlayBattleSound(1);
        await FlyBullets(from,to);

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

        ResumeDeathCheck(); // 恢复死亡检查
        CheckIfAnyUnitDiedAsync(); // 统一检查死亡
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
        card.MoveToPosition(position.GetGlobalPosition());
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
        }
        else
        {
            // 如果不是从手上部署，则调用HaveMoved()方法
            if (!isDeployedFromHand)
            {
                card.HaveMoved();
            }
            // 否则触发移动单位的 Moving 效果
            await TriggerUnitEffects("Moving", card, new List<cardBase_> { card }, checkOnlySourceCard: true);
        }
        ResumeDeathCheck(); // 恢复死亡检查
        CheckIfAnyUnitDiedAsync(); // 统一检查死亡
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
        RefreshAllCardInField();

        // 敌方回合开始时，对敌方单位应用动员等trait
        ApplyEnemyTurnStartTraits();

        // 执行敌方行动队列
        await ExecuteEnemyActionQueue();

        // 敌人行动 AI：移动并攻击
        await EnemyPerformActionsAsync();

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
        CheckIfAnyUnitDiedAsync(); // 检查死亡
    }

    /// <summary>
    /// 从enemyTurn.ini文件加载敌方行动队列
    /// </summary>
    /// <param name="enemyHqName">敌方总部名称，用于确定读取哪个节</param>
    void LoadEnemyActionQueue(string enemyHqName)
    {
        GD.Print($"LoadEnemyActionQueue called with enemyHqName: {enemyHqName}");
        enemyActionQueue.Clear(); // 清空现有队列
        
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
            var value = configFile[enemyHqName][key].ToString().Trim();
            GD.Print($"Loading action: key={key}, value={value}");
            

            
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
    /// 敌方行动：先把单位推进到前线（如果前线没有我方单位），再按优先级进行攻击
    /// 优先级：能破坏总部 -> 能破坏单位 -> 能攻击总部 -> 随机攻击单位
    /// </summary>
    async Task EnemyPerformActionsAsync()
    {
        // 1) 如果前线没有我方单位，尝试把敌方单位推进前线
        bool frontHasFriend = frontLine.Any(p => p.GetMyCard() != null && p.GetMyCard().GetIsFriend() == IsFriend.friend);
        if (!frontHasFriend)
        {
            var enemyUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
            foreach (var eCard in enemyUnits.Where(x=>x.isHq!=HQ.hq && x.CheckIfCanMove()))
            {
                // 总部不能移动
                if (eCard.isHq == HQ.hq) continue;

                // 空军单位和炮兵单位不会主动上前线
                if (eCard.cardType == CardTypes.Plane || eCard.cardType == CardTypes.Bomber || 
                    eCard.cardType == CardTypes.Artillery)
                {
                    continue;
                }

                if (frontLine.Contains(eCard.GetMyPlace()))
                {
                    continue;
                }
        // ============ 修复

                // 找到第一个空的前线格子
                var place = frontLine.FirstOrDefault(p => p.GetMyCard() == null);
                if (place == null) break;

                // 只有当单位还能移动时才尝试移动（CheckIfCanMove 会减少 moveAble）
                if (!eCard.CheckIfCanMove()) continue;

                await Move(eCard, place);
                eCard.HaveMoved();

                // 步兵、火炮、战斗机和轰炸机移动后不能攻击
                if (eCard.cardType == CardTypes.Infantry || eCard.cardType == CardTypes.Artillery || 
                    eCard.cardType == CardTypes.Plane || eCard.cardType == CardTypes.Bomber)
                {
                    eCard.HaveAttacked();
                }

                await Task.Delay(300);
            }
        }

        // 2) 攻击阶段：按优先级对每个敌方单位尝试攻击
        var rnd = new Random();
        var attackers = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
        GD.Print($"Enemy attack phase: Found {attackers.Count} enemy units");

        for (int i = 0; i < attackers.Count; i++)
        {
            var attacker = attackers[i];
            if (attacker == null) continue;

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
                await Attack(attacker, myHq);
                continue;
            }

            // 其次：能破坏任一单位则攻击该单位（仅在允许目标中寻找）
            var killable = allowedTargets.FirstOrDefault(u => CanDestroyTarget(attacker, u));
            if (killable != null)
            {
                await Attack(attacker, killable);
                continue;
            }

            // 其后：能攻击总部（非必定破坏，且总部在允许目标中）
            if (myHq != null && allowedTargets.Contains(myHq))
            {
                await Attack(attacker, myHq);
                continue;
            }

            // 最后：随机攻击一个允许的单位
            var targetRandom = allowedTargets[rnd.Next(allowedTargets.Count)];
            if (targetRandom != null)
            {
                await Attack(attacker, targetRandom);
                continue;
            }
        }
    }
    
    /// <summary>
    /// 刷一个敌方单位 目前只是随机从敌人卡组里抽一张放在支援阵线 之后会在这里写敌方ai的召唤逻辑
    /// </summary>
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
        // 如果死亡检查被暂停，则不执行
        if (pauseDeathCheck == 1)
        {
            return;
        }

        var allCards = ReadCardInPlaces().ToList();
        foreach (var c in allCards)
        {
            if (c == null)
                continue;

            c.ExecChangeList();
        }

        var units = ReadCardInPlaces().Where(x => x.getState() == CardState.placed).ToList();
        List<cardBase_> deadUnits = new List<cardBase_>();
        
        // 第一遍：收集所有死亡的单位
        for (int i = units.Count - 1; i >= 0; i--)
        {
            if (units[i].ReadDefence() <= 0)
            {
                deadUnits.Add(units[i]);
            }
        }
        
        // 第二遍：处理所有死亡单位的Dead效果
        foreach (var deadUnit in deadUnits)
        {
            TriggerUnitEffects("Dead", deadUnit, checkOnlySourceCard:true);
            RemoveCard(deadUnit);
            PlayDeadSound(1);
        }

        foreach (var c in allCards.Where(x=>x.shouldBeRemoved == 1).ToList())
        {
            if (c == null)
                continue;
            else
            {
                c.DisableCombatAbility();
                await c.DiscardCard();
                RemoveCard(c);
            }
        }

        if (deadUnits.Count > 0)
            CheckIfAnyUnitDiedAsync(); // 递归检查是否有新的死亡单位
    }


            /// <summary>
            /// 暂时用作测试
            /// </summary>
    public async void OnNextTurnButtonPressed()
    {
        // 触发友方回合结束时点
        await TriggerUnitEffects("FriendlyTurnEnd", null);

        // 触发双方回合结束时点
        await TriggerUnitEffects("TurnEnd", null);
        await CheckIfAnyUnitDiedAsync(); // 检查死亡
        
        
        // 触发敌方回合开始时点
        await TriggerUnitEffects("EnemyTurnBegin", null);
        
        // 增加所有已部署单位的存活回合数
        foreach(var card in cardInPlaces.Where(x=>x.getState()==CardState.placed).ToList())
        {
            card.IncrementLifeTime();
        }
        
        // 触发双方回合开始时点
        await TriggerUnitEffects("TurnBegin", null);
        
        // 触发友方回合开始时点
        await TriggerUnitEffects("FriendlyTurnBegin", null);

        // 回合开始时trait处理
        ApplyTurnStartTraits();

        // 增加所有已部署单位的存活回合数
        foreach(var card in cardInPlaces.Where(x=>x.getState()==CardState.placed).ToList())
        {
            card.IncrementLifeTime();
        }
        
        CheckIfAnyUnitDiedAsync(); // 检查死亡

        await EnemyTurnAsync();
        
        _ = player1.DrawCard();
        player1.AddPointMaxNatural();
        
    }

    void RefreshAllCardInField()
    {
        foreach(var card in cardInPlaces.Where(x=>x.getState()==CardState.placed).ToList())
        {
            card.RefreshUnit();
        }
    }

    /// <summary>
    /// 回合开始时处理trait效果：动员buff、伏击重置、烟幕前线检查、被守护刷新
    /// </summary>
    private void ApplyTurnStartTraits()
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
        _ = ExecuteChangeLists();

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
    private void ApplyEnemyTurnStartTraits()
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

        _ = ExecuteChangeLists();
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
        if(card.GetIsFriend()== IsFriend.enemy && card.isHq == HQ.hq)
        {
            DarkenScreen();
            // 战役模式：胜利后返回世界地图
            if (BattleStateManager.IsCampaignMode)
            {
                _ = ReturnToWorldMapAfterVictory();
            }
        }
        cardInPlaces.Remove(card);
        card.Dead();
        RefreshAllBeGuardianedStatus(); // 单位离场后刷新被守护状态
    }

    /// <summary>
    /// 战役模式下敌方总部被摧毁后，标记区域已完成，延迟2.5秒返回世界地图
    /// </summary>
    private async System.Threading.Tasks.Task ReturnToWorldMapAfterVictory()
    {
        // 标记当前区域已完成，解锁下一区域
        BattleStateManager.MarkAreaCompleted(BattleStateManager.SelectedArea);

        // 显示战后卡牌奖励选择
        await PostBattleReward.Show(this, player1);

        await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
        BattleStateManager.IsCampaignMode = false;
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

    private int _discardZCounter = 50;

    /// <summary>
    /// 播放弃牌动画并在完成后移除（fire-and-forget，Z-index递增确保后弃置的在上方）
    /// </summary>
    public async Task CardDiscardAndRemove(cardBase_ card)
    {
        card.isDiscarding = true;
        card.ZIndex = _discardZCounter++;
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
        foreach (var card in cardInPlaces.Where(x=>x.getState()==CardState.placed).ToList())
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
    /// 检查卡牌是否匹配指定的TargetType
    /// </summary>
    private bool IsValidTarget(cardBase_ card, TargetType targetType)
    {
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

        // 剥离末尾的attribute元数据 [icon=xxx,description=yyy]
        int lastBracket = effectString.LastIndexOf('[');
        if (lastBracket >= 0 && effectString.EndsWith("]"))
        {
            effectString = effectString.Substring(0, lastBracket).TrimEnd();
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
                // 剥离末尾[icon=...]后检测&结尾标签
                int bracketIdx = part.IndexOf('[');
                string partForLabel = bracketIdx > 0 ? part.Substring(0, bracketIdx) : part;
                if (partForLabel.EndsWith("&"))
                {
                    string label = partForLabel.Substring(0, partForLabel.Length - 1);
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

                // 剥离末尾[icon=...]元数据后缀，避免污染精确匹配（如drawCard）
                int bracketIdx = ins.IndexOf('[');
                if (bracketIdx > 0)
                {
                    ins = ins.Substring(0, bracketIdx);
                    instruction = instruction.Substring(0, bracketIdx);
                }

                // 跳过标签定义（End& 需要被处理以支持 foreach 结构）
                if (instruction.EndsWith("&") && ins != "end&")
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

                // DiscardRandomly(i) - 随机弃置i张卡
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
                                player1.SetLastDrawnCards(addedCards);
                            }
                            else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                            {
                                player2.SetLastDrawnCards(addedCards);
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
                if (instruction.StartsWith("AddPoint", StringComparison.OrdinalIgnoreCase))
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
                    if (enemyHq != null && enemyHq.getState() == CardState.placed)
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

                    if (cardsToShow.Count > 0)
                    {
                        // 显示选择界面
                        var selectedCard = await ShowCardChoice(cardsToShow, false);
                        
                        // 将选中卡加入手牌
                        if (selectedCard != null && sourceCard?.GetIsFriend() == IsFriend.friend)
                        {
                            await player1.AddCardToHand(selectedCard);
                        }
                        else if (selectedCard != null && sourceCard?.GetIsFriend() == IsFriend.enemy)
                        {
                            await player2.AddCardToHand(selectedCard);
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
                    if (enemyHq != null && enemyHq.getState() == CardState.placed)
                    {
                        enemyTargets.Add(enemyHq);
                    }
                    targets = enemyTargets;
                }

                // GetAllFriendTargets() - 获得所有友方目标(目标=单位+总部)
                if (ins == "getallfriendtargets")
                {
                    var friendTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend).ToList();
                    if (myHq != null && myHq.getState() == CardState.placed)
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
        if (unit == null || unit.getState() != CardState.placed)
            return;

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
                unit.SetMyPlace(validPos);
                unit.MoveToPosition(validPos.GetGlobalPosition());
                unit.setState(CardState.placed);
                return;
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

        // 无法返回手牌或敌方单位，直接弃置
        unit.AddChange(ChangeType.DiscardCard,1);
    }

    /// <summary>
    /// 执行指令卡效果。效果结算在打出瞬间完成，弃牌动画独立播放不阻塞玩家操作。
    /// </summary>
    private async void ExecuteCommandAndDiscard(cardBase_ commandCard, List<cardBase_> targets, bool needRestoreColor = false)
    {
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
        CheckIfAnyUnitDiedAsync();
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
        // 获取所有在战场上的单位，包括总部
        List<cardBase_> results = new List<cardBase_>();
        
        // 添加所有在cardInPlaces中的单位
        results.AddRange(cardInPlaces.Where(x => x.getState() == CardState.placed).ToList());
        
        // 添加友方总部（如果存在且在战场上）
        if (myHq != null && myHq.getState() == CardState.placed && !results.Contains(myHq))
        {
            results.Add(myHq);
        }
        
        // 添加敌方总部（如果存在且在战场上）
        if (enemyHq != null && enemyHq.getState() == CardState.placed && !results.Contains(enemyHq))
        {
            results.Add(enemyHq);
        }

        var parts = selector.Split(".");

        foreach (var part in parts)
        {
            if (part == "allTargets")
            {
                // 已经是所有单位
                continue;
            }
            else if (part == "unit")
            {
                // 只保留非HQ单位
                results = results.Where(x => x.isHq == HQ.normalCard).ToList();
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
                // 只保留HQ
                results = results.Where(x => x.isHq == HQ.hq).ToList();
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

        if (condition == "source.isFriend" && sourceCard != null)
        {
            return sourceCard.GetIsFriend() == IsFriend.friend;
        }
        if (condition == "source.isEnemy" && sourceCard != null)
        {
            return sourceCard.GetIsFriend() == IsFriend.enemy;
        }

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
        if(point + i >= pointMaxMaxMax) {point = pointMaxMaxMax;}
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
                    card.SetAnchorsPreset(Godot.Control.LayoutPreset.TopLeft);
                    card.Size = new Godot.Vector2(180, 240);
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
            if (cardsInHand.Count >= maxHandSize)
            {
                battlefield.AddToBattleField(card);
                _ = battlefield.CardDiscardAndRemove(card);
                return;
            }
            card.SetPosition(new Godot.Vector2(-2000, 800));
            await AddCardToHand(card); 
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
                if (cardsInHand.Count >= maxHandSize)
                {
                    await card.DiscardCard();
                    battlefield.RemoveCard(card);
                }
                else
                {
                    card.SetPosition(new Godot.Vector2(-2000, 800));
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
                    if (cardsInHand.Count >= maxHandSize)
                    {
                        battlefield.AddToBattleField(card);
                        await card.DiscardCard();
                        battlefield.RemoveCard(card);
                    }
                    else
                    {
                        card.SetPosition(new Godot.Vector2(-2000, 800));
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
                if (cardsInHand.Count >= maxHandSize)
                {
                    battlefield.AddToBattleField(card);
                    await card.DiscardCard();
                    battlefield.RemoveCard(card);
                }
                else
                {
                    card.SetPosition(new Godot.Vector2(-2000, 800));
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