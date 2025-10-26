using NetCord.Gateway;
using Microsoft.Extensions.Logging;

namespace Ogur.Sentinel.Worker.Discord;

/// <summary>
/// Gates voice actions until the socket is READY and a short cooldown after a disconnect.
/// NetCord version - without Disconnected event (NetCord doesn't have it)
/// </summary>
public sealed class DiscordReadyService
{
    private readonly GatewayClient _client;
    private readonly ILogger<DiscordReadyService> _logger;

    private readonly TaskCompletionSource<bool> _readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _lastDisconnectUtcTicks = 0;

    public DiscordReadyService(GatewayClient client, ILogger<DiscordReadyService> logger)
    {
        _logger = logger;
        _logger.LogTrace("[READY-SVC] 🏗️ Constructor ENTRY");
        
        _client = client;
        _logger.LogTrace("[READY-SVC]   Client stored");
        
        _logger.LogTrace("[READY-SVC]   Subscribing to Ready event...");
        _client.Ready += OnReady;
        
        // NOTE: NetCord doesn't have Disconnected event
        // We track disconnects differently or remove this feature
        _logger.LogTrace("[READY-SVC]   ⚠️ NetCord doesn't have Disconnected event - cooldown feature disabled");
        
        _logger.LogTrace("[READY-SVC]   TaskCompletionSource created");
        _logger.LogTrace("[READY-SVC] ✅ Constructor COMPLETE");
    }

    private ValueTask OnReady(ReadyEventArgs args)
    {
        _logger.LogDebug("[READY-SVC] 🔔 Ready event fired");
        
        _logger.LogTrace("[READY-SVC]   Completing TaskCompletionSource...");
        var result = _readyTcs.TrySetResult(true);
        _logger.LogTrace("[READY-SVC]   TrySetResult returned: {Result}", result);
        
        if (!result)
        {
            _logger.LogTrace("[READY-SVC]   TaskCompletionSource was already completed");
        }
        
        _logger.LogDebug("[READY-SVC] ✅ Ready event handled");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Waits until first READY has fired and a small cooldown passed since last disconnect.
    /// NOTE: Disconnect tracking removed as NetCord doesn't have Disconnected event
    /// </summary>
    public async Task WaitForStableAsync(CancellationToken ct)
    {
        _logger.LogDebug("[READY-SVC] 📥 WaitForStableAsync ENTRY");
        
        // Wait for initial READY
        _logger.LogTrace("[READY-SVC]   Checking if READY has fired...");
        _logger.LogTrace("[READY-SVC]   Task.IsCompleted: {Completed}", _readyTcs.Task.IsCompleted);
        
        if (!_readyTcs.Task.IsCompleted)
        {
            _logger.LogDebug("[READY-SVC]   ⏳ Waiting for READY event...");
            await _readyTcs.Task.WaitAsync(ct);
            _logger.LogDebug("[READY-SVC]   ✅ READY event received");
        }
        else
        {
            _logger.LogTrace("[READY-SVC]   READY already fired");
        }

        // NOTE: Disconnect cooldown feature removed - NetCord doesn't have Disconnected event
        // If you need this, you'll have to track connection state differently
        // For example: use try/catch around voice operations and add manual delays
        
        _logger.LogDebug("[READY-SVC] 📤 WaitForStableAsync COMPLETE - Discord is stable");
    }
    
    /// <summary>
    /// Optional: Manual disconnect tracking for voice operations
    /// Call this when you detect a disconnect (e.g., in voice error handling)
    /// </summary>
    public void RecordDisconnect()
    {
        var now = DateTimeOffset.UtcNow;
        _logger.LogDebug("[READY-SVC] 📝 Recording manual disconnect");
        _logger.LogTrace("[READY-SVC]   Current time: {Time}", now);
        
        var oldTicks = Interlocked.Exchange(ref _lastDisconnectUtcTicks, now.UtcTicks);
        _logger.LogTrace("[READY-SVC]   Old timestamp: {Old}", oldTicks);
        _logger.LogTrace("[READY-SVC]   New timestamp: {New}", now.UtcTicks);
    }
    
    /// <summary>
    /// Optional: Check if enough time passed since last disconnect
    /// Use this in voice operations if you need disconnect cooldown
    /// </summary>
    public async Task WaitForDisconnectCooldownAsync(CancellationToken ct)
    {
        _logger.LogTrace("[READY-SVC]   Checking for recent disconnect...");
        var lastTicks = Interlocked.Read(ref _lastDisconnectUtcTicks);
        _logger.LogTrace("[READY-SVC]   Last disconnect ticks: {Ticks}", lastTicks);
        
        if (lastTicks > 0)
        {
            var last = new DateTimeOffset(lastTicks, TimeSpan.Zero);
            var now = DateTimeOffset.UtcNow;
            var since = now - last;
            var min = TimeSpan.FromSeconds(2);
            
            _logger.LogDebug("[READY-SVC]   Last disconnect: {Time}", last);
            _logger.LogDebug("[READY-SVC]   Current time: {Time}", now);
            _logger.LogDebug("[READY-SVC]   Time since disconnect: {Since}", since);
            _logger.LogDebug("[READY-SVC]   Minimum cooldown: {Min}", min);
            
            if (since < min)
            {
                var waitTime = min - since;
                _logger.LogDebug("[READY-SVC]   ⏳ Cooldown needed: waiting {Wait}...", waitTime);
                await Task.Delay(waitTime, ct);
                _logger.LogDebug("[READY-SVC]   ✅ Cooldown complete");
            }
            else
            {
                _logger.LogTrace("[READY-SVC]   Cooldown not needed (enough time passed)");
            }
        }
        else
        {
            _logger.LogTrace("[READY-SVC]   No disconnect recorded");
        }
    }
}