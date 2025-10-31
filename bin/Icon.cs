using Godot;
using System;

public partial class Icon : Sprite2D
{
    string iconPath = "";
    public void OnRefreshIconPath(string path)
    {
        iconPath = path;
        if (iconPath == "")
        {
            Texture = null;
        }
        else Texture = GD.Load<Texture2D>(iconPath);
            
        QueueRedraw();
        GD.Print(Texture);
        GD.Print("refresh icon");
    }
}
