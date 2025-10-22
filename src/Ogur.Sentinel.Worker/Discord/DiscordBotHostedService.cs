using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Abstractions.Options;

namespace Ogur.Sentinel.Worker.Discord;

public sealed class DiscordBotHostedService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordBotHostedService> _logger;
    private readonly SettingsOptions _opts;

    private int _started; // simple reentrancy guard

    public DiscordBotHostedService(
        DiscordSocketClient client,
        IOptions<SettingsOptions> opts,
        ILogger<DiscordBotHostedService> logger)
    {
        _client = client;
        _logger = logger;
        _opts = opts.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        WireHandlers();

        if (string.IsNullOrWhiteSpace(_opts.DiscordToken))
            throw new InvalidOperationException("Discord token is missing.");

        await _client.LoginAsync(TokenType.Bot, _opts.DiscordToken);
        await _client.StartAsync();

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += msg =>
        {
            if (msg.Source?.Contains("Voice", StringComparison.OrdinalIgnoreCase) == true ||
                msg.Message?.Contains("VOICE", StringComparison.OrdinalIgnoreCase) == true ||
                msg.Message?.Contains("voice", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning("[GATEWAY-RAW] {Source}: {Message}", msg.Source, msg.Message);
            }
            return Task.CompletedTask;
        };

        try
        {
            // keep the service alive until cancellation
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discord] Stop/Logout failed");
        }

        await base.StopAsync(cancellationToken);
    }

    private void WireHandlers()
    {
        _client.Log += OnDiscordLog;
        _client.Connected += OnConnected;
        _client.Disconnected += OnDisconnected;
        _client.Ready += OnReady;
        
        _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
        _client.VoiceServerUpdated += OnVoiceServerUpdated;
        _client.LatencyUpdated += (old, newLatency) => Task.CompletedTask;
        
        (_client as DiscordSocketClient)!.Log += msg =>
        {
            if (msg.Message?.Contains("\"t\":\"VOICE_SERVER_UPDATE\"") == true)
            {
                _logger.LogCritical("[RAW-GATEWAY] VOICE_SERVER_UPDATE payload: {Msg}", msg.Message);
            }
            return Task.CompletedTask;
        };
    }

    private Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user.Id == _client.CurrentUser?.Id)
        {
            var bCh = before.VoiceChannel?.Id.ToString() ?? "-";
            var aCh = after.VoiceChannel?.Id.ToString() ?? "-";
            _logger.LogInformation("[VOICE] UserVoiceStateUpdated (bot) {BeforeChannel} -> {AfterChannel}", bCh, aCh);
        }
        return Task.CompletedTask;
    }

    private Task OnVoiceServerUpdated(SocketVoiceServer vsu)
    {
        // Endpoint + długość tokena – potwierdza, że dostaliśmy VOICE_SERVER_UPDATE
        var ep = string.IsNullOrWhiteSpace(vsu.Endpoint) ? "-" : vsu.Endpoint;
        var tok = vsu.Token is null ? 0 : vsu.Token.Length;
        _logger.LogInformation("[VOICE] VoiceServerUpdated guild={GuildId} endpoint={Endpoint} tokenLen={TokenLen}",
            vsu.Guild.Id, ep, tok);
        _logger.LogWarning("[VOICE] FULL TOKEN: '{Token}'", vsu.Token ?? "NULL");
        return Task.CompletedTask;
    }

    
    private Task OnReady()
    {
        _logger.LogInformation("[Discord] Ready. Guilds={Guilds}, Latency={Latency}ms",
            _client.Guilds.Count, _client.Latency);
        return Task.CompletedTask;
    }

    private Task OnConnected()
    {
        _logger.LogInformation("[Discord] Connected. Latency={Latency}ms", _client.Latency);
        return Task.CompletedTask;
    }

    private Task OnDisconnected(Exception? ex)
    {
        if (ex is null)
            _logger.LogWarning("[Discord] Disconnected (no exception). Will auto-reconnect.");
        else
            _logger.LogWarning(ex, "[Discord] Disconnected. Will auto-reconnect.");

        return Task.CompletedTask;
    }

    private Task OnDiscordLog(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error    => LogLevel.Error,
            LogSeverity.Warning  => LogLevel.Warning,
            LogSeverity.Info     => LogLevel.Information,
            LogSeverity.Verbose  => LogLevel.Debug,
            LogSeverity.Debug    => LogLevel.Trace,
            _ => LogLevel.Information
        };

        // ToString() includes Source, Severity, Message, and Exception message.
        _logger.Log(level, "[Discord] {Text}", msg.ToString());

        if (msg.Exception is not null)
            _logger.Log(level, msg.Exception, "[Discord] Exception");

        return Task.CompletedTask;
    }
}