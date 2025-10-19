using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Worker.Services;

namespace Ogur.Sentinel.Worker;

public sealed class RespawnWorker : BackgroundService
{
    private readonly RespawnSchedulerService _scheduler;
    private readonly ILogger<RespawnWorker> _logger;

    public RespawnWorker(RespawnSchedulerService scheduler, ILogger<RespawnWorker> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? lastTrigger10 = null;
        DateTimeOffset? lastTrigger2h = null;

        _logger.LogInformation("[RespawnWorker] Started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            var (next10, next2) = _scheduler.ComputeNext(now);
            
            // Ile czekać do najbliższego triggera
            var delay10 = next10 - now;
            var delay2 = next2 - now;
            
            // Czekaj do najbliższego (min 100ms żeby nie spinować)
            var nextDelay = TimeSpan.FromMilliseconds(
                Math.Max(100, Math.Min(delay10.TotalMilliseconds, delay2.TotalMilliseconds))
            );

            _logger.LogInformation("[RespawnWorker] Next: 10m at {Next10} (in {Delay10}), 2h at {Next2} (in {Delay2})", 
                next10.ToLocalTime().ToString("HH:mm:ss"), 
                FormatTimeSpan(delay10),
                next2.ToLocalTime().ToString("HH:mm:ss"), 
                FormatTimeSpan(delay2));

            await Task.Delay(nextDelay, stoppingToken);

            // Po delay, sprawdź dokładny czas
            now = DateTimeOffset.Now;

            // Trigger 10m jeśli czas minął I nie był już triggerowany
            if (now >= next10 && next10 != lastTrigger10)
            {
                _logger.LogWarning("[RespawnWorker] 🔔 TRIGGERING 10m respawn at {Time}", now.ToLocalTime().ToString("HH:mm:ss"));
                lastTrigger10 = next10;
                
                // Fire and forget - nie blokuj głównej pętli
                _ = Task.Run(async () => 
                {
                    try 
                    { 
                        await _scheduler.PlayAsync(true, stoppingToken); 
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogError(ex, "[RespawnWorker] 10m trigger failed"); 
                    }
                }, stoppingToken);
            }

            // Trigger 2h jeśli czas minął I nie był już triggerowany
            if (now >= next2 && next2 != lastTrigger2h)
            {
                _logger.LogWarning("[RespawnWorker] 🔔 TRIGGERING 2h respawn at {Time}", now.ToLocalTime().ToString("HH:mm:ss"));
                lastTrigger2h = next2;
                
                _ = Task.Run(async () => 
                {
                    try 
                    { 
                        await _scheduler.PlayAsync(false, stoppingToken); 
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogError(ex, "[RespawnWorker] 2h trigger failed"); 
                    }
                }, stoppingToken);
            }
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }
}