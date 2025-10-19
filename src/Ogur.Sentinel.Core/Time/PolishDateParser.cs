using System.Globalization;
using System.Text.RegularExpressions;

namespace Ogur.Sentinel.Core.Time;


/// <summary>
/// Polish human date parser → UTC midnight in provided TZ.
/// Based on Python helpers/timeparse.py. :contentReference[oaicite:4]{index=4}
/// </summary>
public static class PolishDateParser
{
    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stycznia"] = 1, ["lutego"] = 2, ["marca"] = 3, ["kwietnia"] = 4, ["maja"] = 5, ["czerwca"] = 6,
        ["lipca"] = 7, ["sierpnia"] = 8, ["września"] = 9, ["wrzesnia"] = 9,
        ["października"] = 10, ["pazdziernika"] = 10, ["listopada"] = 11, ["grudnia"] = 12
    };

    private static readonly Regex Iso = new(@"^\s*(\d{4})-(\d{1,2})-(\d{1,2})\s*$", RegexOptions.Compiled);
    private static readonly Regex Dmy = new(@"^\s*(\d{1,2})[./-](\d{1,2})[./-](\d{4})\s*$", RegexOptions.Compiled);
    private static readonly Regex WordMon = new(@"^\s*(\d{1,2})\s+([A-Za-zĄĆĘŁŃÓŚŹŻąćęłńóśźż]+)\s+(\d{4})\s*$", RegexOptions.Compiled);

    public static DateTimeOffset? ParseHumanDate(string text, string tzId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        text = text.Trim();

        // 1) YYYY-MM-DD
        var m = Iso.Match(text);
        if (m.Success)
        {
            var y = int.Parse(m.Groups[1].Value);
            var mo = int.Parse(m.Groups[2].Value);
            var d = int.Parse(m.Groups[3].Value);
            return ToUtcMidnight(y, mo, d, tz);
        }

        // 2) DD.MM.YYYY / DD/MM/YYYY / DD-MM-YYYY
        m = Dmy.Match(text);
        if (m.Success)
        {
            var d = int.Parse(m.Groups[1].Value);
            var mo = int.Parse(m.Groups[2].Value);
            var y = int.Parse(m.Groups[3].Value);
            return ToUtcMidnight(y, mo, d, tz);
        }

        // 3) "30 września 2025"
        m = WordMon.Match(text);
        if (m.Success)
        {
            var d = int.Parse(m.Groups[1].Value);
            var monName = m.Groups[2].Value;
            var y = int.Parse(m.Groups[3].Value);
            if (Months.TryGetValue(monName, out var mo))
                return ToUtcMidnight(y, mo, d, tz);
        }

        // 4) Fallback: "30 09 2025"
        var parts = text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts.All(p => int.TryParse(p, out _)))
        {
            var d = int.Parse(parts[0]);
            var mo = int.Parse(parts[1]);
            var y = int.Parse(parts[2]);
            return ToUtcMidnight(y, mo, d, tz);
        }

        return null;
    }

    private static DateTimeOffset ToUtcMidnight(int y, int mo, int d, TimeZoneInfo tz)
    {
        var local = new DateTime(y, mo, d, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(local);
        // interpret as time in tz at midnight, then convert to UTC
        var dto = new DateTimeOffset(local, offset);
        return dto.ToUniversalTime();
    }
}