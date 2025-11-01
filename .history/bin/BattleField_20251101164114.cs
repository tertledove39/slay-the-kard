using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public partial class BattleField : Control
{

    private CardMaganer cardMaganer = new();
    private PackedScene _card;
    string INIpath = "res://cards/card.ini";
    private Dictionary<string, CardData> _items = [];
    private AudioStreamPlayer2D soundplayer;
    public Player player1;
    private List<Place> supportLine = [];
    private List<Place> frontLine = [];
    private List<Place> enemySupprotLine = [];
    private int times;
    private cardControl enemyHQInstance;
    private cardControl myHQInstance;
    public Hand myhand;
    int spawnX = 100;
    private cardControl nowAttackingCard;
    private cardControl nowChoosedCard;
    private List<cardControl> DrawPile;
    public Label pointLabel;
    public Label pointMaxLabel;
    public Stage nowStage;
    private TextureButton nextTurnButton;
    public int playerTurn = 0;
    public List<cardControl> cardInPlaces = [];
    

    //地图上的所有格子
    public Dictionary<string, Place> allPlaces = [];
    private Player player2;

    public void RefreshPointLable()
    {
        pointLabel.Text = player1.point.ToString();
        pointMaxLabel.Text = player1.pointMax.ToString();
    }

    public void Refresh()
    {
        RefreshPointLable();
        foreach (Node child in GetChildren().Where(x => x is cardControl))
        {
            cardControl childCard = (cardControl)child;
            if (childCard.isFriend == 0 && childCard.state == CardState.placed)
                childCard.Refresh();
        }

    }
    async public void OnNextTurnButtonPressed()
    {

        nowStage = Stage.End;
        nextTurnButton.Disabled = true;
        await EnemyTurn();
        nowStage = Stage.Prepare;
        player1.AddPointMaxNatural();
        nowStage = Stage.Draw;
        player1.DrawCard(1);
        nowStage = Stage.Battle;
        Refresh();
        nextTurnButton.Disabled = false;

    }

    public void AddCardToPlace(string id, string place,Player player)
    {
        cardControl card;
        card = LoadCard(id);
        player.AddCard(card);
        AddChild(card);
        card.targetPosition = allPlaces[place].GlobalPosition + new Vector2(90, 110);
        card.GlobalPosition = allPlaces[place].GlobalPosition + new Vector2(90, 110); ;
        allPlaces[place].myCard = card;
        card.myPlace = allPlaces[place];
        card.state = CardState.placed;
        card.battleField = this;
        card.AttackSend += OnAttackSend;
        cardInPlaces.Add(card);
        if (card.inHand != null)
        {
            card.inHand.RemoveCard(card);
            card.inHand = null;
        }

        if (card.battleField != null)
        {
            card.GetParent().MoveChild(card, 17);
        }
        allPlaces[place].myCard = card;
        RefreshAllCardDisplayOrder();
        }
    
    public Dictionary<string, Place> getAllPlacesDictionary
    {
        get { return allPlaces; }
        set {; }
    }


    public cardControl LoadCard(string id)
    {
        var instance = _card.Instantiate<cardControl>();
        instance.SetCardInformation(cardMaganer.GetCard(id));
        return instance;
    }

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

    public void EnemyDeployUnit(string id)
    {
        Place firstPlace = null;
        foreach (var place in enemySupprotLine)
        {
            if (place.myCard == null)
            {
                firstPlace = place;
                break;
            }
        }
        if (firstPlace != null)
        {
            AddCardToPlace(id, firstPlace.Name,player2);
            return;
        }
        
                
    }

    public void RefreshAllCardDisplayOrder()
    {
        int beginPosition = 17;
        List<cardControl> _cardInPlace = [];
        foreach (var card in getAllPlacesDictionary.Values)
        {
            if (card.myCard != null)
            {
                _cardInPlace.Add(card.myCard);
            }
        }

        foreach (var card in _cardInPlace)
        {
            card.GetParent().MoveChild(card, beginPosition);
            card.ZIndex = 10;
            beginPosition++;
        }
        foreach (var card in player1.myHand.cardInHands)
        {
            card.GetParent().MoveChild(card, beginPosition);
            card.ZIndex = 50;
            beginPosition++;
        }
    }

    /// <summary>
    /// 如果这条阵线是空的 就返回1 否则为0<para />
    /// </summary>
    public static int CheckIfALineIsEmpty(List<Place> line)
    {

        foreach (var place in line)
        {
            if (place.myCard != null)
            {
                return 0;
            }
        }
        return 1;
    }

    /// <summary>
    /// 一个单位攻击 先检查是否可以移动 再检查是否可以攻击<para />
    /// </summary>
    /// <param name="unit">攻击源</param>
    public void EnemyUnitAttack(cardControl unit)
    {
        if (unit.state != CardState.placed || unit.isFriend != player2.isFriend)
        {
            return;
        }
        if (unit.attackAble > 0)
        {
            if (CheckIfALineIsEmpty(frontLine) == 1)
            {

            }
        }
    }
    /// <summary>
    /// 尝试把一个敌方单位移动到第一个可能的位置<para />
    /// </summary>
    public void EnemyUnitMove()
    {
        foreach(var place in frontLine)
        {
            if (place.myCard == null)
            {
                return;
            }
        }
    }
    async public Task EnemyTurn()
    {
        nowStage = Stage.EnemyPrepare;
        nowStage = Stage.EnemyDraw;
        nowStage = Stage.EnemyBattle;
        enemyHQInstance.GetDefence(2 + playerTurn);

        EnemyDeployUnit("t70");
        if (playerTurn == 3)
        {
            enemyHQInstance.GetDefence(5);
        }
        if (playerTurn == 5)
        {
            enemyHQInstance.GetDefence(10);
        }
        if (playerTurn == 7)
        {
            foreach (var _card in cardInPlaces)
            {
                if (_card.isFriend == 0)
                {
                    _card.GetDefence(-5);
                }
            }
            if (playerTurn == 10)
            {
                foreach (var _card in cardInPlaces)
                {
                    if (_card.isFriend == 0)
                    {
                        _card.GetDefence(-20);
                    }
                }
            }
            if (playerTurn == 14)
            {
                foreach (var _card in cardInPlaces)
                {
                    myHQInstance.GetDefence(-20);
                }
            }
            myHQInstance.GetDefence(-3);
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

        }
        nowStage = Stage.EnemyEnd;
    }
    public cardControl HQinit(int place,Player player)
    {
        myHQInstance = LoadCard("moscow");
        myHQInstance.myPlayer = player;
        myHQInstance.battleField = this;
        myHQInstance.isFriend = player.isFriend;
        AddChild(myHQInstance);
        myHQInstance.targetPosition = allPlaces[place.ToString()].GlobalPosition + new Vector2(90, 110);
        myHQInstance.GlobalPosition = allPlaces[place.ToString()].GlobalPosition + new Vector2(90, 110); ;
        myHQInstance.state = CardState.placed;
        myHQInstance.OnButtonUp();
        allPlaces[place.ToString()].myCard = myHQInstance;
        player.AddCard(myHQInstance);
        return myHQInstance;
    }


    public override void _Ready()
    {
        DrawPile = new List<cardControl>();
        nextTurnButton = GetNode<TextureButton>("NextTurnButton");
        foreach (Node place in GetChildren())
        {
            if (place.SceneFilePath == "res://bin/place.tscn")
                allPlaces[place.Name] = (Place)place;

        }
        supportLine = [allPlaces["11"], allPlaces["12"], allPlaces["13"], allPlaces["14"], allPlaces["15"]];
        enemySupprotLine = [allPlaces["1"], allPlaces["2"], allPlaces["3"], allPlaces["4"], allPlaces["5"]];
        frontLine = [allPlaces["6"], allPlaces["7"], allPlaces["8"], allPlaces["9"], allPlaces["10"]];


        soundplayer = GetNode<AudioStreamPlayer2D>("SoundPlayer");


        pointLabel = GetNode<Label>("point");
        pointMaxLabel = GetNode<Label>("pointMax");


        _card = GD.Load<PackedScene>("res://bin/cardbase.tscn");
        if (!Godot.FileAccess.FileExists(INIpath))
        {
            GD.PushError($"INI file not found: {INIpath}");
            return;
        }

        var configFile = new IniFile();
        configFile.Load("cards\\card.ini");

        foreach (var section in configFile)
        {
            var card = new CardData();
            {
                card.id = section.Key;
                card.name = configFile[section.Key]["name"].ToString();
                card.description = configFile[section.Key]["description"].ToString();
                card.attack = configFile[section.Key]["attack"].ToInt();
                card.defense = configFile[section.Key]["defense"].ToInt();
                card.cost = configFile[section.Key]["price"].ToInt();
                card.isHq = (HQ)configFile[section.Key]["isHq"].ToInt();
                card.effect = configFile[section.Key]["effect"].ToString();
                card.cardType = GetTypes(configFile[section.Key]["cardType"].ToString());
                card.rarity = GetRarity(configFile[section.Key]["rarity"].ToString());
                card.iconPath = configFile[section.Key]["icon"].ToString(); ;
            }
            _items[card.id] = card;
        }

        cardMaganer.SetCardDictionary(_items);

        for (int i = 0; i < 40; i++)
        {
            cardControl enemyInstance;
            enemyInstance = LoadCard(cardMaganer.GetRandomCard());
            enemyInstance.AttackSend += OnAttackSend;
            enemyInstance.beingChoosed += OnBeingChoosed;
            DrawPile.Add(enemyInstance);
        }

        //load HQ
        
        

        player1 = new Player(DrawPile, 0,new Vector2(800,800),this);
        player1.DrawCard(4);
        myHQInstance = HQinit(13,player1);
        player1.myHQ = myHQInstance;

        player2 = new Player(DrawPile, 1,new Vector2(800,100),this);
        player2.DrawCard(4);
        player2.isFriend = 1;
        enemyHQInstance = HQinit(3,player2);
        player2.myHQ = enemyHQInstance;
    }

    public void OnAttackSend(cardControl sender, cardControl target)
    {
    
        nowAttackingCard = sender;
        if (target != null && sender != null && sender != target && sender.state==CardState.placed && target.state==CardState.placed)
        {
            target.BeingAttacked(sender);
            sender.BeingAttacked(target);
        }
        soundplayer.Play();
        
    }

    public void OnBeingChoosed(cardControl sender)
    {
        nowChoosedCard = sender;
    }

}


