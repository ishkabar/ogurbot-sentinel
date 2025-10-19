namespace Ogur.Sentinel.Core.Time;


/// <summary>
/// Utilities for day-difference and pretty remaining string.
/// Mirrors Python logic from helpers/timeparse.py and helpers/helpers.py. 
/// 

/// </summary>
public static class TimeUtil
{
    public static int DaysUntil(DateTimeOffset targetUtc)
    {
        var todayUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tgt = new DateTimeOffset(targetUtc.UtcDateTime.Date, TimeSpan.Zero);
        return Math.Max(0, (tgt - todayUtc).Days);
    }

    public static string RemainingPretty(DateTimeOffset endsAtUtc)
    {
        var delta = endsAtUtc - DateTimeOffset.UtcNow;
        if (delta < TimeSpan.Zero) delta = TimeSpan.Zero;

        var mins = (int)(delta.TotalSeconds / 60);
        var secs = (int)(delta.TotalSeconds % 60);
        return mins > 0 ? $"{mins} min {secs} s" : $"{secs} s";
    }
}