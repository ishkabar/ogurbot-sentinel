using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Core.Ore;

namespace Ogur.Sentinel.Worker.Services;

public sealed class OreCleanupWorker : BackgroundService
{
    private readonly OreDiscordPostService _orePost;
    private readonly ILogger<OreCleanupWorker> _logger;

    public OreCleanupWorker(OreDiscordPostService orePost, ILogger<OreCleanupWorker> logger)
    {
        _orePost = orePost;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var next = OreScheduling.GetNextDeletionTimeUtc(now);
            var delay = next - now;

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            try
            {
                _logger.LogInformation("[ORE-CLEANUP] Scheduled deletion at {Time}", DateTimeOffset.UtcNow);
                await _orePost.DeleteDynamicPostAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORE-CLEANUP] Failed to delete dynamic post on schedule");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}