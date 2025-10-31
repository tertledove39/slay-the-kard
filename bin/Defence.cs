using Godot;
using System;

public partial class Defence : Label
{
    int defence = 1;
    public void OnRefreshDefence(int x)
    {
        setDefence(x);
    }

    public void setDefence(int x)
    {
        defence = x;
        Text = defence.ToString();
    }
}