public class CardMaganer
{
    private Dictionary<string, CardData> _items = [];
    private Random random = new Random();
    public string GetRandomCard()
    { CardData randomCard;
        do
        {
            randomCard = _items.Values.ToList()[random.Next(0, _items.Count)];

        } while (randomCard.isHq == HQ.hq);

        return randomCard.id;
    }

    public void SetCardDictionary(Dictionary<string, CardData> items)
    {
        // 将传入的卡牌字典赋值给私有字段_items
        _items = items;
    }

    public CardData GetCard(string id)
    {

        foreach (var i in _items)
        {

        }
        if (_items.ContainsKey(id))
        {
            return _items[id];
        }
        else
        {
            return null;
        }
    }
}
public partial class CardData : Resource
{
    // 基础信息
    [Export] public string id{ get; set; } = "";
    [Export] public HQ isHq = HQ.normalCard;
    [Export] public string name { get; set; } = "Unknown Card";
    [Export] public string description { get; set; } = "";

    // 核心属性
    [Export] public int attack { get; set; } = 1;
    [Export] public int defense { get; set; } = 1;
    [Export] public int cost { get; set; } = 1;
    [Export] public string effect { get; set; } = "";

    // 元数据
    [Export] public CardTypes cardType { get; set; } = CardTypes.Infantry; // plane bomber tank infantry artillery
    [Export] public Rarity rarity { get; set; } = Rarity.Common;   // "Common", "Rare", "Epic", "Legendary"

