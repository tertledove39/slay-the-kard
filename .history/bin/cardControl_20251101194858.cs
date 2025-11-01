using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
public enum HQ
{
    normalCard,
    hq,
    command
}
public partial class cardControl : Control
{



    [Signal]
    public delegate void refreshAttackEventHandler(int attack);
    [Signal]
    public delegate void refreshDefenceEventHandler(int defence);
    [Signal]
    public delegate void refreshCostEventHandler(int cost);
    [Signal]
    public delegate void refreshNameEventHandler(string name);
    [Signal]
    public delegate void refreshDescriptionEventHandler(string description);
    [Signal]
    public delegate void refreshEffectEventHandler(string effect);
    [Signal]
    public delegate void refreshIconPathEventHandler(string effect);
    [Signal]
    public delegate void refreshUnitTypeEventHandler(int cardType);
    [Signal]
    public delegate void AttackSendEventHandler(cardControl self, cardControl target);
    [Signal]
    public delegate void beingChoosedEventHandler(cardControl self);

    //[Signal]
    //public delegate void beingSentToAPlaceEventHandler(cardControl x,string target);


    private bool _isDragging = false;
    private Vector2 _dragOffset;
    public Place myPlace;
    [Export] public CardState state = CardState.inHand;
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
    public Player myPlayer;

    public Hand inHand;
    public BattleField battleField;
    private Label costLabel;
    public Vector2 offset = new Vector2(90, 110);
    private Label attackLabel;
    private Label defenceLabel;
    private Label nameLabel;
    private RichTextLabel descriptionLabel;
    private Sprite2D icon;
    private Sprite2D unitTypeIcon;
    public Vector2 targetPosition;
    private Vector2 _direction = new Vector2(900, 900);
    private float speed = 1600;
    private Node2D cardBase;
    private int beingChoosedNow = 0;//0 未选中 1 选中
    private AudioStreamPlayer2D soundolayer;
    private int styleInit = 0;
    public void RefreshState()

    {
        EmitSignal(SignalName.refreshAttack, attack);
        EmitSignal(SignalName.refreshDefence, defence);
        EmitSignal(SignalName.refreshCost, cost);
        EmitSignal(SignalName.refreshDescription, description);
        EmitSignal(SignalName.refreshEffect, effect);
        EmitSignal(SignalName.refreshName, name);
        EmitSignal(SignalName.refreshIconPath, IconPath);
        EmitSignal(SignalName.refreshUnitType, (int)cardType);

    }


    public void Refresh()
    {
        moveAble = 1;
        attackAble = 1;
    }
    public void MoveTo(Vector2 position)
    {
        targetPosition = position;
    }

    public void dead()
    {
        state = CardState.destroyed;
        this.Visible = false;
        this.QueueFree();
        if(battleField.cardInPlaces.Contains(this)){
            battleField.cardInPlaces.Remove(this);
        }
    }

    async public void RunEffect(cardControl target)
    {
        if (inHand != null)
        {
            inHand.RemoveCard(this);
        }
        this.state = CardState.destroyed;
        MoveTo(new Vector2(1400, 500));
        string[] effects= [effect];
        if (effect.Contains(','))
        {
            effects = effect.Split(',');
        }
        foreach (var a_effect in effects)
        {
            List<cardControl> targets = [];
            int result=0;
            var parts = a_effect.Split('|');

            foreach (var part in parts)
            {
                string[] sections = [part];
                if (part.Contains('.'))
                {
                    sections = part.Split('.');
                }
                foreach (var section in sections)
                {
                    switch (section)
                    {
                        case "allUnits":
                            targets = battleField.cardInPlaces; break;
                    }
                    if (section == "enemy")
                    {
                        targets = battleField.cardInPlaces.FindAll(x => x.isFriend == 1 - isFriend);
                    }
                    if (section == "friend")
                    {
                        targets = battleField.cardInPlaces.FindAll(x => x.isFriend == isFriend);
                    }
                    if (section == "count")
                    {
                        result = targets.Count;
                    }
                    if (section == "target")
                    {
                        targets = [target];
                    }


                }
                if (part.StartsWith("setResult"))
                {
                    Match match = Regex.Match(part, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        var xStr = match.Groups[1].Value;
                        if (int.TryParse(xStr, out int x))
                        {
                            result = x;
                        }
                    }
                }
                if (part == "drawCard")
                {

                    myPlayer.DrawCard(result);
                }
                int number;
                bool isNumeric = int.TryParse(part, out number);
                if (isNumeric)
                {
                    result = number;
                }

                if (part.StartsWith("damage"))
                {
                    Match match = Regex.Match(part, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        var xStr = match.Groups[1].Value;
                        if (int.TryParse(xStr, out int x))
                        {
                            foreach (var _target in targets)
                            {
                                _target.GetDefence(-x);
                            }
                        }
                    }
                }
                if (part.StartsWith("addToSupportLine"))
                {
                    Match match = Regex.Match(part, @"\(([^)]*)\)");
                    if (match.Success)
                    {
                        List<string> freePlace = new List<string> { "11", "12", "14", "15" };
                        foreach (var _freePlace in freePlace)
                        {
                            if (battleField.allPlaces[_freePlace].myCard == null)
                            {
                                battleField.AddCardToPlace(match.Groups[1].Value, _freePlace,battleField.player1);
                                break;
                            }
                        }
                    }
                }
            }
        }
        await ToSignal(GetTree().CreateTimer(3.0f), "timeout");
        dead();

        GD.Print("runEffect");

    }



