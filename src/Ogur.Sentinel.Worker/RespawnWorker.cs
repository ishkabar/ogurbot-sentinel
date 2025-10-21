using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Worker.Services;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Abstractions.Options;

namespace Ogur.Sentinel.Worker;

public sealed class RespawnWorker : BackgroundService
{
    private readonly RespawnSchedulerService _scheduler;
    private readonly RespawnState _state;  
    private readonly WikiSyncService _wikiSync;
    private readonly IOptions<RespawnOptions> _opts;
    private readonly ILogger<RespawnWorker> _logger;

    public RespawnWorker(
        RespawnSchedulerService scheduler, 
        RespawnState state, 
        WikiSyncService wikiSync,
        IOptions<RespawnOptions> opts,
        ILogger<RespawnWorker> logger)
    {
        _scheduler = scheduler;
        _state = state;
        _wikiSync = wikiSync;
        _opts = opts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _ = Task.Run(() => SyncLoopAsync(stoppingToken), stoppingToken);

    DateTimeOffset? lastTrigger10 = null;
    DateTimeOffset? lastTrigger2h = null;

    _logger.LogInformation("[RespawnWorker] Started");

    while (!stoppingToken.IsCancellationRequested)
    {
        var now = DateTimeOffset.Now;
        var (next10, next2) = _scheduler.ComputeNext(now);
        
        var delay10 = next10 - now;
        var delay2 = next2 - now;
        
        var nextDelay = TimeSpan.FromMilliseconds(
            Math.Max(100, Math.Min(delay10.TotalMilliseconds, delay2.TotalMilliseconds))
        );

        _logger.LogInformation("[RespawnWorker] Next: 10m at {Next10} (in {Delay10}), 2h at {Next2} (in {Delay2}). Status: 10m={En10}, 2h={En2}", 
            next10.ToLocalTime().ToString("HH:mm:ss"), 
            FormatTimeSpan(delay10),
            next2.ToLocalTime().ToString("HH:mm:ss"), 
            FormatTimeSpan(delay2),
            _state.Enabled10m ? "ON" : "OFF", 
            _state.Enabled2h ? "ON" : "OFF");

        await Task.Delay(nextDelay, stoppingToken);

        now = DateTimeOffset.Now;
        
        // PRIORYTET: Najpierw sprawdź 2h trigger
        bool triggered2h = false;
        if (now >= next2 && next2 != lastTrigger2h && _state.Enabled2h)
        {
            _logger.LogWarning("[RespawnWorker] 🔔 TRIGGERING 2h respawn at {Time}", now.ToLocalTime().ToString("HH:mm:ss"));
            lastTrigger2h = next2;
            triggered2h = true;
            
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
        else if (now >= next2 && next2 != lastTrigger2h && !_state.Enabled2h)
        {
            _logger.LogInformation("[RespawnWorker] ⏭️ Skipping 2h trigger (disabled)");
            lastTrigger2h = next2;
        }

        // Sprawdź kolizję - czy oba triggery są w tym samym ~30s oknie
        var timeDiff = Math.Abs((next10 - next2).TotalSeconds);
        bool isCollision = timeDiff < 30;

        // Potem sprawdź 10m trigger (z pominięciem przy kolizji z 2h)
        if (now >= next10 && next10 != lastTrigger10 && _state.Enabled10m)
        {
            if (triggered2h && isCollision)
            {
                // Kolizja - 2h ma priorytet, pomijamy 10m
                _logger.LogInformation("[RespawnWorker] ⏭️ Skipping 10m trigger (collision with 2h, 2h has priority)");
                lastTrigger10 = next10;
            }
            else
            {
                // Normalny trigger 10m
                _logger.LogWarning("[RespawnWorker] 🔔 TRIGGERING 10m respawn at {Time}", now.ToLocalTime().ToString("HH:mm:ss"));
                lastTrigger10 = next10;
                
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
        }
        else if (now >= next10 && next10 != lastTrigger10 && !_state.Enabled10m)
        {
            _logger.LogInformation("[RespawnWorker] ⏭️ Skipping 10m trigger (disabled)");
            lastTrigger10 = next10;
        }
    }
}
    
    private async Task SyncLoopAsync(CancellationToken ct)
    {
        if (!_opts.Value.SyncEnabled)
        {
            _logger.LogInformation("[WikiSync] Sync loop disabled");
            return;
        }

        _logger.LogInformation("[WikiSync] Sync loop started (interval: {Interval}m)", 
            _opts.Value.SyncIntervalMinutes);

        await _wikiSync.SyncAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_opts.Value.SyncIntervalMinutes), ct);
                await _wikiSync.SyncAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WikiSync] Loop error");
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