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

    void RefreshAllCardDisplayOrder()
    {
        cardInPlaces = SortCardList(cardInPlaces).AsEnumerable().Reverse().ToList();
        foreach (var card in cardInPlaces)
        {
            MoveChild(card,0);
        }
        foreach (var card in player1.GetCardsInHand())
        {
            MoveChild(card,0);
        }
        if(cardNowChoose!= null)
        {
            MoveChild(cardNowChoose,0);
        }
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

/// <summary>
/// 检查卡牌是否在合法区域内
/// </summary>
Control validArea;
Marker2D arrowFrom; 

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

        //初始化手卡区
        player1 = new Player(new Vector2(700,700), this);
        
        //初始化打牌判定区域
        validArea = GetNode<Control>("validCardArea");

        //初始化箭头
        arrowFrom = GetNode<Marker2D>("Cardbase/Marker2D");

        //初始化卡组

         var INIpath = "res://cards/card.ini";

        if (!Godot.FileAccess.FileExists(INIpath))
        {
            GD.PushError($"INI file not found: {INIpath}");
            return;
        }

        //初始化箭头
        cardBase = GetNode<Node2D>("Cardbase");
        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
        cardBase.Visible = false;

        var configFile = new IniFile();
        configFile.Load("cards\\card.ini");
        //_item 所有卡的数据组成的数组
        Dictionary<string, CardData> _items = [];

        foreach (var section in configFile)
        {
            var card = new CardData();
            {
                card.Id = section.Key;
                card.Name = configFile[section.Key]["name"].ToString();
                card.Description = configFile[section.Key]["description"].ToString();
                card.Attack = configFile[section.Key]["attack"].ToInt();
                card.Defense = configFile[section.Key]["defense"].ToInt();
                card.Cost = configFile[section.Key]["price"].ToInt();
                card.IsHq = (HQ)configFile[section.Key]["isHq"].ToInt();
                card.Effect = configFile[section.Key]["effect"].ToString();
                card.CardType = GetTypes(configFile[section.Key]["cardType"].ToString());
                card.Rarity = GetRarity(configFile[section.Key]["rarity"].ToString());
                card.IconPath = configFile[section.Key]["icon"].ToString(); ;
            }
            _items[card.Id] = card;
        }
    }

/// <summary>
/// 稀有度->string
/// </summary>
/// <param name="rare"></param>
/// <returns></returns>
    public static Rarity GetRarity(string rare)
    {
        switch (rare)
        {
            case "Common":
                return Rarity.Common;
            case "Rare":
                return Rarity.Rare;
            case "Epic":
                return Rarity.Epic;
            default:
                return Rarity.Legendary;
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
    Vector2 arrowOffset = new Vector2(90, 120);

    public override void _Input(InputEvent @event)
    {
         if (@event is InputEventMouseButton mouseButton)
        {
            var mousePosition = GetGlobalMousePosition();
            //选择卡
            if (mouseButton.Pressed)
            {
                RefreshAllCardDisplayOrder();
                // 点击事件
                
                var card = CheckCardClick(mousePosition);
                //手上的
                if (card != null && card.getState() == CardState.inHand)
                {
                    offset = card.GetGlobalPosition() - mousePosition;
                    cardNowChoose = card;
                    card.setState(CardState.caught);
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
                
                
                
            }
            //释放时 取消选择
            if (mouseButton.Pressed==false)
            {
                RefreshAllCardDisplayOrder();
                //如果现在选着卡
                if(cardNowChoose!= null)
                {
                    //松开时检查是否在任意一个合法的格子上
                    if (validArea.GetGlobalRect().HasPoint(mousePosition))
                    {
                        var result = GetPlaceWithPosition(mousePosition);
                        if(result != null)
                        {
                            if(cardNowChoose.getState() == CardState.caught)
                            {
                                //在手上并且在的话就移动过去
                                player1.RemoveFromHand(cardNowChoose);
                                result.BondCard(cardNowChoose);
                                cardNowChoose.SetMyPlace(result);
                                cardNowChoose.MoveToPosition(result.GetGlobalPosition());
                                cardNowChoose.setState(CardState.placed);
                            }
                            else if(cardNowChoose.getState() == CardState.inplaceAndCaught)
                            {
                                //在场上并且拖动到合法格子
                                cardNowChoose.MoveToPosition(result.GetGlobalPosition());
                                cardNowChoose.SetMyPlace(result);
                                cardNowChoose.setState(CardState.placed);
                            }
                            
                        }
                        
                        
                    }
                    else
                        {
                            //否则回到起点
                            if (cardNowChoose.GetMyPlace() != null)
                            {
                                cardNowChoose.MoveToPosition(cardNowChoose.GetMyPlace().GlobalPosition);
                                
                            }
                            cardNowChoose.setState(CardState.inHand);
                        }
                    //不管怎样都刷新手牌区
                    player1.RefreshMyHand();
                    cardNowChoose = null;
                    //隐藏箭头

                    cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                    cardBase.Visible = false;
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        //跟踪卡牌
        if (Input.IsMouseButtonPressed(MouseButton.Left) && cardNowChoose != null && cardNowChoose.getState() == CardState.caught)
        {
            cardNowChoose.SetGlobalPosition(GetGlobalMousePosition()+offset);
        }
    }




/// <summary>
/// 暂时用作测试
/// </summary>
    public void OnNextTurnButtonPressed()
    {
        _ = player1.DrawCard();
        
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
    public Player(Godot.Vector2 initPos,battlefield_ battlefield)
    {
        //初始化 设置手的位置
        this.initPos = initPos;
        this.battlefield = battlefield;
        PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
        //temp
        deck = new List<cardBase_>();
        for(int i = 0; i < 30; i++)
        {
            
            var card = cardRes.Instantiate() as cardBase_;
            deck.Add(card);
        }
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
    public async Task DrawCard(int number=1)
    {
        if (deck.Count >= 0)
        {
            var card = deck[0];
            deck.RemoveAt(0);
            card.SetPosition(new Godot.Vector2(-2000, 800));
            await AddCardToHand(card);
        }
        
    }

}


/// <summary>
/// 管理所有卡牌 用于加载卡
/// </summary>
public class CardMaganer
{
    
}
