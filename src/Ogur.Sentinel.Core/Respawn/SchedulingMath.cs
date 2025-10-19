using System.Globalization;


namespace Ogur.Sentinel.Core.Respawn;


/// <summary>
/// Core math for aligned timers with base HH:MM[:SS] and lead seconds.
/// Port of _parse_hhmm and _next_aligned from Python. :contentReference[oaicite:7]{index=7}

/// </summary>
public static class SchedulingMath
{
    /// <summary>Parses "HH:MM" or "HH:MM:SS". Throws on invalid format.</summary>
    public static TimeOnly ParseHhmm(string hhmm)
    {
        var parts = hhmm.Split(':');
        if (parts.Length is not (2 or 3)) throw new FormatException("Invalid time format");
        var h = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var m = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var s = parts.Length == 3 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
        if (h is < 0 or > 23 || m is < 0 or > 59 || s is < 0 or > 59) throw new FormatException("Invalid time range");
        return new TimeOnly(h, m, s);
    }

    /// <summary>
    /// Returns the next trigger instant aligned to the given period, shifted by lead.
    /// All in local time (caller decides the timezone).
    /// </summary>
    public static DateTimeOffset NextAligned(DateTimeOffset nowLocal, TimeOnly baseLocal, TimeSpan period, TimeSpan lead)
    {
        var baseToday = new DateTimeOffset(
            nowLocal.Year, nowLocal.Month, nowLocal.Day,
            baseLocal.Hour, baseLocal.Minute, 0,
            nowLocal.Offset);

        // If base today is in the future, go back one day to guarantee floor
        var baseFloor = baseToday > nowLocal ? baseToday.AddDays(-1) : baseToday;

        var elapsed = nowLocal - baseFloor;
        var steps = (int)Math.Floor(elapsed / period) + 1;
        var nextTick = baseFloor + TimeSpan.FromTicks(period.Ticks * steps);

        var candidate = nextTick - lead;
        if (candidate <= nowLocal)
            candidate = candidate + period;

        return candidate;
    }
}