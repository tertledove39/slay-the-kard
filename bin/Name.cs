using Godot;
using System;

public partial class Name : Label
{
    public void OnRefreshName(string input)
    {
        Text = input;
    }
}
