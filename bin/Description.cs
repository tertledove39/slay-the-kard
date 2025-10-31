using Godot;
using System;

public partial class Description : RichTextLabel
{
    string description = "";
    public void OnRefreshDescription(string description)
    {
        this.description = description;
        Text = this.description;
    }
}
