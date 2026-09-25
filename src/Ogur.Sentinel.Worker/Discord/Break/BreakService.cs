using System.Collections.Concurrent;

namespace Ogur.Sentinel.Worker.Discord.Break;

public sealed class BreakService
{
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), DateTimeOffset> _breaks = new();

    public DateTimeOffset Start(ulong guildId, ulong userId, TimeSpan duration)
    {
        var until = DateTimeOffset.UtcNow + duration;
        _breaks[(guildId, userId)] = until;
        return until;
    }

    public bool IsActive(ulong guildId, ulong userId)
    {
        if (!_breaks.TryGetValue((guildId, userId), out var until))
            return false;

        if (until > DateTimeOffset.UtcNow)
            return true;

        _breaks.TryRemove((guildId, userId), out _);
        return false;
    }
}