using Godot;
using System;

public partial class UnitType : Sprite2D
{
    CardTypes cardType = CardTypes.Infantry;
    public void OnRefreshUnitType(int x)
    {
        cardType = (CardTypes)x;
        switch (cardType)
        {
            case CardTypes.Tank:
                Texture =GD.Load<Texture2D>("res://cards/tank.png");break;
            case CardTypes.Plane:
                Texture =GD.Load<Texture2D>("res://cards/fighter.png");break;
            case CardTypes.Bomber:
                Texture =GD.Load<Texture2D>("res://cards/bomber.png");break;
            case CardTypes.Artillery:
                Texture =GD.Load<Texture2D>("res://cards/arlitery.png");break;
            case CardTypes.Command:
                Texture =GD.Load<Texture2D>("res://cards/command.png");break;
            default:
                Texture =GD.Load<Texture2D>("res://cards/inf.png");break;
        }
    }
}
