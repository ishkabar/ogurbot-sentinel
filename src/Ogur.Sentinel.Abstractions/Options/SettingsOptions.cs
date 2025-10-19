namespace Ogur.Sentinel.Abstractions.Options;


/// <summary>
/// Strongly-typed app settings. Maps env/appsettings used by Worker/API.
/// Mirrors Python Settings.from_env keys for continuity.
/// </summary>
public sealed class SettingsOptions
{
    // Required
    public string DiscordToken { get; set; } = default!;
    public ulong BreakChannelId { get; set; }
    public ulong BreakRoleId { get; set; }

    // Optional / extra
    public ulong? GuildId { get; set; }
    public string? KeepaliveUrl { get; set; }
    public ulong? BreakInfoChannelId { get; set; }
    public IReadOnlyList<ulong> BreakAllowedRoles { get; set; } = Array.Empty<ulong>();
    public int DefaultBreakMinutes { get; set; } = 5;
    public int Port { get; set; } = 10000;

    // Leaderboard
    public ulong? LeaderboardChannelId { get; set; }
    public int LeaderboardIntervalMinutes { get; set; } = 0;
    public string LeaderboardCommandText { get; set; } = "/top type:Text duration:Week";
    public string LeaderboardCommandVoice { get; set; } = "/top type:Voice duration:Week";

    // Leave / TZ
    public ulong LeaveChannelId { get; set; }
    public string Timezone { get; set; } = "UTC";
}