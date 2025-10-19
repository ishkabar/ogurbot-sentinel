using System;
using System.Threading.Tasks;
//using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Abstractions.Leaves;
using Ogur.Sentinel.Worker.Services;

namespace Ogur.Sentinel.Worker.Discord.Modules;

/*
 
public sealed class LeaveModule : ApplicationCommandModule
{
    private readonly LeaveService _service;
    private readonly ILogger<LeaveModule> _logger;

    public LeaveModule(LeaveService service, ILogger<LeaveModule> logger)
    {
        _service = service;
        _logger = logger;
    }

    [SlashCommand("leave", "Leave management")]
    public async Task LeaveRootAsync(InteractionContext ctx)
        => await ctx.CreateResponseAsync("Use subcommands: set / clear");

    [SlashCommand("leave-set", "Set return date (UTC ISO or yyyy-MM-dd HH:mm)")]
    public async Task LeaveSetAsync(
        InteractionContext ctx,
        [Option("nick", "Game nick")] string nick,
        [Option("return_at_utc", "UTC return time")] string when,
        [Option("reason", "Optional reason")] string? reason = null)
    {
        if (!DateTimeOffset.TryParse(when, out var dto))
        {
            await ctx.CreateResponseAsync("Invalid datetime format.");
            return;
        }

        var rec = new LeaveRecord
        {
            GuildId = ctx.Guild?.Id ?? 0,
            ChannelId = ctx.Channel.Id,
            MessageId = 0,
            UserId = ctx.User.Id,
            GameNick = nick,
            ReturnAtUtc = dto.ToUniversalTime(),
            Reason = reason
        };

        _service.Set(rec);

        var (days, remaining) = _service.GetRemaining(ctx.User.Id);
        await ctx.CreateResponseAsync($"Leave set for **{nick}** → back in {days} days, {remaining}.");
    }

    [SlashCommand("leave-clear", "Clear user's leave")]
    public async Task LeaveClearAsync(InteractionContext ctx)
    {
        var ok = _service.Clear(ctx.User.Id);
        await ctx.CreateResponseAsync(ok ? "Leave cleared." : "No leave set.");
    }
}
*/