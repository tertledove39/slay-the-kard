using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 卡牌本身类 只用于存储卡牌信息和播放动画
/// </summary>
public partial class cardBase_ : Control
{
    [Export] int height = 0;
    [Export] public int isFriend = 0;//0 友军 1敌军
    [Export] public string id;
    [Export] public string description = "";
    [Export] public int attack = 1;
    [Export] public int defence = 1;
    [Export] public string effect = "";
    [Export] public int cost = 1;
    [Export] public string name = "轻步兵";


    [Export] public CardTypes cardType = CardTypes.Infantry;
    [Export] public Rarity rarity = Rarity.Common;
    [Export] public string IconPath = "res://cards/轻步兵.png";
    [Export] public HQ isHq = HQ.normalCard;
    [Export] public int moveAble = 0;
    [Export] public int attackAble = 0;

    CardState state;
    Node2D cardBase;
    battlefield_ battleField;

    /// <summary>
    /// 卡牌的所在位置,记得要释放
    /// </summary>
    place_ myPlace; 
    int styleInit; //0 未初始化 1 已初始化
    battlefield_ battlefield;



    public void _ready()
    {


        height = 25;
        state = CardState.inHand;
        MouseFilter = MouseFilterEnum.Stop;
        cardBase = GetNode<Node2D>("Cardbase");
        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
        cardBase.Visible = false;

        battleField = GetTree().Root.GetNode<battlefield_>("BattleField");
        attackAble = 0;
        moveAble = 0;
        ZIndex = 10;

        


        if (this.isHq == HQ.hq && styleInit == 0)
        {
            GetNode<Label>("defence").Position = new Vector2(-20, 0);
            GetNode<Label>("attack").Visible = false;
            GetNode<Sprite2D>("cardbase").Texture = GD.Load<Texture2D>("res://cards/HQ_moscow.png");
            GetNode<Sprite2D>("icon").Visible = false;
            GetNode<Sprite2D>("unitType").Visible = false;
            GetNode<Label>("cost").Visible = false;
            GetNode<Label>("name").Visible = false;
            GetNode<RichTextLabel>("description").Position -= new Vector2(0, -20);
            styleInit = 1;

        }

        if (this.cardType == CardTypes.Command)
        {
            GetNode<Label>("defence").Visible = false;
            GetNode<Label>("attack").Visible = false;
            GetNode<Sprite2D>("cardbase").Texture = GD.Load<Texture2D>("res://cards/CommandBack.png");
        }

        battleField = GetTree().Root.GetNode<battlefield_>("BattleField");
    }



/// <summary>
/// 设置卡牌信息
/// </summary>
/// <param name="cardData"></param>
    public void SetCardInformation(CardData cardData)
    {

        name = cardData.Name;
        attack = cardData.Attack;
        defence = cardData.Defense;
        effect = cardData.Effect;
        cost = cardData.Cost;
        cardType = cardData.CardType;
        rarity = cardData.Rarity;
        IconPath = cardData.IconPath;
        description = cardData.Description;
        cardType = cardData.CardType;
        isHq = cardData.IsHq;
        RefreshState();
        
    }


/// <summary>
/// 将内存中的状态和现实出来的刷新一下，一般用于卡牌信息改变的时候
/// </summary>
    public void RefreshState()
    {
        GetNode<Sprite2D>("icon").Texture = GD.Load<Texture2D>(IconPath);
        GetNode<Label>("name").Text = name;
        GetNode<Label>("attack").Text = attack.ToString();
        GetNode<Label>("defence").Text = defence.ToString();
        GetNode<Label>("cost").Text = cost.ToString();
        GetNode<RichTextLabel>("description").Text = description;
        OnRefreshUnitType((int)cardType);

    }

