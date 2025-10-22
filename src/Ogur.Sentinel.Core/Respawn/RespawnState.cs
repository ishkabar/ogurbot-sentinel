using Ogur.Sentinel.Abstractions.Respawn;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Abstractions.Options;

namespace Ogur.Sentinel.Core.Respawn;


/// <summary>
/// Pure state for respawn feature (no Discord deps).
/// Holds configuration and runtime toggles used by Worker.
/// Mirrors Python RespawnState. :contentReference[oaicite:6]{index=6}

/// </summary>
public sealed class RespawnState
{
    private readonly List<ulong> _channels = new();
    public event Action? OnSettingsChanged;

    public IReadOnlyList<ulong> Channels => _channels;
    public string BaseHhmm { get; set; } = "00:00:00";
    public int LeadSeconds { get; set; } = 0;
    
    public bool Enabled10m { get; set; } = false;
    public bool Enabled2h { get; set; } = false;
    public int MaxChannels { get; set; } = 3;
    public bool UseSyncedTime { get; set; } = false;
    public string? SyncedBaseTime { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    
    public HashSet<ulong> RolesAllowed { get; } = new();
    public int RepeatPlays { get; set; } = 3;
    public int RepeatGapMs { get; set; } = 250;

    public void SetChannels(IEnumerable<ulong> ids)
    {
        _channels.Clear();
        _channels.AddRange(ids.Take(MaxChannels));
    }

    public void NotifySettingsChanged() 
    {
        OnSettingsChanged?.Invoke();
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
        Enabled10m = p.Enabled10m;
        Enabled2h = p.Enabled2h;
        UseSyncedTime = p.UseSyncedTime;
        SyncedBaseTime = p.SyncedBaseTime;
        LastSyncAt = p.LastSyncAt;
        RepeatPlays = p.RepeatPlays; 
        RepeatGapMs = p.RepeatGapMs;
        RolesAllowed.Clear();
        foreach (var r in p.RolesAllowed) RolesAllowed.Add((ulong)r);
    }

    public PersistedSettings ToPersisted() => new()
    {
        Channels = _channels.Select(x => (ulong)x).ToList(),
        BaseHhmm = BaseHhmm,
        LeadSeconds = LeadSeconds,
        Enabled10m = Enabled10m,
        Enabled2h = Enabled2h,
        UseSyncedTime = UseSyncedTime,
        SyncedBaseTime = SyncedBaseTime,
        LastSyncAt = LastSyncAt,
        RepeatPlays = RepeatPlays,  
        RepeatGapMs = RepeatGapMs,  
        RolesAllowed = RolesAllowed.Select(x => (ulong)x).ToList()
    };
}