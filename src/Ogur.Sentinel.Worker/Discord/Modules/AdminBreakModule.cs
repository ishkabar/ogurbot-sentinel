using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Ogur.Sentinel.Worker.Discord.Modules;

[SlashCommand("admin", "Admin utilities")]
public sealed class AdminBreakModule : ApplicationCommandModule<ApplicationCommandContext>
{
    [SubSlashCommand("ping", "Check bot")]
    public InteractionMessageProperties Ping() => new()
    {
        Content = "pong",
        Flags = MessageFlags.Ephemeral
    };
}