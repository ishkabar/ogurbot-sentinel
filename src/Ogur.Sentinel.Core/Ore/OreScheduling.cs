namespace Ogur.Sentinel.Core.Ore;

/// <summary>
/// Grid: reset co 30 minut, wyrównane do :28 i :58 każdej godziny.
/// </summary>
public static class OreScheduling
{
    private static readonly TimeSpan Period = TimeSpan.FromMinutes(30);

    // Dowolny punkt w czasie leżący dokładnie na siatce :28/:58 (UTC).
    private static readonly DateTimeOffset Anchor =
        new(2024, 1, 1, 0, 28, 0, TimeSpan.Zero);

    public static (DateTimeOffset WindowStart, DateTimeOffset WindowEnd) GetCurrentWindow(DateTimeOffset nowUtc)
    {
        var elapsedTicks = (nowUtc - Anchor).Ticks;
        var windowsElapsed = Math.Floor(elapsedTicks / (double)Period.Ticks);
        var windowStart = Anchor + TimeSpan.FromTicks((long)(windowsElapsed * Period.Ticks));
        return (windowStart, windowStart + Period);
    }

    public static bool IsInCurrentWindow(DateTimeOffset? timestampUtc, DateTimeOffset nowUtc)
    {
        if (timestampUtc is null) return false;
        var (windowStart, windowEnd) = GetCurrentWindow(nowUtc);
        return timestampUtc.Value >= windowStart && timestampUtc.Value < windowEnd;
    }
}