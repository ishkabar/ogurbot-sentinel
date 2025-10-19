using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Ogur.Sentinel.Worker.Discord;

/// <summary>
/// Gates voice actions until the socket is READY and a short cooldown after a disconnect.
/// </summary>
public sealed class DiscordReadyService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordReadyService> _logger;

    private readonly TaskCompletionSource<bool> _readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // store last disconnect time as ticks (long) to avoid volatile on DateTimeOffset
    private long _lastDisconnectUtcTicks = 0;

    public DiscordReadyService(DiscordSocketClient client, ILogger<DiscordReadyService> logger)
    {
        _client = client;
        _logger = logger;

        _client.Ready += OnReady;
        _client.Disconnected += OnDisconnected;
    }

    private Task OnReady()
    {
        _logger.LogInformation("[Discord] READY");
        _readyTcs.TrySetResult(true);
        return Task.CompletedTask;
    }

    private Task OnDisconnected(Exception? ex)
    {
        Interlocked.Exchange(ref _lastDisconnectUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
        _logger.LogWarning(ex, "[Discord] DISCONNECTED");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits until first READY has fired and a small cooldown passed since last disconnect.
    /// </summary>
    public async Task WaitForStableAsync(CancellationToken ct)
    {
        // wait for initial READY
        await _readyTcs.Task.WaitAsync(ct);

        // if we recently disconnected, wait up to 2 seconds from that moment
        var lastTicks = Interlocked.Read(ref _lastDisconnectUtcTicks);
        if (lastTicks > 0)
        {
            var last = new DateTimeOffset(lastTicks, TimeSpan.Zero);
            var since = DateTimeOffset.UtcNow - last;
            var min = TimeSpan.FromSeconds(2);
            if (since < min)
                await Task.Delay(min - since, ct);
        }
    }
}
