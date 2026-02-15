using Godot;
using System;

public partial class place_ : Control
{
    string placeName = "";
    [Export] int pos = 0;
    public Vector2 GetPlaceGlobalPosition()
    {
        return GlobalPosition;
    }
}
