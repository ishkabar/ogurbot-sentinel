using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Ogur.Sentinel.Abstractions.Options;
using Ogur.Sentinel.Abstractions.Respawn;

namespace Ogur.Sentinel.Worker.Services;

public sealed class SettingsStore
{
    private readonly string _filePath;
    private readonly ILogger<SettingsStore> _logger;

    public SettingsStore(IConfiguration cfg, ILogger<SettingsStore> logger)
    {
        _filePath = cfg["Respawn:SettingsFile"] ?? "appsettings/respawn.settings.json";
        _logger = logger;
    }

    public async Task<PersistedSettings> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_filePath))
                return PersistedSettings.Default();

            await using var fs = File.OpenRead(_filePath);
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(fs, cancellationToken: ct);
            if (doc.ValueKind == JsonValueKind.Undefined || doc.ValueKind == JsonValueKind.Null)
                return PersistedSettings.Default();

            var ps = new PersistedSettings
            {
                RolesAllowed = doc.TryGetProperty("roles_allowed", out var ra) && ra.ValueKind == JsonValueKind.Array
                    ? ra.EnumerateArray().Select(x => (ulong)x.GetUInt64()).ToList()
                    : new List<ulong>(),

                Channels = doc.TryGetProperty("channels", out var ch) && ch.ValueKind == JsonValueKind.Array
                    ? ch.EnumerateArray().Select(x => (ulong)x.GetUInt64()).ToList()
                    : new List<ulong>(),

                BaseHhmm = doc.TryGetProperty("base_hhmm", out var bh) && bh.ValueKind is JsonValueKind.String
                    ? (bh.GetString() ?? "00:00:00")
                    : "00:00:00",

                LeadSeconds = doc.TryGetProperty("lead_seconds", out var ls) && ls.ValueKind is JsonValueKind.Number
                    ? ls.GetInt32()
                    : 0
            };

            return ps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load respawn settings from {Path}", _filePath);
            return PersistedSettings.Default();
        }
    }

    public async Task SaveAsync(PersistedSettings settings, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var payload = new
            {
                roles_allowed = settings.RolesAllowed,
                channels = settings.Channels,
                base_hhmm = settings.BaseHhmm,
                lead_seconds = settings.LeadSeconds
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save respawn settings to {Path}", _filePath);
        }
    }
}