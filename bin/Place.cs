using Godot;
using System;

public partial class Place : Node2D
{

    [Export] public int line = 0;
    public cardControl myCard = null;
    string selfName;
    

    public override void _Ready()
    {
        ZIndex = 0;
        
        selfName = this.Name;
    }


}
