using Ogur.Sentinel.Abstractions.Respawn;

namespace Ogur.Sentinel.Core.Respawn;


/// <summary>
/// Pure state for respawn feature (no Discord deps).
/// Holds configuration and runtime toggles used by Worker.
/// Mirrors Python RespawnState. :contentReference[oaicite:6]{index=6}

/// </summary>
public sealed class RespawnState
{
    private readonly List<ulong> _channels = new();

    public IReadOnlyList<ulong> Channels => _channels;
    public string BaseHhmm { get; set; } = "00:00:00";
    public int LeadSeconds { get; set; } = 0;

    // Runtime toggles (controlled by slash commands in Worker)
    public bool Enabled10m { get; set; } = false;
    public bool Enabled2h { get; set; } = false;

    public HashSet<ulong> RolesAllowed { get; } = new();

    public void SetChannels(IEnumerable<ulong> ids)
    {
        _channels.Clear();
        _channels.AddRange(ids.Take(3));
    }

    public bool RemoveChannel(ulong id)
    {
        var removed = _channels.Remove(id);
        return removed;
    }

    public void ClearChannels() => _channels.Clear();

    public void ApplyPersisted(PersistedSettings p)
    {
        SetChannels(p.Channels);
        BaseHhmm = p.BaseHhmm;
        LeadSeconds = p.LeadSeconds;
        RolesAllowed.Clear();
        foreach (var r in p.RolesAllowed) RolesAllowed.Add((ulong)r);
    }

    public PersistedSettings ToPersisted() => new()
    {
        Channels = _channels.Select(x => (ulong)x).ToList(),
        BaseHhmm = BaseHhmm,
        LeadSeconds = LeadSeconds,
        RolesAllowed = RolesAllowed.Select(x => (ulong)x).ToList()
    };
}