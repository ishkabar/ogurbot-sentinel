namespace Ogur.Sentinel.Api.Services;

public sealed class OreMarkLogger
{
    private readonly string _logDir;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger<OreMarkLogger> _logger;

    public OreMarkLogger(ILogger<OreMarkLogger> logger)
    {
        _logger = logger;
        _logDir = Path.Combine(AppContext.BaseDirectory, "appsettings", "ore-logs");
        Directory.CreateDirectory(_logDir);
    }

    public async Task LogMarkAsync(double x, double y, string username, string userId, string ip)
    {
        var now = DateTimeOffset.Now;
        var fileName = $"ore-marks-{now:yyyy-MM-dd}.log";
        var filePath = Path.Combine(_logDir, fileName);

        var line = $"{now:yyyy-MM-dd HH:mm:ss}\tuser={username} ({userId})\tip={ip}\tx={x:F2}\ty={y:F2}";

        await _writeLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(filePath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORE-MARK-LOG] Failed to write log entry");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<(DateTimeOffset Time, string Username, string UserId, double X, double Y)>> GetRecentAsync(int count)
    {
        var result = new List<(DateTimeOffset, string, string, double, double)>();

        // Sprawdź dzisiejszy i wczorajszy plik, żeby złapać 6 ostatnich nawet tuż po północy
        var files = new[]
        {
            Path.Combine(_logDir, $"ore-marks-{DateTimeOffset.Now:yyyy-MM-dd}.log"),
            Path.Combine(_logDir, $"ore-marks-{DateTimeOffset.Now.AddDays(-1):yyyy-MM-dd}.log")
        };

        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;

            var lines = await File.ReadAllLinesAsync(file);
            foreach (var line in lines)
            {
                var parsed = ParseLine(line);
                if (parsed is not null)
                {
                    result.Add(parsed.Value);
                }
            }
        }

        return result.OrderByDescending(r => r.Item1).Take(count).ToList();
    }

    private static (DateTimeOffset Time, string Username, string UserId, double X, double Y)? ParseLine(string line)
    {
        try
        {
            var parts = line.Split('\t');
            if (parts.Length < 4) return null;

            var time = DateTimeOffset.Parse(parts[0]);

            var userPart = parts[1].Replace("user=", "");
            var openParen = userPart.IndexOf('(');
            var username = openParen > 0 ? userPart[..openParen].Trim() : userPart;
            var userId = openParen > 0 ? userPart[(openParen + 1)..].TrimEnd(')') : "";

            var xStr = parts[3].Replace("x=", "");
            var yStr = parts[4].Replace("y=", "");

            var x = double.Parse(xStr, System.Globalization.CultureInfo.InvariantCulture);
            var y = double.Parse(yStr, System.Globalization.CultureInfo.InvariantCulture);

            return (time, username, userId, x, y);
        }
        catch
        {
            return null;
        }
    }
}