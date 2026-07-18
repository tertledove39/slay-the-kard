using Godot;
using System;

public static class EventMaterialPoints
{
    private const string EffectPrefix = "materialPoints(";

    public static bool TryParse(string effect, out int amount)
    {
        amount = 0;
        if (!effect.StartsWith(EffectPrefix) || !effect.EndsWith(")"))
        {
            LogInvalidEffect(effect);
            return false;
        }

        string amountText = effect[EffectPrefix.Length..^1];
        if (int.TryParse(amountText, out amount) && amount >= 0)
            return true;

        amount = 0;
        LogInvalidEffect(effect);
        return false;
    }

    public static int Add(int current, int amount)
    {
        return (int)Math.Min((long)current + amount, int.MaxValue);
    }

    private static void LogInvalidEffect(string effect)
    {
        GD.PushError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] EventMaterialPoints.TryParse: 无效资源点效果 {effect}");
    }
}