    public void OnRefreshUnitType(int x)
    {
        Sprite2D unitType = GetNode<Sprite2D>("unitType");
        var _cardType = (CardTypes)x;
        unitType.Texture = _cardType switch
        {
            CardTypes.Tank => GD.Load<Texture2D>("res://cards/tank.png"),
            CardTypes.Plane => GD.Load<Texture2D>("res://cards/fighter.png"),
            CardTypes.Bomber => GD.Load<Texture2D>("res://cards/bomber.png"),
            CardTypes.Artillery => GD.Load<Texture2D>("res://cards/arlitery.png"),
            CardTypes.Command => GD.Load<Texture2D>("res://cards/command.png"),
            _ => GD.Load<Texture2D>("res://cards/inf.png"),
        };
    }
    public place_ GetMyPlace()
    {
        return myPlace;
    }

/// <summary>
/// 重新绑定自己的格子
/// </summary>
/// <param name="place"></param>
    public void SetMyPlace(place_ place)
    {
        if(myPlace != null)
        {
            myPlace.UnbondCard();
        }
        myPlace = place;
    }

/// <summary>
/// 死亡函数这块
/// </summary> 
    public void Dead()
    {
        if(myPlace!= null)
        {
            myPlace.UnbondCard();
        }
        
        state = CardState.destroyed;
        this.QueueFree();
    }


    /// <summary>
    /// 返回卡牌的当前状态
    /// </summary>
    /// <returns>当前状态</returns>
    public CardState getState()
    {
        return state;
    }

    /// <summary>
    /// 设置卡牌的当前状态
    /// </summary>
    /// <param name="state">目标状态</param>
    public void setState(CardState state)
    {
        this.state = state;
    }

    public Boolean CheckIfPointIsIn(Vector2 point)
    {
        return _HasPoint(point);
    }

    //以下为动画区域===============================================================================================

    /// <summary>
    /// 移动到目标地点的动画 异步
    /// </summary>
    /// <param name="destination">目标地点</param>
    /// <param name="duration">时长</param>
    /// <returns></returns>
    async public Task MoveToPosition(Vector2 destination, float duration = 0.5f)
    {
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(this, "position", destination, duration);

        // 等待 Tween 完成
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}

public enum HQ
{
    normalCard,
    hq,
    command
}

public enum CardTypes
{
    Plane,
    Bomber,
    Tank,
    Infantry,
    Artillery,
    Command,


}

public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum Stage
{
    Prepare,
    Draw,
    Battle,
    End,
    EnemyPrepare,
    EnemyDraw,
    EnemyBattle,
    EnemyEnd
}

public enum CardState
{
    inHand,
    caught,
    placed,
    played,
    attack,
    beAttacked,
    destroyed,
    inplaceAndCaught
}

public enum Times
{
    played,
    attack,
    beingAttack,
    dead,
    deployed,
    none,
    aFriendlyUnitDeployed,
    aFriendlyUnitAttacked,
    aFriendlyUnitBeingAttacked,
    aFriendlyUnitDead,
    aEnemyUnitDeployed,
    aEnemyUnitAttacked,
    aEnemyUnitBeingAttacked,
    aEnemyUnitDead,
    myHqBeingAttacked,
    enemyHqBeingAttacked
}

//储存卡牌信息
public partial class CardData : Resource
{
    // 基础信息
    [Export] public string Id { get; set; } = "";
    [Export] public HQ IsHq = HQ.normalCard;
    [Export] public string Name { get; set; } = "Unknown Card";
    [Export] public string Description { get; set; } = "";

    // 核心属性
    [Export] public int Attack { get; set; } = 1;
    [Export] public int Defense { get; set; } = 1;
    [Export] public int Cost { get; set; } = 1;
    [Export] public string Effect { get; set; } = "";

    // 元数据
    [Export] public CardTypes CardType { get; set; } = CardTypes.Infantry; // plane bomber tank infantry artillery
    [Export] public Rarity Rarity { get; set; } = Rarity.Common;   // "Common", "Rare", "Epic", "Legendary"

    // 资源引用
    [Export] public Texture2D Icon { get; set; }
    [Export] public string IconPath { get; set; } = ""; // 用于加载时暂存路径


}

