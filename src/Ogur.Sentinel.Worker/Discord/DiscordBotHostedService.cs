using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using Ogur.Sentinel.Abstractions.Options;

namespace Ogur.Sentinel.Worker.Discord;

/// <summary>
/// NetCord-based Discord bot hosted service
/// Replaces Discord.Net DiscordSocketClient with NetCord GatewayClient
/// </summary>
public sealed class DiscordBotHostedService : BackgroundService
{
    private readonly GatewayClient _client;
    private readonly ILogger<DiscordBotHostedService> _logger;
    private readonly SettingsOptions _opts;

    private int _started;
    private CurrentUser? _currentUser;


    public DiscordBotHostedService(
        GatewayClient client,
        IOptions<SettingsOptions> opts,
        ILogger<DiscordBotHostedService> logger)
    {
        _logger = logger;
        _logger.LogTrace("[DISCORD-HOST] 🏗️ Constructor ENTRY");
        
        _client = client;
        _logger.LogTrace("[DISCORD-HOST]   Client stored");
        
        _opts = opts.Value;
        _logger.LogTrace("[DISCORD-HOST]   Options loaded, Token length: {Len}", _opts.DiscordToken?.Length ?? 0);
        
        _logger.LogTrace("[DISCORD-HOST] ✅ Constructor COMPLETE");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("[DISCORD-HOST] 📥 StartAsync ENTRY");
        
        _logger.LogTrace("[DISCORD-HOST]   Checking reentrancy guard...");
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            _logger.LogWarning("[DISCORD-HOST] ⚠️ Already started, returning");
            return;
        }
        _logger.LogTrace("[DISCORD-HOST]   Guard passed, first start");

        _logger.LogDebug("[DISCORD-HOST] 🔌 Wiring Discord event handlers...");
        WireHandlers();
        _logger.LogDebug("[DISCORD-HOST] ✅ Event handlers wired");

        _logger.LogTrace("[DISCORD-HOST] 🔍 Validating Discord token...");
        if (string.IsNullOrWhiteSpace(_opts.DiscordToken))
        {
            _logger.LogError("[DISCORD-HOST] ❌ Discord token is missing!");
            throw new InvalidOperationException("Discord token is missing.");
        }
        _logger.LogTrace("[DISCORD-HOST]   Token present, length: {Len}", _opts.DiscordToken.Length);

        _logger.LogDebug("[DISCORD-HOST] 🚀 Starting NetCord GatewayClient...");
        //await _client.StartAsync(cancellationToken: cancellationToken);
        _logger.LogDebug("[DISCORD-HOST] ✅ GatewayClient managed by hosting infrastructure");
        _logger.LogDebug("[DISCORD-HOST] ✅ NetCord GatewayClient started");

        _logger.LogTrace("[DISCORD-HOST]   Calling base.StartAsync()...");
        await base.StartAsync(cancellationToken);
        _logger.LogDebug("[DISCORD-HOST] 📤 StartAsync COMPLETE");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[DISCORD-HOST] 📥 ExecuteAsync ENTRY");

        try
        {
            _logger.LogDebug("[DISCORD-HOST] ⏳ Keeping service alive (infinite wait)...");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[DISCORD-HOST] ✅ Service cancelled (expected on shutdown)");
        }
        
        _logger.LogDebug("[DISCORD-HOST] 📤 ExecuteAsync EXIT");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("[DISCORD-HOST] 📥 StopAsync ENTRY");
        
        _logger.LogTrace("[DISCORD-HOST]   Calling base.StopAsync()...");
    
        try
        {
            await base.StopAsync(cancellationToken);
            _logger.LogDebug("[DISCORD-HOST] ✅ NetCord GatewayClient closed gracefully");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Connection not started"))
        {
            _logger.LogDebug("[DISCORD-HOST] ⏭️ Connection already closed or never started (expected on quick shutdown)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DISCORD-HOST] ⚠️ Shutdown error (non-critical)");
        }
    
        _logger.LogDebug("[DISCORD-HOST] 📤 StopAsync COMPLETE");
    }


    private void WireHandlers()
{
    _logger.LogTrace("[DISCORD-HOST] 🔌 WireHandlers ENTRY");
    
    _logger.LogTrace("[DISCORD-HOST]   Subscribing to Ready event...");
    _client.Ready += OnReady;
    
    _logger.LogTrace("[DISCORD-HOST]   Subscribing to VoiceStateUpdate event...");
    _client.VoiceStateUpdate += OnVoiceStateUpdate;
    
    _logger.LogTrace("[DISCORD-HOST]   Subscribing to VoiceServerUpdate event...");
    _client.VoiceServerUpdate += OnVoiceServerUpdate;
    
    _logger.LogTrace("[DISCORD-HOST] ✅ WireHandlers COMPLETE");
}

private ValueTask OnVoiceStateUpdate(VoiceState voiceState)
{
    _logger.LogTrace("[DISCORD-HOST] 🔔 VoiceStateUpdate event");
    _logger.LogTrace("[DISCORD-HOST]   UserId: {Id}", voiceState.UserId);

    var currentUserId = _client.Cache?.User?.Id;
    if (currentUserId.HasValue && voiceState.UserId == currentUserId.Value)
    {
        var channelId = voiceState.ChannelId?.ToString() ?? "-";
    
        _logger.LogDebug("[DISCORD-HOST] 🎤 Bot voice state changed");
        _logger.LogTrace("[DISCORD-HOST]   ChannelId: {Ch}", channelId);
        _logger.LogTrace("[DISCORD-HOST]   SessionId: '{SessionId}'", voiceState.SessionId ?? "NULL");
        _logger.LogTrace("[DISCORD-HOST]   IsSelfDeafened: {Deaf}", voiceState.IsSelfDeafened);
        _logger.LogTrace("[DISCORD-HOST]   IsSelfMuted: {Mute}", voiceState.IsSelfMuted);
    }

    return ValueTask.CompletedTask;
}

private ValueTask OnVoiceServerUpdate(VoiceServerUpdateEventArgs args)
{
    _logger.LogDebug("[DISCORD-HOST] 🔔 VoiceServerUpdate event");
    _logger.LogDebug("[DISCORD-HOST]   GuildId: {GuildId}", args.GuildId);
    
    var ep = string.IsNullOrWhiteSpace(args.Endpoint) ? "-" : args.Endpoint;
    var tok = args.Token is null ? 0 : args.Token.Length;
    
    _logger.LogDebug("[DISCORD-HOST]   Endpoint: '{Endpoint}'", ep);
    _logger.LogDebug("[DISCORD-HOST]   Token length: {Len}", tok);
    _logger.LogCritical("[DISCORD-HOST] 🚨 FULL TOKEN: '{Token}'", args.Token ?? "NULL");
    _logger.LogTrace("[DISCORD-HOST]   Token type: {Type}", args.Token?.GetType().Name ?? "NULL");
    
    return ValueTask.CompletedTask;
}

private ValueTask OnReady(ReadyEventArgs args)
{
    _logger.LogDebug("[DISCORD-HOST] 🔔 Ready event");
    _logger.LogDebug("[DISCORD-HOST]   Guilds: {Count}", _client.Cache?.Guilds.Count ?? 0);
    _logger.LogTrace("[DISCORD-HOST]   CurrentUser.Id: {Id}", _client.Cache?.User?.Id);
    _logger.LogTrace("[DISCORD-HOST]   CurrentUser.Username: {Name}", _client.Cache?.User?.Username);
    
    return ValueTask.CompletedTask;
}
}