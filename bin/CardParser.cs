using Godot;
using System;
using System.Linq;

public static class CardParser
{
    public static CardTypes GetTypes(string type)
    {
        switch (type)
        {
            case "Tank":
                return CardTypes.Tank;
            case "Artillery":
                return CardTypes.Artillery;
            case "Plane":
                return CardTypes.Plane;
            case "Bomber":
                return CardTypes.Bomber;
            case "Command":
                return CardTypes.Command;
            default:
                return CardTypes.Infantry;
        }
    }

    public static Rarity GetRarity(string rare)
    {
        switch (rare.ToLower())
        {
            case "common":
                return Rarity.Common;
            case "rare":
                return Rarity.Rare;
            case "epic":
                return Rarity.Epic;
            case "unobtainable":
                return Rarity.Unobtainable;
            default:
                return Rarity.Legendary;
        }
    }

    public static TargetType GetTargetType(string v)
    {
        if (System.Enum.TryParse<TargetType>(v, ignoreCase: true, out var result))
        {
            return result;
        }
        GD.Print($"[GetTargetType] parse failed: '{v}' -> fallback anyTarget");
        return TargetType.anyTarget;
    }

    public static UnitTraits GetTraitList(string s)
    {
        try
        {
            var traitList = s.Split(',').Select(x => (UnitTraits)Enum.Parse(typeof(UnitTraits), x.Trim())).ToList();
            UnitTraits outTrait = UnitTraits.None;
            foreach (var trait in traitList)
            {
                outTrait |= trait;
            }
            return outTrait;
        }
        catch (Exception)
        {
        }
        return UnitTraits.None;
    }
}
