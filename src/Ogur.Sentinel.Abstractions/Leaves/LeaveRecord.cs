namespace Ogur.Sentinel.Abstractions.Leaves;


/// <summary>
/// Leave message record (for periodic updates).
/// Ported from Python dict structure used by updater and /urlop flow.
/// </summary>
public sealed class LeaveRecord
{
    public required ulong GuildId { get; init; }
    public required ulong ChannelId { get; init; }
    public required ulong MessageId { get; init; }
    public required ulong UserId { get; init; }
    public required string GameNick { get; init; }
    public required DateTimeOffset ReturnAtUtc { get; init; }
    public string? Reason { get; init; }
}