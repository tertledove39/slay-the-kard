using Godot;
using System;

public partial class Attack : Label
{



    int attack = 1;
    int attackMax = 99;

    public void OnRefreshAttack(int attack)
    {
        GD.Print("Attack: " + attack);
        setAttack(attack);
    }

    public override void _Ready()
    {
        attack = 1;
    }
    public void Addattack(int x)
    {
        if (attack < attackMax)
        {
            attack += x;
        }
        else
        {
            attack = attackMax;
        }
        Text = attack.ToString();
    }

    public void setAttack(int x)
    {
        if (x < attackMax)
        {
            attack = x;
        }
        else
        {
            attack = attackMax;
        }
        Text = attack.ToString();
    }


}
