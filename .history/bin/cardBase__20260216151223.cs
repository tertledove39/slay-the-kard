using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 卡牌本身类 只用于存储卡牌信息和播放动画
/// </summary>
public partial class cardBase_ : Control
{
    [Export] int height = 0;
    [Export] public IsFriend isFriend = 0;//isfriend 0表示敌方 1表示我方
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

    Texture2D UssrPic;
    Texture2D Germanypic;

    /// <summary>
    /// 卡牌的所在位置,记得要释放
    /// </summary>
    place_ myPlace; 
    int styleInit; //0 未初始化 1 已初始化
    battlefield_ battlefield;


/// <summary>
/// 设置自身阵营
/// </summary>
/// <param name="isFriend"></param>
    public void SetIsFriend(IsFriend isFriend)
    {
        switch (isFriend)
        {
            case IsFriend.friend:
                UssrPic = GD.Load<Texture2D>("res://cards/ussr.png");
                GetNode<Sprite2D>("country").Texture = UssrPic;
                break;
            case IsFriend.enemy:
                Germanypic = GD.Load<Texture2D>("res://cards/Germany.png");
                GetNode<Sprite2D>("country").Texture = Germanypic;
                break;
        }

        this.isFriend = isFriend;
    }

/// <summary>
/// 获取自身阵营
/// </summary>
/// <returns></returns>
    public IsFriend GetIsFriend()
    {
        return isFriend;
    }



/// <summary>
/// 获得防御力
/// </summary>
/// <param name="n"></param>
    public void GetDefence(int n)
    {
        if(defence+n <= 99) defence += n;
        else defence = 99; 
        RefreshState();
    }

/// <summary>
/// 失去防御力
/// </summary>
/// <param name="n"></param>
    public void LoseDefence(int n)
    {
        if (defence - n >= 0) defence -= n;
        else defence = 0;
        RefreshState();
    }

/// <summary>
/// 增加攻击力
/// </summary>
/// <param name="n"></param>
    public void GetAttack(int n)
    {
        if (attack + n <= 99) attack += n;
        else attack = 99;
        RefreshState();
    }

/// <summary>
/// 减少攻击力
/// </summary>
/// <param name="n"></param>
    public void LoseAttack(int n)
    {
        if (attack - n >= 0) attack -= n;
        else attack = 0;
        RefreshState();
    }

/// <summary>
/// 读取防御力
/// </summary>
/// <returns></returns>
    public int ReadDefence()
    {
        return defence;
    }

/// <summary>
/// 读取攻击力
/// </summary>
/// <returns></returns>
    public int ReadAttack()
    {
        return attack;
    }



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

        UssrPic = GD.Load<Texture2D>("res://cards/ussr.png");
        Germanypic = GD.Load<Texture2D>("res://cards/Germany.png");


        if (this.isHq == HQ.hq && styleInit == 0)
        {
            GetNode<Label>("defence").Position = new Vector2(75, 120);
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
        if(FileAccess.FileExists(IconPath)) GetNode<Sprite2D>("icon").Texture = GD.Load<Texture2D>(IconPath);   
        if(name!= null)GetNode<Label>("name").Text = name;
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
        myPlace.BondCard(this);
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

    /// <summary>
    /// 弃置卡牌动画 - 移到画面边缘、旋转等待3s、旋转移出画面并删除
    /// </summary>
    /// <returns></returns>
    async public Task DiscardCard()
    {
        // 获取当前屏幕坐标（受相机影响）
        Vector2 screenSize = GetViewportRect().Size;
        Vector2 currentPos = GlobalPosition;
        
        // 计算目标位置：右边缘下方 (屏幕坐标右边界 + 100px的偏移)
        Vector2 edgePosition = new Vector2(currentPos.X + 400, currentPos.Y + 200);
        
        // 步骤1: 缓慢移动到画面下方右侧边缘 (1秒)
        var moveTween = CreateTween();
        moveTween.SetTrans(Tween.TransitionType.Quad);
        moveTween.SetEase(Tween.EaseType.In);
        moveTween.TweenProperty(this, "global_position", edgePosition, 1.0f);
        await ToSignal(moveTween, Tween.SignalName.Finished);
        
        // 步骤2: 在边缘旋转3秒（不移动）
        //var rotateTween = CreateTween();
        //rotateTween.SetTrans(Tween.TransitionType.Linear);
        //rotateTween.TweenProperty(this, "rotation", Mathf.Pi * 2, 3.0f); // 旋转一圈
        //await ToSignal(rotateTween, Tween.SignalName.Finished);
        
        // 步骤3: 旋转着飞出画面 (1.5秒) - 同时旋转和移动
        Vector2 exitPosition = edgePosition + new Vector2(800, 400);
        var exitTween = CreateTween();
        exitTween.SetTrans(Tween.TransitionType.Quad);
        exitTween.SetEase(Tween.EaseType.In);
        exitTween.SetParallel(true); // 并行执行以下两个动画
        exitTween.TweenProperty(this, "global_position", exitPosition, 1.5f);
        exitTween.TweenProperty(this, "rotation", Mathf.Pi * 4, 1.5f); // 再旋转两圈
        await ToSignal(exitTween, Tween.SignalName.Finished);
        
        // 步骤4: 删除卡牌
        Dead();
    }

    async public Task AttackInf(cardBase_ target)
    {
        
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


public enum IsFriend
{
    friend,
    enemy,
    neutral,
    enemyNeutral
}
