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
}