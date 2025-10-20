namespace Ogur.Sentinel.Abstractions.Respawn;

/// <summary>
/// Persisted respawn configuration (stored by infrastructure).
/// Matches Python JSON layout: channels, base_hhmm, lead_seconds, roles_allowed. 
/// </summary>
public sealed class PersistedSettings
{
    public List<ulong> RolesAllowed { get; init; } = new();
    public List<ulong> Channels { get; init; } = new();
    /// <summary>Base alignment time, HH:MM or HH:MM:SS (server time).</summary>
    public string BaseHhmm { get; init; } = "00:00:00";
    /// <summary>Seconds to trigger before the aligned tick.</summary>
    public int LeadSeconds { get; init; }
    public bool Enabled10m { get; init; }
    public bool Enabled2h { get; init; }

    public static PersistedSettings Default() => new()
    {
        RolesAllowed = new(),
        Channels = new(),
        BaseHhmm = "00:00:00",
        LeadSeconds = 0,
        Enabled10m = false,
        Enabled2h = false
    };
}