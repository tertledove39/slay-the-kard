using Godot;
using System;

public partial class Cost : Label
{
    int cost = 1;
    public void OnRefreshCost(int cost)
    {
        SetCost(cost);
    }

    public void SetCost(int x)
    {
        this.cost = x;
        Text = cost.ToString();
    }
}
