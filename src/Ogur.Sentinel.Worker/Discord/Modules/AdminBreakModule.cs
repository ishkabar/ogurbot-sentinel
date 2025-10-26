//using Discord;
//using Discord.WebSocket;

namespace Ogur.Sentinel.Worker.Discord.Modules;


public sealed class AdminBreakModule
{/*
    public SlashCommandBuilder Build() =>
        new SlashCommandBuilder()
            .WithName("admin")
            .WithDescription("Admin utilities")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("ping")
                .WithDescription("Check bot")
                .WithType(ApplicationCommandOptionType.SubCommand));

    public Task Handle(SocketSlashCommand cmd)
    {
        var sub = cmd.Data.Options.FirstOrDefault()?.Name;
        return sub == "ping"
            ? cmd.RespondAsync("pong", ephemeral: true)
            : cmd.RespondAsync("Unknown subcommand.", ephemeral: true);
    }*/
}