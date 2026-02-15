using Godot;
using System;

public partial class place_ : Control
{
    [Export] int pos = 0;
    string placeName = "";
    
    public Vector2 GetPlaceGlobalPosition()
    {
        return GlobalPosition;
    }
}
