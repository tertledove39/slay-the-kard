using Godot;
using System;

public partial class ButtonControl : Button
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
    }
}
