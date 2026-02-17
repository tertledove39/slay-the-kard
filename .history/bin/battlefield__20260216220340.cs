using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;



public partial class battlefield_ : Control
{




/// <summary>
/// 是否允许控制输入 0 允许 1 禁止
/// </summary> 
    int allowControl = 0;

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
        if (!cardInPlaces.Contains(card))
        {
            AddToBattleField(card);
        }
        card.SetMyPlace(place);
        card.setState(CardState.placed);
        //RefreshAllCardDisplayOrder();
        await card.MoveToPosition(place.GetNode<Control>("Control").GlobalPosition);
        
        
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

        //初始化敌人
        EnemyInit();
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
        var rnd = new Random();
        foreach(var bullet in bullets)
        {
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
                        cardNowChoose = card;
                        cardNowChoose.setState(CardState.commandCardCaught);
                        // 显示箭头起点
                        arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                        cardBase.ProcessMode     = Node.ProcessModeEnum.Inherit;
                        cardBase.Visible         = true;
                        HighlightValidTargets(cardNowChoose.targetType); // 根据 Command 卡的目标类型高亮合法目标
                        
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
                    
                    cardNowChoose = card;
                    card.setState(CardState.inplaceAndCaught);

                    //箭头显示
                    arrowFrom.GlobalPosition = card.GetGlobalPosition() + arrowOffset;
                    cardBase.ProcessMode = Node.ProcessModeEnum.Inherit;
                    cardBase.Visible = true;
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
                    {
                        var result = GetPlaceWithPosition(mousePosition);
                        if(result != null)
                        {
                            if(result.GetMyCard()== null)
                            {
                                if(cardNowChoose.getState() == CardState.caught  && player1.UsePoint(cardNowChoose.ReadCost()) == true)
                                {
                                    // 在手上并且有钱的话就移动过去
                                    Move(cardNowChoose,result);
                                }
                                else if(cardNowChoose.getState() == CardState.inplaceAndCaught)
                                {
                                    // 在场上并且拖动到合法格子
                                    Move(cardNowChoose,result);
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
                                        // 从箭头起点到目标格中心绘制箭头
                                        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                                        cardBase.Visible     = false;
                                          // 这里可以触发命令卡的效果或记录 result（格子）供后续使用
                                        //GD.Print($"Command card released on place: {result.Name}");
                                        cardNowChoose.setState(CardState.inHand);
                                    }
                                    
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

                    //不管怎样都刷新手牌区
                    player1.RefreshMyHand();
                    cardNowChoose = null;
                    //隐藏箭头

                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                    cardBase.Visible     = false;
                    
                    RestoreAllTargetsColor(); // 取消高亮，恢复所有单位的原始颜色
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
    /// 两军交战 注意攻击本身不会导致单位死亡 检查函数才会导致单位死亡
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param> 
    public async Task Attack(cardBase_ from,cardBase_ to)
    {
        ForbidControl();
        if (!from.CheckIfCanAttack())
        {
            AllowControl();
            return;
        }
        from.LoseDefence(to.ReadAttack());
        to.LoseDefence(from.ReadAttack());
        PlayBattleSound(1);
        await FlyBullets(from,to);
        CheckIfAnyUnitDied();
        AllowControl();
    }

    /// <summary>
    /// 将一张卡牌从一个位置移动到另一个位置 注意这个函数检查合法性 必须是手牌到支援阵线 支援阵线到前线
    /// </summary>
    /// <param name="card"></param>
    /// <param name="position"></param>
    void Move(cardBase_ card, place_ position)
    {
        if (card == null || position == null)
        {
            return;
        }

        if(position.GetMyCard()!= null)
        {
            return;
        }
        if(card.GetIsFriend() == IsFriend.friend)
        {
            if (supportLine.Contains(card.GetMyPlace()))
            {
                if(!frontLine.Contains(position) || CheckIfFrontLineIsFriend()== IsFriend.enemy||card.CheckIfCanMove() == false)
                {
                    return;
                }
            }
            else if(frontLine.Contains(card.GetMyPlace()))
            {
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
                    return;
                }
            }
            else if(frontLine.Contains(card.GetMyPlace()))
            {
                return;
            }
        }
        
        card.SetMyPlace(position);
        card.MoveToPosition(position.GetGlobalPosition());
        card.setState(CardState.placed);
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
        ForbidControl();
        RefreshAllCardInField();
        enemyHq.GetDefence(5);
        turn++;
        switch(turn)
        {
            case 1: 
                await enemySummonAsync("t70");
                break;
            case 2:
                break;
            case 3: 
            await enemySummonAsync("t70");
                break;
            default: 
            await enemySummonAsync("t70");
                break;
        }
        AllowControl();
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
        var units = ReadCardInPlaces().Where(x=>x.getState()==CardState.placed).ToList();
        for (int i = units.Count - 1; i >= 0; i--)
            {
                units[i].ExecChangeList();
                if (units[i].ReadDefence()==0)
                {
                    RemoveCard(units[i]);
                    PlayDeadSound(1);
                }
            }
    }


            /// <summary>
            /// 暂时用作测试
            /// </summary>
    public void OnNextTurnButtonPressed()
    {
        EnemyTurnAsync();
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
        cardInPlaces.Remove(card);
        card.Dead();
    }

    /// <summary>
    /// 根据指定的TargetType高亮符合条件的单位，其他单位变成灰色
    /// </summary>
    public void HighlightValidTargets(TargetType targetType)
    {
        foreach (var card in cardInPlaces)
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

    int      point              = 0;
    int      pointMax           = 1;
    readonly int pointMaxMax    = 12;
    readonly int pointMaxMaxMax = 24;
    
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

    public void AddPointMax(int x)
    {
        if (pointMax + x <= pointMaxMaxMax)
        {
            pointMax += x;
        }
        else
        {
            pointMax = pointMaxMaxMax;
        }
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
            
            card.SetCardInformation(battlefield.GetCardMaganer().GetRandomCard());
            card.SetIsFriend(isFriend);
            deck.Add(card);
        }
        pointLabel = battlefield.GetNode<Label>("point");
        pointMaxLabel = battlefield.GetNode<Label>("pointMax");
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
            card.SetCardInformation(GetCard("moscow"));

        if(isFriend == 1)
        {
            card.SetIsFriend(IsFriend.friend);
        }
        else
        {
            card.SetIsFriend(IsFriend.enemy);
        }
        return card;
        
    }

}