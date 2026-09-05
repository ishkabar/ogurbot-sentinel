namespace Ogur.Sentinel.Core.Ore;

public sealed class OreState
{
    private readonly object _lock = new();

    public double MarkedX { get; private set; }
    public double MarkedY { get; private set; }
    public string? MarkedByUserId { get; private set; }
    public string? MarkedByUsername { get; private set; }
    public DateTimeOffset? MarkedAtUtc { get; private set; }

    public void SetMark(double x, double y, string userId, string username, DateTimeOffset atUtc)
    {
        lock (_lock)
        {
            MarkedX = x;
            MarkedY = y;
            MarkedByUserId = userId;
            MarkedByUsername = username;
            MarkedAtUtc = atUtc;
        }
    }

    public OrePersisted ToPersisted()
    {
        lock (_lock)
        {
            return new OrePersisted
            {
                MarkedX = MarkedX,
                MarkedY = MarkedY,
                MarkedByUserId = MarkedByUserId,
                MarkedByUsername = MarkedByUsername,
                MarkedAtUtc = MarkedAtUtc
            };
        }
    }

    public void ApplyPersisted(OrePersisted persisted)
    {
        lock (_lock)
        {
            MarkedX = persisted.MarkedX;
            MarkedY = persisted.MarkedY;
            MarkedByUserId = persisted.MarkedByUserId;
            MarkedByUsername = persisted.MarkedByUsername;
            MarkedAtUtc = persisted.MarkedAtUtc;
        }
    }
}

public sealed class OrePersisted
{
    public double MarkedX { get; set; }
    public double MarkedY { get; set; }
    public string? MarkedByUserId { get; set; }
    public string? MarkedByUsername { get; set; }
    public DateTimeOffset? MarkedAtUtc { get; set; }
}