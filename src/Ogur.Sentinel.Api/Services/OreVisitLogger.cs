namespace Ogur.Sentinel.Api.Services;

public sealed class OreVisitLogger
{
    private readonly string _logDir;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger<OreVisitLogger> _logger;

    public OreVisitLogger(ILogger<OreVisitLogger> logger)
    {
        _logger = logger;
        _logDir = Path.Combine(AppContext.BaseDirectory, "appsettings", "ore-visits");
        Directory.CreateDirectory(_logDir);
    }

    public async Task LogVisitAsync(string username, string ip)
    {
        var now = DateTimeOffset.Now;
        var fileName = $"ore-visits-{now:yyyy-MM-dd}.log";
        var filePath = Path.Combine(_logDir, fileName);

        var line = $"{now:yyyy-MM-dd HH:mm:ss}\tuser={username}\tip={ip}";

        await _writeLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(filePath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORE-VISIT-LOG] Failed to write log entry");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<(DateTimeOffset Time, string Username, string Ip)>> GetRecentAsync(TimeSpan window)
    {
        var result = new List<(DateTimeOffset, string, string)>();
        var cutoff = DateTimeOffset.Now - window;

        // Sprawdź dzisiejszy plik i, jeśli okno sięga przed północ, wczorajszy też
        var filesToCheck = new List<string>
        {
            Path.Combine(_logDir, $"ore-visits-{DateTimeOffset.Now:yyyy-MM-dd}.log")
        };

        if (DateTimeOffset.Now.Date != cutoff.Date)
        {
            filesToCheck.Add(Path.Combine(_logDir, $"ore-visits-{cutoff:yyyy-MM-dd}.log"));
        }

        foreach (var file in filesToCheck)
        {
            if (!File.Exists(file)) continue;

            var lines = await File.ReadAllLinesAsync(file);
            foreach (var line in lines)
            {
                var parsed = ParseLine(line);
                if (parsed is not null && parsed.Value.Time >= cutoff)
                {
                    result.Add(parsed.Value);
                }
            }
        }

        return result.OrderByDescending(r => r.Item1).ToList();
    }

    private static (DateTimeOffset Time, string Username, string Ip)? ParseLine(string line)
    {
        try
        {
            var parts = line.Split('\t');
            if (parts.Length < 3) return null;

            var time = DateTimeOffset.Parse(parts[0]);
            var username = parts[1].Replace("user=", "");
            var ip = parts[2].Replace("ip=", "");

            return (time, username, ip);
        }
        catch
        {
            return null;
        }
    }
}