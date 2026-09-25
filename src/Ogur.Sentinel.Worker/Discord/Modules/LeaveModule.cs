using System.Globalization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Ogur.Sentinel.Abstractions.Leaves;
using Ogur.Sentinel.Worker.Services;

namespace Ogur.Sentinel.Worker.Discord.Modules;

[SlashCommand("leave", "Leave management")]
public sealed class LeaveModule(LeaveService service) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SubSlashCommand("set", "Set return date (UTC ISO or yyyy-MM-dd HH:mm)")]
    public InteractionMessageProperties Set(
        [SlashCommandParameter(Description = "Game nick")] string nick,
        [SlashCommandParameter(Name = "return_at_utc", Description = "UTC return time")] string returnAtUtc,
        [SlashCommandParameter(Description = "Optional reason")] string? reason = null)
    {
        if (!DateTimeOffset.TryParse(returnAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return Ephemeral("Invalid datetime format.");

        var rec = new LeaveRecord
        {
            GuildId = Context.Interaction.GuildId ?? 0,
            ChannelId = Context.Channel.Id,
            MessageId = 0,
            UserId = Context.User.Id,
            GameNick = nick,
            ReturnAtUtc = dto.ToUniversalTime(),
            Reason = reason
        };
        service.Set(rec);

        var (days, remaining) = service.GetRemaining(Context.User.Id);
        return Ephemeral($"Leave set for **{nick}** → back in {days} days, {remaining}.");
    }

    [SubSlashCommand("clear", "Clear user's leave")]
    public InteractionMessageProperties Clear()
    {
        var ok = service.Clear(Context.User.Id);
        return Ephemeral(ok ? "Leave cleared." : "No leave set.");
    }

    private static InteractionMessageProperties Ephemeral(string content) => new()
    {
        Content = content,
        Flags = MessageFlags.Ephemeral
    };
}