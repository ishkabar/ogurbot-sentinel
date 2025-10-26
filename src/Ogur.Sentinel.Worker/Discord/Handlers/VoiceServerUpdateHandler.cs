using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Microsoft.Extensions.Logging;

namespace Ogur.Sentinel.Worker.Discord.Handlers;


public sealed class VoiceServerUpdateHandler : IVoiceServerUpdateGatewayHandler
{
    private readonly ILogger<VoiceServerUpdateHandler> _logger;

    public VoiceServerUpdateHandler(ILogger<VoiceServerUpdateHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(VoiceServerUpdateEventArgs args)
    {
        _logger.LogTrace("[VOICE-HANDLER] 🔔 VoiceServerUpdate");
        _logger.LogTrace("[VOICE-HANDLER]   GuildId: {GuildId}", args.GuildId);
        _logger.LogTrace("[VOICE-HANDLER]   Endpoint: {Endpoint}", args.Endpoint);
        _logger.LogTrace("[VOICE-HANDLER]   Token: {TokenLength} chars", args.Token?.Length ?? 0);

        
        return ValueTask.CompletedTask;
    }
}