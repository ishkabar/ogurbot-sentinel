using Microsoft.Extensions.Logging;
//using Discord;
//using Discord.WebSocket;
using Ogur.Sentinel.Abstractions.Leaves;
using Ogur.Sentinel.Worker.Services;

namespace Ogur.Sentinel.Worker.Discord.Modules;


public sealed class LeaveModule
{/*
    private readonly LeaveService _service;
    private readonly ILogger<LeaveModule> _logger;

    public LeaveModule(LeaveService service, ILogger<LeaveModule> logger)
    {
        _service = service;
        _logger = logger;
    }

    public SlashCommandBuilder Build()
    {
        var b = new SlashCommandBuilder()
            .WithName("leave")
            .WithDescription("Leave management");

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("set")
            .WithDescription("Set return date (UTC ISO or yyyy-MM-dd HH:mm)")
            .AddOption("nick", ApplicationCommandOptionType.String, "Game nick", isRequired: true)
            .AddOption("return_at_utc", ApplicationCommandOptionType.String, "UTC return time", isRequired: true)
            .AddOption("reason", ApplicationCommandOptionType.String, "Optional reason", isRequired: false)
            .WithType(ApplicationCommandOptionType.SubCommand));

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("clear")
            .WithDescription("Clear user's leave")
            .WithType(ApplicationCommandOptionType.SubCommand));

        return b;
    }

    public async Task Handle(SocketSlashCommand cmd)
    {
        var sub = cmd.Data.Options.FirstOrDefault()?.Name;
        switch (sub)
        {
            case "set":
            {
                var nick = (string)cmd.Data.Options.First().Options.First(x => x.Name == "nick").Value!;
                var when = (string)cmd.Data.Options.First().Options.First(x => x.Name == "return_at_utc").Value!;
                var reason = (string?)cmd.Data.Options.First().Options.FirstOrDefault(x => x.Name == "reason")?.Value;

                if (!DateTimeOffset.TryParse(when, out var dto))
                {
                    await cmd.RespondAsync("Invalid datetime format.", ephemeral: true);
                    return;
                }

                var rec = new LeaveRecord
                {
                    GuildId = cmd.GuildId ?? 0,
                    ChannelId = cmd.Channel.Id,
                    MessageId = 0,
                    UserId = cmd.User.Id,
                    GameNick = nick,
                    ReturnAtUtc = dto.ToUniversalTime(),
                    Reason = reason
                };
                _service.Set(rec);

                var (days, remaining) = _service.GetRemaining(cmd.User.Id);
                await cmd.RespondAsync($"Leave set for **{nick}** → back in {days} days, {remaining}.", ephemeral: true);
                break;
            }
            case "clear":
                var ok = _service.Clear(cmd.User.Id);
                await cmd.RespondAsync(ok ? "Leave cleared." : "No leave set.", ephemeral: true);
                break;

            default:
                await cmd.RespondAsync("Unknown subcommand.", ephemeral: true);
                break;
        }
    }*/
}