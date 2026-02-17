using Godot;
using System;
using System.Collections.Generic;
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
    [Export] int moveAble = 0;
    [Export] int attackAble = 0;

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

    public void RefreshUnit()
    {
        moveAble = 1;
        attackAble = 1;
    }

        /// <summary>
        /// 进行移动检查 会进行移动计数
        /// </summary>
        /// <returns></returns>
    public Boolean CheckIfCanMove()
    {
        if(cardType == CardTypes.Infantry||cardType == CardTypes.Artillery||cardType == CardTypes.Plane||cardType == CardTypes.Bomber)
        {
            attackAble = 0;
        }
            if(moveAble >= 1){
            moveAble--;
            return true;
        }
        return false;
    }

    public Boolean CheckIfCanAttack()
    {
        if (cardType == CardTypes.Infantry || cardType == CardTypes.Artillery || cardType == CardTypes.Plane || cardType == CardTypes.Bomber)
        {
            moveAble = 0;
        }
        if(attackAble >= 1){
            attackAble--;
            return true;
        }
        return false;
    }


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

    List<Change> ChangeList = new List<Change>();
    public void ExecChangeList()
    {
        foreach (var change in ChangeList)
        {
            switch (change.Type)
            {
                case ChangeType.GetAttack:
                    GetAttack(change.Value);
                    break;
                case ChangeType.LoseAttack:
                    LoseAttack(change.Value);
                    break;
                case ChangeType.GetDefence:
                    GetDefence(change.Value);
                    break;
                case ChangeType.LoseDefence:
                    LoseDefence(change.Value);
                    break;
            }
        }
        ChangeList.Clear();
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


    /// <summary>
    /// 读取价格
    /// </summary>
    public int ReadCost()
    {
        return cost;
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
    /// 弃置卡牌动画 - 从左侧进入、停留3s、旋转着向左侧飞出并删除
    /// </summary>
    /// <returns></returns>
    async public Task DiscardCard()
    {
        // 设置旋转中心为卡牌中心 (宽180/2=90, 高240/2=120)
        var Offset = new Vector2(90, 120);
        
        // 获取屏幕参数
        Vector2 screenSize = GetViewportRect().Size;
        
        // 计算左侧屏幕中间位置（以中心计算，因为Offset已设置）
        Vector2 leftScreenPos = new Vector2(100, screenSize.Y / 2 - 90);  // 屏幕左侧中间
        
        // 步骤1: 从左侧飞入，停在屏幕左侧中间 (1秒)
        var enterTween = CreateTween();
        enterTween.SetTrans(Tween.TransitionType.Quad);
        enterTween.SetEase(Tween.EaseType.Out);
        enterTween.TweenProperty(this, "global_position", leftScreenPos, 1.0f);
        await ToSignal(enterTween, Tween.SignalName.Finished);
        
        // 步骤2: 正着停留3秒（不动不转）
        await Task.Delay(1000);
        
                                                                     // 步骤3: 旋转着向左侧飞出 (1.5秒)
        Vector2 exitPosition = new Vector2(-1400, screenSize.Y / 2);  // 飞出到左侧
        var exitTween = CreateTween();
        exitTween.SetTrans(Tween.TransitionType.Quad);
        exitTween.SetEase(Tween.EaseType.In);
        exitTween.SetParallel(true); // 并行执行以下动画
        exitTween.TweenProperty(this, "global_position", exitPosition, 1.5f);
        exitTween.TweenProperty(this, "rotation", Mathf.Pi * 4, 1.5f); // 旋转两圈
        await ToSignal(exitTween, Tween.SignalName.Finished);
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

public enum ChangeType
{
    GetAttack,
    LoseAttack,
    GetDefence,
    LoseDefence,

}

struct Change
{
    public ChangeType Type;
    public int Value;

    public Change(ChangeType type, int value)
    {
        Type = type;
        Value = value;
    }
}
