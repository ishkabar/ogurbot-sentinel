namespace Ogur.Sentinel.Worker.Discord.Break;

public sealed class BreakOptions
{
    public ulong ChannelId { get; init; }
    public int Minutes { get; init; } = 30;
}