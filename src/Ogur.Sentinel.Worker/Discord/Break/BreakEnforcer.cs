using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;

namespace Ogur.Sentinel.Worker.Discord.Break;

public sealed class BreakEnforcer(
    GatewayClient client,
    BreakService service,
    IOptions<BreakOptions> options,
    ILogger<BreakEnforcer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        client.VoiceStateUpdate += OnVoiceStateUpdate;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        client.VoiceStateUpdate -= OnVoiceStateUpdate;
        return Task.CompletedTask;
    }

    private async ValueTask OnVoiceStateUpdate(VoiceState state)
    {
        var target = options.Value.ChannelId;

        if (state.ChannelId is not { } channelId || channelId == target)
            return;

        if (!service.IsActive(state.GuildId, state.UserId))
            return;

        try
        {
            await client.Rest.ModifyGuildUserAsync(state.GuildId, state.UserId, o => o.ChannelId = target);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to return user {UserId} to break channel", state.UserId);
        }
    }
}