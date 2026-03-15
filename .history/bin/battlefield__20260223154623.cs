using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;
using System.Text;
using Godot;
using System;
using System.Collections.Generic;
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
    private List<cardBase_> cardInPlaces;

    

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
        cardInPlaces = value;
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
/// 给定一个若干张卡组成的列表 重新按照从上到下从左到右排序
/// </summary>
/// <param name="list"></param>
/// <returns></returns>
    List<cardBase_> SortCardList(List<cardBase_> list)
    {
        var sortedCards = list
            .OrderBy(list => list.GlobalPosition.Y)   // 先按 Y（行）
            .ThenBy(list => list.GlobalPosition.X)    // 再按 X（列）
            .ToList();
        return sortedCards;
    }


/// <summary>
/// 重新按照顺序显示卡
/// </summary> 
    void RefreshAllCardDisplayOrder()
    {
        cardInPlaces = SortCardList(cardInPlaces).AsEnumerable().Reverse().ToList();


        
        foreach (var card in player1.GetCardsInHand())
        {
            MoveChild(card,1);
            card.ZIndex = 20;
        }

        if(cardNowChoose!= null)
        {
            MoveChild(cardNowChoose,1);
            cardNowChoose.ZIndex = 15;
        }
        foreach (var card in cardInPlaces)
        {
            if (card!= cardNowChoose) MoveChild(card,1);
            card.ZIndex = 10;
            
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
        
        // 闪击特性：单位被加入战场时刷新
        if(card.HasTrait(UnitTraits.Blitz)) card.RefreshUnit();

                      // 触发被加入战场的效果
        await TriggerUnitEffects("BeingAddedToField", card, new List<cardBase_>(), checkOnlySourceCard: true);
        ResumeDeathCheck(); // 恢复死亡检查
        CheckIfAnyUnitDied(); // 检查死亡
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
            bullets.Add(GD.Load<PackedScene>("res://bin/bullet.tscn").Instantiate() as Bullet);
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
        cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        //初始化按钮
        buttonNextTurn           = GetNode<TextureButton>("NextTurnButton");

        //初始化卡组

         var INIpath = "res://cards/card.ini";

        if (!Godot.FileAccess.FileExists(INIpath))
        {
            GD.PushError($"INI file not found: {INIpath}");
            return;
        }

        var configFile = new IniFile();
        configFile.Load("cards\\card.ini");
        //_item 所有卡的数据组成的数组
        Dictionary<string, CardData> _items = [];

        foreach (var section in configFile)
        {
            var card = new CardData();
            {
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
            }
            _items[card.Id] = card;
        }

        cardMaganer.SetCardDictionary(_items);

        player1 = new Player(new Vector2(700,700), this,IsFriend.friend);
        player2 = new Player(new Vector2(700,0), this,IsFriend.enemy);


        //初始化hq
        myHq = cardMaganer.LoadHq(1);
        AddCardToPlace(myHq,supportLine[2]);


        enemyHq = cardMaganer.LoadHq(2);
        AddCardToPlace(enemyHq,enemySupprotLine[2]);
        
        // 加载敌方行动队列
        LoadEnemyActionQueue("berlin");

        //初始化敌人
        EnemyInit();
        
        GetNode<End>("end").Visible = false;

        player1.DrawCard(5);
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
            return outTrait;
        }
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

    public override void _Input(InputEvent @event)
    {
        if (ReadControlState() == 1) return;
         if (@event is InputEventMouseButton mouseButton)
        {
            var mousePosition = GetGlobalMousePosition();
            //选择卡
            if (mouseButton.Pressed)
            {
                
                // 点击事件
                
                var card = CheckCardClick(mousePosition);
                //手上的
                if (card != null && card.getState() == CardState.inHand)
                {
                    // 如果是 Command 卡，改为显示箭头并记录为已选择状态，但不移动卡牌
                    if (card.cardType == CardTypes.Command)
                    {
                        // 只有在点数足够时才允许选择并显示箭头
                        if (player1 != null && player1.HasPoint(card.ReadCost()))
                        {
                            cardNowChoose = card;
                            cardNowChoose.setState(CardState.commandCardCaught);
                            // 显示箭头起点
                            arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                            cardBase.ProcessMode     = Node.ProcessModeEnum.Inherit;
                            cardBase.Visible         = true;
                            HighlightValidTargets(cardNowChoose.targetType); // 根据 Command 卡的目标类型高亮合法目标
                        }
                        // 点数不足时不做任何反应
                    }
                    else
                    {
                        offset        = card.GetGlobalPosition() - mousePosition;
                        cardNowChoose = card;
                        card.setState(CardState.caught);
                    }
                }

                //场上的
                if (card != null && card.getState() == CardState.placed)
                {
                    // 检查是否是友方单位，如果是敌方单位则不允许拖动
                    if (card.GetIsFriend() != IsFriend.friend)
                    {
                        return;  // 敌方单位不能被拖动
                    }
                    // 检查是否是总部，总部不能被移动
                    if (card.isHq == HQ.hq)
                    {
                        return;  // 总部不能被移动
                    }
                    
                    cardNowChoose = card;
                    card.setState(CardState.inplaceAndCaught);

                    //箭头显示
                    arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                    cardBase.ProcessMode = Node.ProcessModeEnum.Inherit;
                    cardBase.Visible = true;
                    
                    // 高亮合法攻击目标（敌方单位）
                    HighlightValidAttackTargets(card);
                }
                //RefreshAllCardDisplayOrder();
                
                
            }
            //释放时 取消选择
            if (mouseButton.Pressed==false)
            {
                
                //如果现在选着卡
                if(cardNowChoose!= null )
                {
                    

                    //if (validArea.GetGlobalRect().HasPoint(mousePosition))

                    //松开时检查是否在任意一个合法的格子上
                    var result = GetPlaceWithPosition(mousePosition);

                    // 处理 TargetType.NOTarget 的 Command 卡：只要释放点在 validArea 内，直接以 null 目标执行效果
                    if (cardNowChoose.getState() == CardState.commandCardCaught &&
                        cardNowChoose.cardType == CardTypes.Command &&
                        cardNowChoose.targetType == TargetType.NOTarget &&
                        validArea.GetGlobalRect().HasPoint(mousePosition))
                    {
                        if (player1.UsePoint(cardNowChoose.ReadCost()))
                        {
                            _ = ParseAndExecuteEffect(cardNowChoose.effect, cardNowChoose, null);
                            player1.RemoveFromHand(cardNowChoose);
                            RemoveCard(cardNowChoose);
                            CheckIfAnyUnitDied(); // 结算单位变化
                        }
                        else
                        {
                            // 点数不足，什么也不执行
                        }

                        // 清理并返回，避免后续针对 result 的处理干扰
                        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                        cardBase.Visible     = false;
                        cardNowChoose.setState(CardState.inHand);
                        player1.RefreshMyHand();
                        cardNowChoose = null;
                        RestoreAllTargetsColor();
                        return;
                    }

                    if(result != null)
                    {
                            if(result.GetMyCard()== null)
                            {
                                if(cardNowChoose.getState() == CardState.caught  && player1.UsePoint(cardNowChoose.ReadCost()) == true)
                                {
                                    // 在手上并且有钱的话就移动过去
                                    _ = Move(cardNowChoose,result);
                                }
                                else if(cardNowChoose.getState() == CardState.inplaceAndCaught)
                                {
                                    // 在场上并且拖动到合法格子
                                    _ = Move(cardNowChoose,result);
                                }
                            }
                                else if((result.GetMyCard().GetIsFriend() == IsFriend.enemy ||result.GetMyCard().GetIsFriend() == IsFriend.enemyNeutral)&&cardNowChoose.getState()==CardState.inplaceAndCaught)
                            {
                                //攻击
                                Attack(cardNowChoose,result.GetMyCard());
                            }
                            else if(cardNowChoose.getState() == CardState.commandCardCaught)
                                {
                                    // 如果是 Command 卡，则不移动卡牌，改为生成箭头并记录释放所在格子
                                    if (cardNowChoose.cardType == CardTypes.Command)
                                    {
                                        // 检查玩家是否有足够的点数
                                        if (player1.UsePoint(cardNowChoose.ReadCost()))
                                        {
                                            // 检查目标是否合法
                                            bool isValidTarget = false;
                                            
                                            if (cardNowChoose.targetType == TargetType.NOTarget)
                                            {
                                                // NOTarget 无需指向任何目标，只要在 validArea 内就可以
                                                isValidTarget = validArea.GetGlobalRect().HasPoint(mousePosition);
                                            }
                                            else
                                            {
                                                // 其他目标类型需要检查目标卡是否合法
                                                cardBase_ targetCard = result?.GetMyCard();
                                                isValidTarget = (targetCard != null && IsValidTarget(targetCard, cardNowChoose.targetType)) ||
                                                                (targetCard == null && cardNowChoose.targetType == TargetType.aPlace);
                                            }
                                            
                                            if (isValidTarget)
                                            {
                                                // 目标合法，执行效果
                                                cardBase_ targetCard = result?.GetMyCard();                                                       // 获取目标格子上的卡
                                                          _          = ParseAndExecuteEffect(cardNowChoose.effect, cardNowChoose, [targetCard]);
                                                
                                                // 从手牌中移除这张卡
                                                player1.RemoveFromHand(cardNowChoose);
                                                    // 彻底删除这张卡（会调用Dead()）
                                                RemoveCard(cardNowChoose);
                                                CheckIfAnyUnitDied(); // 结算单位变化
                                                
                                                // 隐藏箭头
                                                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                                cardBase.Visible     = false;
                                                cardNowChoose.setState(CardState.inHand);
                                            }
                                            else
                                            {
                                                // 目标不合法，返还点数并恢复状态
                                                player1.RestorePoint(cardNowChoose.ReadCost());
                                                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                                cardBase.Visible     = false;
                                                cardNowChoose.setState(CardState.inHand);
                                            }
                                        }
                                        else
                                        {
                                            // 点数不足，隐藏箭头并回到手牌状态
                                            cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                            cardBase.Visible     = false;
                                            cardNowChoose.setState(CardState.inHand);
                                        }
                                    }

                                    
                                    
                                }
                            
                            
                        }
                    // 如果没有命中任何 place（result == null），允许对 TargetType.NOTarget 的 Command 卡在 validArea 内释放生效
                    if (result == null && cardNowChoose.getState() == CardState.commandCardCaught)
                    {
                        if (cardNowChoose.cardType == CardTypes.Command)
                        {
                            // 仅当卡的目标类型为 NOTarget 且鼠标在 validArea 内才允许
                            if (cardNowChoose.targetType == TargetType.NOTarget && validArea.GetGlobalRect().HasPoint(mousePosition))
                            {
                                if (player1.UsePoint(cardNowChoose.ReadCost()))
                                {
                                    // 执行效果（无具体目标）
                                    _ = ParseAndExecuteEffect(cardNowChoose.effect, cardNowChoose, null);

                                    // 从手牌中移除并删除该卡
                                    player1.RemoveFromHand(cardNowChoose);
                                    RemoveCard(cardNowChoose);

                                    // 隐藏箭头并恢复状态
                                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                    cardBase.Visible     = false;
                                    cardNowChoose.setState(CardState.inHand);
                                }
                                else
                                {
                                    // 点数不足，隐藏箭头并回到手牌状态
                                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                    cardBase.Visible     = false;
                                    cardNowChoose.setState(CardState.inHand);
                                }
                            }
                            else
                            {
                                // 非法释放，直接回到手牌状态（没有扣点）
                                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                cardBase.Visible     = false;
                                cardNowChoose.setState(CardState.inHand);
                            }
                        }
                    }

                    //否则回到起点
                    if (cardNowChoose.getState() == CardState.caught)
                    {
                        cardNowChoose.setState(CardState.inHand);
                    }
                    else if (cardNowChoose.getState() == CardState.inplaceAndCaught)
                    {
                        // 如果是 Command 卡，释放后依然保持在手牌状态（不移动）
                        if (cardNowChoose.cardType == CardTypes.Command)
                        {
                            cardNowChoose.setState(CardState.inHand);
                        }
                        else
                        {
                            cardNowChoose.setState(CardState.placed);
                        }
                    }
                    else if (cardNowChoose.getState() == CardState.commandCardCaught)
                    {
                        // Command 卡如果没有有效释放，恢复为 inHand 状态
                        cardNowChoose.setState(CardState.inHand);
                    }

                    //不管怎样都刷新手牌区
                    player1.RefreshMyHand();
                    cardNowChoose = null;
                    //隐藏箭头

                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                    cardBase.Visible     = false;
                    
                    RestoreAllTargetsColor(); // 取消高亮，恢复所有单位的原始颜色
                    CheckIfAnyUnitDied();
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        RefreshAllCardDisplayOrder();
        //跟踪卡牌
        if (Input.IsMouseButtonPressed(MouseButton.Left) && cardNowChoose != null && cardNowChoose.getState() == CardState.caught)
        {
            cardNowChoose.SetGlobalPosition(GetGlobalMousePosition()+offset);
        }
    }

    /// <summary>
    /// 替换所有 & 开头的变量为其实际值
    /// </summary>
    private string ReplaceVariables(string effectString, int result)
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
                    // 可以在这里添加更多变量的支持
                    default:
                        // 未知变量，保持原样
                        sb.Append('&').Append(varName);
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

    /// <summary>
    /// 分割效果字符串，忽略括号内的逗号
    /// </summary>
    private string[] SplitEffectString(string effectString)
    {
        List<string> segments = new List<string>();
        StringBuilder currentSegment = new StringBuilder();
        int parenCount = 0;

        foreach (char c in effectString)
        {
            if (c == '(')
            {
                parenCount++;
                currentSegment.Append(c);
            }
            else if (c == ')')
            {
                parenCount--;
                currentSegment.Append(c);
            }
            else if (c == ',' && parenCount == 0)
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

            var prefix = effectString.Split(":")[0].Trim();
            if (prefix != triggerPoint) continue;

            // 移除时间前缀，获取实际效果
            var actualEffect = effectString.Substring(effectString.IndexOf(":") + 1);

            // 触发此单位的效果（unit 作为执行效果的单位，targetCards 作为目标列表）
            await ParseAndExecuteEffect(actualEffect, unit, targetCards);
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
        
        // 烟幕特性：单位第一次攻击时失去烟幕
        if (from.HasSmokeScreenActive())
        {
            from.RemoveSmokeScreen();
        }

        // 冲击特性：攻击时不受到反击
        bool attackerHasShock = from.HasShockActive();
        if (attackerHasShock)
        {
            from.RemoveShock(); // 攻击后失去冲击
        }
        
          // 判断是否进行反击
          // 战斗机、步兵、坦克、火炮可以反击敌人
          // 轰炸机不能反击敌人
        bool canCounterAttack = to.cardType != CardTypes.Bomber;
        
        // 战斗机、步兵、坦克在攻击后会受到反击
        // 轰炸机、火炮不会受到反击
        bool willReceiveCounterAttack = 
            from.cardType == CardTypes.Plane || 
            from.cardType == CardTypes.Infantry || 
            from.cardType == CardTypes.Tank;
        
        // 伏击特性：被攻击时先造成反击伤害
        bool defenderHasAmbush = to.HasTrait(UnitTraits.Ambush);

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
            
            // 伏击特性：先造成反击伤害
            if (defenderHasAmbush)
            {
                await from.LoseDefence(counterDamage);
                // 若敌方单位因此死亡，则不受到来自对方的伤害
                if (from.ReadDefence() <= 0)
                {
                    // 不执行后续的正常反击
                    counterDamage = 0;
                }
            }
            else
            {
                await from.LoseDefence(counterDamage);
            }
        }
        
        PlayBattleSound(1);
        await FlyBullets(from,to);

        // 标记单位已经攻击，减少可攻击次数
        from.HaveAttacked();

        // 步兵、火炮、战斗机和轰炸机攻击后不能移动
        if (from.cardType == CardTypes.Infantry || from.cardType == CardTypes.Artillery || 
            from.cardType == CardTypes.Plane || from.cardType == CardTypes.Bomber)
        {
            from.HaveMoved();
        }

        ResumeDeathCheck(); // 恢复死亡检查
        CheckIfAnyUnitDied(); // 统一检查死亡
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
            // 否则触发移动单位的 Moving 效果
            await TriggerUnitEffects("Moving", card, new List<cardBase_> { card }, checkOnlySourceCard: true);
        }
        ResumeDeathCheck(); // 恢复死亡检查
        CheckIfAnyUnitDied(); // 统一检查死亡
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


    List<cardBase_> enemyDeck;

    /// <summary>
    /// 创建敌人池子 目前只是随机生成30张卡 之后会在这里写敌人卡组构建
    /// </summary>
    void EnemyInit()
    {
        enemyDeck = new List<cardBase_>();
        PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
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
        CheckIfAnyUnitDied(); // 检查死亡
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
            foreach (var action in enemyActionQueue[currentTurnKey])
            {
                await ExecuteEnemyAction(action);
                hasAction = true;
            }
        }
        
        // 如果当前回合没有行动，执行default行动
        if (!hasAction && enemyActionQueue.ContainsKey("default"))
        {
            foreach (var action in enemyActionQueue["default"])
            {
                await ExecuteEnemyAction(action);
            }
        }
        
        // 执行everyXXt格式的行动（如every5t、every10t等）
        foreach (var key in enemyActionQueue.Keys)
        {
            if (key.StartsWith("every") && key.EndsWith("t"))
            {
                // 提取数字部分
                string numberStr = key.Substring(5, key.Length - 6); // "every"长度为5，"t"长度为1
                if (int.TryParse(numberStr, out int interval))
                {
                    if (interval > 0 && turn % interval == 0)
                    {
                        foreach (var action in enemyActionQueue[key])
                        {
                            await ExecuteEnemyAction(action);
                        }
                    }
                }
            }
        }
        
        // 执行ADD行动（每回合都执行）
        if (enemyActionQueue.ContainsKey("ADD"))
        {
            foreach (var action in enemyActionQueue["ADD"])
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
    void CheckIfAnyUnitDied()
    {
        // 如果死亡检查被暂停，则不执行
        if (pauseDeathCheck == 1)
        {
            return;
        }

        foreach(var c in ReadCardInPlaces())
        {
            c.ExecChangeList();
        }

        var units = ReadCardInPlaces().Where(x=>x.getState()==CardState.placed).ToList();
        List<cardBase_> deadUnits = new List<cardBase_>();
        
        // 第一遍：收集所有死亡的单位
        for (int i = units.Count - 1; i >= 0; i--)
        {
            if (units[i].ReadDefence()<=0)
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
        if(deadUnits.Count>0)             CheckIfAnyUnitDied(); // 递归检查是否有新的死亡单位
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
        CheckIfAnyUnitDied(); // 检查死亡
        
        
        // 触发敌方回合开始时点
        await TriggerUnitEffects("EnemyTurnBegin", null);
        
        // 触发双方回合开始时点
        await TriggerUnitEffects("TurnBegin", null);
        
        // 触发友方回合开始时点
        await TriggerUnitEffects("FriendlyTurnBegin", null);
        CheckIfAnyUnitDied(); // 检查死亡

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

    public void RemoveCard(cardBase_ card)
    {
        if(card.GetIsFriend()== IsFriend.enemy && card.isHq == HQ.hq)
        {
            DarkenScreen();
        }
        cardInPlaces.Remove(card);
        card.Dead();
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
        player1.AddCardToHand(cardMaganer.GetCard("喀秋莎"));
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

        // 移除时间前缀 如 "deployed:"
        if (effectString.Contains(":"))
        {
            effectString = effectString.Substring(effectString.IndexOf(":") + 1);
        }

        

        // 首先用逗号分割 逗号分割优先级更高，但要忽略括号内的逗号
        var effectSegments = SplitEffectString(effectString);

        foreach (var segment in effectSegments)
        {
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

            // 对每个逗号分割的效果段 再用 "|" 分割多个指令
            var parts = segment.Split("|");
            
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

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                string instruction = part.Trim();
                
                // 跳过标签定义
                if (instruction.EndsWith("&"))
                {
                    continue;
                }

                // 替换所有 & 开头的变量为其实际值
                instruction = ReplaceVariables(part, result);
                
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
                if (instruction == "this")
                {
                    if (sourceCard != null)
                    {
                        targets = new List<cardBase_> { sourceCard };
                    }
                }
                // target - 指代传入的目标列表
                else if (instruction == "target")
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
                else if (instruction.StartsWith("GetTargetByIndex"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                    {
                        if (index >= 0 && index < targets.Count)
                        {
                            targets = new List<cardBase_> { targets[index] };
                        }
                    }
                }

                if(instruction == "myHq")
                {
                    targets = [myHq];
                }
                else if(instruction == "enemyHq")
                {
                    targets = [enemyHq];
                }

                // Heal(n) - 使用AddChange增加防御力
                if (instruction.StartsWith("Heal", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int healAmount))
                    {
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.GetDefence, healAmount);
                                
                            }
                        }
                    }
                }

                if (instruction.StartsWith("GetAttack"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int GetAttackAmount))
                    {
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.GetAttack, GetAttackAmount);
                                
                            }
                        }
                    }
                }

                if (instruction.StartsWith("SetDefence"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int GetAttackAmount))
                    {
                        foreach (var target in targets)
                        {
                            if (target != null)
                            {
                                target.AddChange(ChangeType.SetDefence, GetAttackAmount);
                                
                            }
                        }
                    }
                }

                // damage(n) - 使用AddChange减少防御力
                if (instruction.StartsWith("damage"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int damageAmount))
                    {
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
                if (instruction == "drawCard")
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
                if (instruction.StartsWith("DrawACard"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction,  @"\((.*),(\d+)\)");
                    if (match.Success)
                    {
                        string namePattern = match.Groups[1].Value;
                        int count = int.Parse(match.Groups[2].Value);

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
                if (instruction.StartsWith("DrawACardWithType"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction,  @"\((.*),(\d+)\)");
                    if (match.Success)
                    {
                        string unitType = match.Groups[1].Value;
                        int count = int.Parse(match.Groups[2].Value);

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

                // DiscardRandomly(i) - 随机弃置i张卡
                if (instruction.StartsWith("DiscardRandomly"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
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
                if (instruction == "GetCardsBeingTreated")
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

                // AddToHand(string) - 将名字为s的卡加入手牌
                if (instruction.StartsWith("AddToHand"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((.*)\)");
                    if (match.Success)
                    {
                        string cardName = match.Groups[1].Value;
                        var cardData = GetCardMaganer().GetCard(cardName);
                        if (cardData != null)
                        {
                            PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
                            var card = cardRes.Instantiate() as cardBase_;
                            card.SetCardInformation(cardData);
                            card.SetIsFriend(sourceCard?.GetIsFriend() ?? IsFriend.friend);

                            if (sourceCard?.GetIsFriend() == IsFriend.friend)
                            {
                                await player1.AddCardToHand(card);
                                player1.SetLastDrawnCards([card]);
                            }
                            else if (sourceCard?.GetIsFriend() == IsFriend.enemy)
                            {
                                await player2.AddCardToHand(card);
                                player2.SetLastDrawnCards([card]);
                            }
                        }
                    }
                }

                // DiscardWithName(string,i) - 弃置i张名字含s的卡(若可能)
                if (instruction.StartsWith("DiscardWithName"))
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
                if (instruction.StartsWith("setResult"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
                    {
                        result = value;
                    }
                }

                // getCount(${selector}) - 获取符合条件的单位数量
                if (instruction.StartsWith("getCount"))
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
                if (instruction == "setTarget")
                {
                    if (targetCard != null)
                    {
                        targets = [targetCard];
                    }
                }

                // addToSupportLine(cardId) - 添加卡到支援阵线
                if (instruction.StartsWith("addToSupportLine"))
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
                            }
                        }
                    }
                }

                // addToEnemySupportLine(cardId) - 添加卡到敌方支援阵线
                if (instruction.StartsWith("addToEnemySupportLine"))
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
                            }
                        }
                    }
                }

                // addToDeck(cardId) - 将指定卡牌洗入卡组
                if (instruction.StartsWith("addToDeck"))
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
                if (instruction == "GetPoint")
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
                if (instruction == "GetPointMax")
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
                if (instruction.StartsWith("AddPointMax"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
                    {
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
                if (instruction.StartsWith("AddPoint"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
                    {
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
                if (instruction == "GetRandomFriendUnit")
                {
                    var friendUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend && x.isHq != HQ.hq).ToList();
                    if (friendUnits.Count > 0)
                    {
                        var rnd = new Random();
                        targets = new List<cardBase_> { friendUnits[rnd.Next(friendUnits.Count)] };
                    }
                }

                // GetRandomEnemyUnit() - 随机获得一个敌方单位
                if (instruction == "GetRandomEnemyUnit")
                {
                    var enemyUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy && x.isHq != HQ.hq).ToList();
                    if (enemyUnits.Count > 0)
                    {
                        var rnd = new Random();
                        targets = new List<cardBase_> { enemyUnits[rnd.Next(enemyUnits.Count)] };
                    }
                }

                // GetRandomFriendTarget() - 随机获得一个友方目标(目标=单位+总部)
                if (instruction == "GetRandomFriendTarget")
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
                }

                // GetRandomEnemyTarget() - 随机获得一个敌方目标(目标=单位+总部)
                if (instruction == "GetRandomEnemyTarget")
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
                }

                // GetRandomNumber() - 随机获得一个数字(包括两个参数,min,max)
                if (instruction.StartsWith("GetRandomNumber"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(instruction, @"\((\d+),(\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int min) && int.TryParse(match.Groups[2].Value, out int max))
                    {
                        var rnd = new Random();
                        result = rnd.Next(min, max + 1);
                    }
                }

                // KillAllTargets() - 消灭列表上的所有单位
                if (instruction == "KillAllTargets")
                {
                    foreach (var target in targets)
                    {
                        if (target != null && target.getState() == CardState.placed)
                        {
                            target.LoseDefence(target.ReadDefence());
                        }
                    }
                }

                // HealAllTargets() - 完全修复单位，将列表上所有单位的防御力设置为历史最大值
                if (instruction == "HealAllTargets")
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
                if (instruction == "GetAllEnemyUnits")
                {
                    var enemyUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
                    targets = enemyUnits;
                }

                // GetAllFriendUnits() - 获得所有友方单位
                if (instruction == "GetAllFriendUnits")
                {
                    var friendUnits = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend).ToList();
                    targets = friendUnits;
                }

                // GetAllEnemyTargets() - 获得所有敌方目标(目标=单位+总部)
                if (instruction == "GetAllEnemyTargets")
                {
                    var enemyTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.enemy).ToList();
                    targets = enemyTargets;
                }

                // GetAllFriendTargets() - 获得所有友方目标(目标=单位+总部)
                if (instruction == "GetAllFriendTargets")
                {
                    var friendTargets = ReadCardInPlaces().Where(x => x.getState() == CardState.placed && x.GetIsFriend() == IsFriend.friend).ToList();
                    targets = friendTargets;
                }

                // GetEnemyHq() - 获得敌方总部
                if (instruction == "GetEnemyHq")
                {
                    if (enemyHq != null && enemyHq.getState() == CardState.placed)
                    {
                        targets = new List<cardBase_> { enemyHq };
                    }
                }

                // GetFriendHq() - 获得友方总部
                if (instruction == "GetFriendHq")
                {
                    if (myHq != null && myHq.getState() == CardState.placed)
                    {
                        targets = new List<cardBase_> { myHq };
                    }
                }
            }
        }

    }

    /// <summary>
    /// 根据选择器获取目标列表
    /// 格式: allTargets.unit.friend 或 allTargets.enemy 等
    /// </summary>
    private List<cardBase_> GetTargetsFromSelector(string selector)
    {
        List<cardBase_> results = cardInPlaces.Where(x => x.getState() == CardState.placed).ToList();

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

        // 处理 result 相关条件
        if (condition.StartsWith("result"))
        {
            if (condition.StartsWith("result>"))
            {
                if (int.TryParse(condition.Substring(7), out int value))
                {
                    return result > value;
                }
            }
            else if (condition.StartsWith("result>="))
            {
                if (int.TryParse(condition.Substring(8), out int value))
                {
                    return result >= value;
                }
            }
            else if (condition.StartsWith("result<"))
            {
                if (int.TryParse(condition.Substring(7), out int value))
                {
                    return result < value;
                }
            }
            else if (condition.StartsWith("result<="))
            {
                if (int.TryParse(condition.Substring(8), out int value))
                {
                    return result <= value;
                }
            }
            else if (condition.StartsWith("result=="))
            {
                if (int.TryParse(condition.Substring(8), out int value))
                {
                    return result == value;
                }
            }
            else if (condition.StartsWith("result!="))
            {
                if (int.TryParse(condition.Substring(8), out int value))
                {
                    return result != value;
                }
            }
        }

        // 处理 targets.count 相关条件
        if (condition.StartsWith("targets.count"))
        {
            if (condition.StartsWith("targets.count>"))
            {
                if (int.TryParse(condition.Substring(13), out int value))
                {
                    return targets.Count > value;
                }
            }
            else if (condition.StartsWith("targets.count>="))
            {
                if (int.TryParse(condition.Substring(14), out int value))
                {
                    return targets.Count >= value;
                }
            }
            else if (condition.StartsWith("targets.count<"))
            {
                if (int.TryParse(condition.Substring(13), out int value))
                {
                    return targets.Count < value;
                }
            }
            else if (condition.StartsWith("targets.count<="))
            {
                if (int.TryParse(condition.Substring(14), out int value))
                {
                    return targets.Count <= value;
                }
            }
            else if (condition.StartsWith("targets.count=="))
            {
                if (int.TryParse(condition.Substring(14), out int value))
                {
                    return targets.Count == value;
                }
            }
            else if (condition.StartsWith("targets.count!="))
            {
                if (int.TryParse(condition.Substring(14), out int value))
                {
                    return targets.Count != value;
                }
            }
        }

        // 处理 target.isFriend 和 target.isEnemy 条件
        if (condition == "target.isFriend" && targets.Count > 0)
        {
            return targets[0].GetIsFriend() == IsFriend.friend;
        }
        if (condition == "target.isEnemy" && targets.Count > 0)
        {
            return targets[0].GetIsFriend() == IsFriend.enemy;
        }
        
        // 处理 target 相关的条件（类型、费用、攻击力、防御力）
        if (targets.Count > 0 && condition.StartsWith("target."))
        {
            var target = targets[0];
            
            // 处理 target.type == 类型名
            if (condition.StartsWith("target.cardType=="))
            {
                string type = condition.Substring(17).Trim();
                return target.cardType.ToString() == type;
            }

              // 处理 target.type != 类型名
            if (condition.StartsWith("target.cardType!="))
            {
                string type = condition.Substring(17).Trim();
                return target.cardType.ToString() != type;
            }
            
            // 处理 target.cost > n, target.cost >= n, target.cost < n, target.cost <= n, target.cost == n, target.cost != n
            if (condition.StartsWith("target.cost>"))
            {
                if (int.TryParse(condition.Substring(12), out int value))
                {
                    return target.cost > value;
                }
            }
            else if (condition.StartsWith("target.cost>="))
            {
                if (int.TryParse(condition.Substring(13), out int value))
                {
                    return target.cost >= value;
                }
            }
            else if (condition.StartsWith("target.cost<"))
            {
                if (int.TryParse(condition.Substring(12), out int value))
                {
                    return target.cost < value;
                }
            }
            else if (condition.StartsWith("target.cost<="))
            {
                if (int.TryParse(condition.Substring(13), out int value))
                {
                    return target.cost <= value;
                }
            }
            else if (condition.StartsWith("target.cost=="))
            {
                if (int.TryParse(condition.Substring(13), out int value))
                {
                    return target.cost == value;
                }
            }
            else if (condition.StartsWith("target.cost!="))
            {
                if (int.TryParse(condition.Substring(13), out int value))
                {
                    return target.cost != value;
                }
            }
            
            // 处理 target.attack > n, target.attack >= n, target.attack < n, target.attack <= n, target.attack == n, target.attack != n
            if (condition.StartsWith("target.attack>"))
            {
                if (int.TryParse(condition.Substring(14), out int value))
                {
                    return target.attack > value;
                }
            }
            else if (condition.StartsWith("target.attack>="))
            {
                if (int.TryParse(condition.Substring(15), out int value))
                {
                    return target.attack >= value;
                }
            }
            else if (condition.StartsWith("target.attack<"))
            {
                if (int.TryParse(condition.Substring(14), out int value))
                {
                    return target.attack < value;
                }
            }
            else if (condition.StartsWith("target.attack<="))
            {
                if (int.TryParse(condition.Substring(15), out int value))
                {
                    return target.attack <= value;
                }
            }
            else if (condition.StartsWith("target.attack=="))
            {
                if (int.TryParse(condition.Substring(15), out int value))
                {
                    return target.attack == value;
                }
            }
            else if (condition.StartsWith("target.attack!="))
            {
                if (int.TryParse(condition.Substring(15), out int value))
                {
                    return target.attack != value;
                }
            }
            
            // 处理 target.defence > n, target.defence >= n, target.defence < n, target.defence <= n, target.defence == n, target.defence != n
            if (condition.StartsWith("target.defence>"))
            {
                if (int.TryParse(condition.Substring(16), out int value))
                {
                    return target.defence > value;
                }
            }
            else if (condition.StartsWith("target.defence>="))
            {
                if (int.TryParse(condition.Substring(17), out int value))
                {
                    return target.defence >= value;
                }
            }
            else if (condition.StartsWith("target.defence<"))
            {
                if (int.TryParse(condition.Substring(16), out int value))
                {
                    return target.defence < value;
                }
            }
            else if (condition.StartsWith("target.defence<="))
            {
                if (int.TryParse(condition.Substring(17), out int value))
                {
                    return target.defence <= value;
                }
            }
            else if (condition.StartsWith("target.defence=="))
            {
                if (int.TryParse(condition.Substring(17), out int value))
                {
                    return target.defence == value;
                }
            }
            else if (condition.StartsWith("target.defence!="))
            {
                if (int.TryParse(condition.Substring(17), out int value))
                {
                    return target.defence != value;
                }
            }
        }

        // 处理 source.isFriend 和 source.isEnemy 条件
        if (condition == "source.isFriend" && sourceCard != null)
        {
            return sourceCard.GetIsFriend() == IsFriend.friend;
        }
        if (condition == "source.isEnemy" && sourceCard != null)
        {
            return sourceCard.GetIsFriend() == IsFriend.enemy;
        }

        return false;
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
    List<cardBase_> cardsInHand=[];
    int maxHandSize = 9;
    IsFriend isFriend;
    Label pointLabel;
    Label pointMaxLabel;

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
        if(point + i >= pointMaxMaxMax) {point = pointMaxMaxMax;}
        else point += i;
        
    }

    public void AddPointMax(int i = 1)
    {
        if(pointMax + i >= pointMaxMaxMax) {pointMax = pointMaxMaxMax;}
        else pointMax += i;
        RefreshPointLabel();
    }
    
    public Boolean UsePoint(int x)
    {
        if(point >= x)
        {
            point -= x;
            RefreshPointLabel();
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
        point += x;
        if (point > pointMax) point = pointMax;
        RefreshPointLabel();
    }

    void RefreshPointLabel()
    {
        pointLabel.Text = $"{point}";
        pointMaxLabel.Text = $"{pointMax}";
    }
    
    public void RefreshPoint()
    {
        point = pointMax;
        RefreshPointLabel();
    }


    public void AddPointMaxNatural()
    {
        if (pointMax + 1 <= pointMaxMax)
        {
            pointMax += 1;
        }
        RefreshPoint();
        
    }

    public List<cardBase_> GetCardsInHand()
    {
        return cardsInHand;
    }

    /// <summary>
    /// 将卡牌添加到手牌
    /// </summary>
    /// <param name="card"></param>
    /// <returns></returns>
    public async Task AddCardToHand(cardBase_ card)
    {
        cardsInHand.Add(card);
        battlefield.AddToBattleField(card);
        card.setState(CardState.inHand);
        await RefreshMyHand();
    }

    public async Task AddCardToHand(CardData card)
    {
        PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        var _card = cardRes.Instantiate() as cardBase_;
        _card.SetCardInformation(card);
        cardsInHand.Add(_card);
        battlefield.AddToBattleField(_card);
        _card.setState(CardState.inHand);
        await RefreshMyHand();
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
        deck = new List<cardBase_>();
        for(int i = 0; i < 30; i++)
        {        
            var card = cardRes.Instantiate() as cardBase_;
            var cardResources = battlefield.GetCardMaganer().GetRandomCard();
            if(cardResources.Rarity == Rarity.Unobtainable)
            {
                 i--;
                 continue;
            }
            card.SetCardInformation(cardResources);
            card.SetIsFriend(isFriend);
            deck.Add(card);
        }
        pointLabel = battlefield.GetNode<Label>("point");
        pointMaxLabel = battlefield.GetNode<Label>("pointMax");
        RefreshPointLabel();  // 初始化点数显示
    }

/// <summary>
/// 重新排列所有手牌 每次改变自己的手牌的时候都要刷新
/// </summary>
/// <returns></returns>
    public async Task RefreshMyHand()
    {
        
        float left = initPos.X - (cardsInHand.Count - 1) * 80;
        for (int i = 0; i < cardsInHand.Count; i++)
        {
            _ = cardsInHand[i].MoveToPosition(new Vector2(left + i * 160, initPos.Y), 0.3f);
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
                await card.DiscardCard();
                battlefield.RemoveCard(card);
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
            await Task.Delay(500);
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



}


/// <summary>
/// 管理所有卡牌 用于加载卡
/// </summary>
public class CardMaganer
{
    private Dictionary<string, CardData> _items = [];
    private Random random = new Random();

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

}