    public void _ready()
    {
        soundolayer = GetNode<AudioStreamPlayer2D>("cardSoundPlayer");
        ZIndex = 50;
        state = CardState.inHand;
        MouseFilter = MouseFilterEnum.Stop;
        cardBase = GetNode<Node2D>("Cardbase");
        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
        cardBase.Visible = false;
        beingChoosedNow = 0;
        battleField = GetTree().Root.GetNode<BattleField>("BattleField");
        attackAble = 0;
        moveAble = 0;

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
    }

    public void OnButtonDown()
    {
        if (isFriend == 0 && isHq == HQ.normalCard && battleField.playerTurn == 0)
        {
            ZIndex = 15;
            if (state == CardState.inHand)
            {
                if (myPlayer.point >= cost)
                {
                    state = CardState.caught;
                    _dragOffset = GetViewport().GetMousePosition() - Position;
                    if (myPlace != null)
                    {
                        myPlace.myCard = null;
                    }
                }



            }
            else if (state == CardState.placed)
            {
                beingChoosedNow = 1;
                cardBase.ProcessMode = Node.ProcessModeEnum.Inherit;
                cardBase.Visible = true;
            }
        }

    }

    public void OnButtonUp()
    {

        if (isFriend == 0 && battleField.playerTurn == 0)
        {
            if (state == CardState.caught)
            {
                var battleField = GetTree().Root.GetNode<BattleField>("BattleField");
                var allPlacesDictionary = battleField.getAllPlacesDictionary;
                double minDistance = 999999;
                string minPlaceName = "";
                var offset = new Vector2(90, 110);
                foreach (var place in allPlacesDictionary)

                {
                    if (place.Value.myCard == null)
                    {
                        if (GlobalPosition.DistanceTo(place.Value.GlobalPosition + offset) < minDistance)
                        {
                            minDistance = GlobalPosition.DistanceTo(place.Value.GlobalPosition + offset);
                            minPlaceName = place.Value.Name;
                        }
                    }
                }
                if (minPlaceName == "11" || minPlaceName == "12" || minPlaceName == "13" || minPlaceName == "14" || minPlaceName == "15" || isHq == HQ.hq)
                {
                    allPlacesDictionary[minPlaceName].myCard = this;
                    myPlace = allPlacesDictionary[minPlaceName];
                    targetPosition = allPlacesDictionary[minPlaceName].GlobalPosition + offset;
                    state = CardState.placed;

                    if (inHand != null)
                    {
                        inHand.RemoveCard(this);
                        inHand = null;
                    }

                    if (battleField != null)
                    {
                        this.GetParent().MoveChild(this, 17);
                    }

                    myPlayer.point -= cost;
                    battleField.Refresh();
                    battleField.cardInPlaces.Add(this);
                }
                else
                {
                    state = CardState.inHand;
                }


            }// 不知道为什么会产生偏移,总之就这样把偏移消掉}

            if (state == CardState.placed)
            {
                beingChoosedNow = 0;
                cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
                cardBase.Visible = false;
            }
            ZIndex = 10;
        }
        battleField.RefreshAllCardDisplayOrder();
    }

