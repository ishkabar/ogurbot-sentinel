using System;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Abstractions.Options;

namespace Ogur.Sentinel.Worker.Discord;

public sealed class DiscordBotHostedService : BackgroundService
{
    private readonly DiscordClient _client;
    private readonly ILogger<DiscordBotHostedService> _logger;
    private readonly SettingsOptions _opts;

    public DiscordBotHostedService(
        DiscordClient client,
        IOptions<SettingsOptions> opts,
        ILogger<DiscordBotHostedService> logger)
    {
        _client = client;
        _logger = logger;
        _opts = opts.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // DSharpPlus 5.0 nightly-02405 - eventy się nazywają inaczej
        _client.Ready += (s, e) =>
        {
            _logger.LogInformation("[Discord] Ready");
            return Task.CompletedTask;
        };

        _client.VoiceStateUpdated += (s, e) =>
        {
            var beforeCh = e.Before?.Channel?.Id.ToString() ?? "-";
            var afterCh = e.After?.Channel?.Id.ToString() ?? "-";
            _logger.LogInformation("[VOICE] UserVoiceStateUpdated {User} {Before} -> {After}",
                e.User.Username, beforeCh, afterCh);
            return Task.CompletedTask;
        };

        _client.VoiceServerUpdated += (s, e) =>
        {
            _logger.LogInformation("[VOICE] VoiceServerUpdated guild={GuildId} endpoint={Endpoint}",
                e.Guild.Id, e.Endpoint ?? "null");
            return Task.CompletedTask;
        };

        await _client.ConnectAsync();
        _logger.LogInformation("[Discord] Connected");

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discord] Disconnect failed");
        }

        _client.Dispose();
        await base.StopAsync(cancellationToken);
    }
}