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
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            var (next10, next2) = _scheduler.ComputeNext(now);
            var delay10 = next10 - now;
            var delay2 = next2 - now;
            var nextDelay = TimeSpan.FromSeconds(Math.Min(delay10.TotalSeconds, delay2.TotalSeconds));

            _logger.LogInformation("Next trigger in {Delay}", nextDelay);
            await Task.Delay(nextDelay, stoppingToken);

            if (DateTimeOffset.Now >= next10) await _scheduler.PlayAsync(true, stoppingToken);
            if (DateTimeOffset.Now >= next2) await _scheduler.PlayAsync(false, stoppingToken);
        }
    }
}