    public override void _Process(double delta)
    {
        if (state == CardState.caught)
        {
            Position = GetViewport().GetMousePosition() - _dragOffset;
        }

        if (Position.DistanceTo(targetPosition) > 20)
        {
            _direction = (targetPosition - Position).Normalized();
            Position += (_direction * (float)delta * speed);
        }
        else
        {
            Position = targetPosition;
        }
        if (beingChoosedNow == 1)
        {
            ;
        }
    }
    /// <summary>
    /// 攻击一个目标
    /// </summary>
    /// <param name="target">攻击目标</param>
    public void Attack(cardControl target)
    {
        EmitSignal(SignalName.AttackSend, this, target);
        if (cardType == CardTypes.Infantry || cardType == CardTypes.Bomber || cardType == CardTypes.Plane || cardType == CardTypes.Artillery)
        {
            attackAble = 0;
            moveAble = 0;
        }
        if (cardType == CardTypes.Tank)
        {
            attackAble = 0;
        }
    }

    public void Move(Place place)
    {
        if (battleField.frontLine.Contains(this.myPlace))
        {
            return;
        }
        place.myCard = this;
        myPlace.myCard = null;
        myPlace = place;
        targetPosition = place.GlobalPosition + offset;
        if (cardType == CardTypes.Infantry)
        {
            attackAble = 0;
            moveAble = 0;
        }
        else if (cardType == CardTypes.Tank)
        {
            moveAble = 0;
        }
    }
    public override void _Input(InputEvent @event)
    {
        //cardControl target;
        if (@event is InputEventMouseButton mouseEvent && isFriend == 0 && battleField.nowStage == Stage.Battle)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left && !mouseEvent.Pressed)
            {
                var battleField = GetTree().Root.GetNode<BattleField>("BattleField");

                            

                            var allPlacesDictionary = battleField.getAllPlacesDictionary;
                            string minPlaceName = GetClostestPlace();
                if (state == CardState.placed && beingChoosedNow == 1)
                {
                        if (allPlacesDictionary[minPlaceName].myCard != null && allPlacesDictionary[minPlaceName].myCard != this)
                        {
                        if (allPlacesDictionary[minPlaceName].myCard.isFriend != this.isFriend && attackAble == 1)
                        {
                            Attack(allPlacesDictionary[minPlaceName].myCard);
                            goto end;
                        }
                    }
                        if (allPlacesDictionary[minPlaceName].myCard == null && allPlacesDictionary[minPlaceName].line == myPlace.line + 1 && allPlacesDictionary[minPlaceName].line != 2 && moveAble == 1)
                        {
                            Move(allPlacesDictionary[minPlaceName]);
                        }
                }
                else if (state == CardState.caught && cardType == CardTypes.Command)
                {
                    if (mouseEvent.ButtonIndex == MouseButton.Left && !mouseEvent.Pressed && myPlayer.point >= cost)
                    {
                        myPlayer.point -= cost;
                        battleField.Refresh();
                        RunEffect(allPlacesDictionary[minPlaceName].myCard);
                    }
                }
            end:;

            }

        }
    }
    public void SetCardInformation(CardData cardData)
    {

        name = cardData.name;
        attack = cardData.attack;
        defence = cardData.defense;
        effect = cardData.effect;
        cost = cardData.cost;
        cardType = cardData.cardType;
        rarity = cardData.rarity;
        IconPath = cardData.iconPath;
        description = cardData.description;
        cardType = cardData.cardType;
        isHq = cardData.isHq;
        RefreshState();
        RefreshState();
    }
    async public void BeingAttacked(cardControl source)
    {
        defence -= source.attack;
        RefreshState();
        if (defence <= 0)
        {
            this.state = CardState.destroyed;
            this.myPlace.myCard = null;
            this.cardBase.Visible = false;
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
            soundolayer.Play();
            
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
            dead();
            


        }

    }

    public void GetDefence(int defence)
    {
        if (this.defence + defence <= 99)
        {
            this.defence += defence;
        }
        else
        {
            this.defence = 99;
        }
        RefreshState();
    }

    public void GetAttack(int attack)
    {
        if (this.attack + attack <= 99)
        {
            this.attack += attack;
        }
        else
        {
            this.attack = 99;
        }
        RefreshState();
        
    }

    public string GetClostestPlace()
    {
        var allPlacesDictionary = battleField.getAllPlacesDictionary;
        double minDistance = 999999;
        string minPlaceName = "";
        var offset = new Vector2(90, 110);
        foreach (var place in allPlacesDictionary)
        {

            if (GetGlobalMousePosition().DistanceTo(place.Value.GlobalPosition + new Vector2(90, 110)) < minDistance)
            {
                minDistance = GetGlobalMousePosition().DistanceTo(place.Value.GlobalPosition + offset);
                minPlaceName = place.Value.Name;
            }
        }
        return minPlaceName;

    }

}

public enum CardState
{
    inHand,
    caught,
    placed,
    played,
    attack,
    beAttacked,
    destroyed
}