    // 资源引用
    [Export] public Texture2D Icon { get; set; }
    [Export] public string iconPath { get; set; } = ""; // 用于加载时暂存路径


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

public class Player
{
    public Hand myHand;
    public List<cardControl> myCards = [];
    public BattleField myBattleField;
    public cardControl myHQ;
    public List<cardControl> drawPile;
    public int isFriend = 0; //0 Friend

    public void AddCard(cardControl card)
    {
        myCards.Add(card);
        card.isFriend = isFriend;
    }
    public void DrawCard(int x)
    {
        for (int i = 0; i < x; i++)
        {
            if (drawPile.Count > 0)
            {
                drawPile[0].GlobalPosition = new Vector2(1400, 100);
                myHand.AddCard(drawPile[0]);
                AddCard(drawPile[0]);
                myBattleField.AddChild(drawPile[0]);
                drawPile[0].isFriend = isFriend;

                drawPile.RemoveAt(0);

            }

        }

    }
    public Player(List<cardControl> drawPile, int isFriend, Vector2 position,BattleField myBattleField)
    {
        myHand = new Hand(position);
        myHand.myPlayer = this;
        this.isFriend = isFriend;
        this.drawPile = drawPile;
        this.myBattleField = myBattleField;
    }

    public void RefreshPoint()
    {
        point = pointMax;
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
    }

    public void AddPointMaxNatural()
    {
        if (pointMax + 1 <= pointMaxMax)
        {
            pointMax += 1;
        }
        RefreshPoint();
    }

    public int point = 0;
    public int pointMax=1;
    public int pointMaxMax=12;
    public int pointMaxMaxMax=24;

}

public class Hand
{
    public List<cardControl> cardInHands = [];
    public int cardMax = 9;
    private int count = 0;
    public Vector2 initPos;
    public Player myPlayer;


    public Hand(Vector2 initPos)
    {
        this.initPos = initPos;
    }


    async public void AddCard(cardControl card)
    {

        if (cardInHands.Count <= cardMax)
        {
            cardInHands.Add(card);
            card.inHand = this;
            card.myPlayer = myPlayer;
            count = cardInHands.Count;
            card.ZIndex = 50;

            RefreshMyHand();
        }
        else
        {
            if (card.inHand != null)
            {
                card.inHand.RemoveCard(card);
            }
            card.state = CardState.destroyed;
            card.MoveTo(new Vector2(1400, 500));
            await Task.Delay(3000);
            card.Visible = false;
            card.QueueFree();
        }
    }

    public void RemoveCard(cardControl card)
    {
        cardInHands.Remove(card);
        count = cardInHands.Count;
        card.ZIndex = 10;

        RefreshMyHand();
    }

    private void RefreshMyHand()
    {
        float left = initPos.X - (count - 1) * 80;
        for (int i = 0; i < count; i++)
        {
            cardInHands[i].targetPosition = new Vector2(left + i * 160, initPos.Y);
        }
    }
}

