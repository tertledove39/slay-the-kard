using Godot;
using System;

public partial class place_ : Node2D
{
    cardBase_ card;
    public cardBase_ GetMyCard() { return card; }
    public override void _Ready()
    {
        card = null;
    }



    public Vector2 GetPlaceGlobalPosition()
    {
        return GlobalPosition;
    }

    public void BondCard(cardBase_ _card)
    {
        card = _card;
    }

    public void UnbondCard()
    {
        card = null;
    }
}
