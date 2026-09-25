using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Ogur.Sentinel.Worker.Discord.Break;

public sealed class BreakModule(BreakService service, IOptions<BreakOptions> options)
    : ApplicationCommandModule<UserCommandContext>
{
    [UserCommand("Send on break")]
    public async Task<InteractionMessageProperties> SendOnBreak(User user)
    {
        if (Context.Interaction.GuildId is not { } guildId)
            return Ephemeral("Server only.");

        var cfg = options.Value;
        var until = service.Start(guildId, user.Id, TimeSpan.FromMinutes(cfg.Minutes));

        try
        {
            await Context.Client.Rest.ModifyGuildUserAsync(guildId, user.Id, o => o.ChannelId = cfg.ChannelId);
        }
        catch (RestException)
        {
            return Ephemeral($"{user.Username} is not on a voice channel. Break is set until <t:{until.ToUnixTimeSeconds()}:t> and applies once they join.");
        }

        return Ephemeral($"{user.Username} sent on break until <t:{until.ToUnixTimeSeconds()}:t>.");
    }

    private static InteractionMessageProperties Ephemeral(string content) => new()
    {
        Content = content,
        Flags = MessageFlags.Ephemeral
    };
}