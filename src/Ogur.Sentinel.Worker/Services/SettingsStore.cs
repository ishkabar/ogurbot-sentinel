using System.Text.Json;
using Microsoft.Extensions.Options;
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
        _logger = logger; // NAJPIERW przypisz logger!
        
        var relativePath = cfg["Respawn:SettingsFile"] ?? "appsettings/respawn.settings.json";

        _filePath = Path.IsPathRooted(relativePath) 
            ? relativePath 
            : Path.Combine(AppContext.BaseDirectory, relativePath);

        _logger.LogInformation("[SettingsStore] Using file: {Path}", _filePath);
    }

    public async Task<PersistedSettings> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("[SettingsStore] File not found, using defaults: {Path}", _filePath);
                return PersistedSettings.Default();
            }

            _logger.LogDebug("[SettingsStore] Loading from {Path}", _filePath);
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
                    ? ch.EnumerateArray().Select(x => 
                        x.ValueKind == JsonValueKind.String 
                            ? ulong.Parse(x.GetString()!) 
                            : (ulong)x.GetUInt64()
                    ).ToList()
                    : new List<ulong>(),

                BaseHhmm = doc.TryGetProperty("base_hhmm", out var bh) && bh.ValueKind is JsonValueKind.String
                    ? (bh.GetString() ?? "00:00:00")
                    : "00:00:00",

                LeadSeconds = doc.TryGetProperty("lead_seconds", out var ls) && ls.ValueKind is JsonValueKind.Number
                    ? ls.GetInt32()
                    : 0,
                
                Enabled10m = doc.TryGetProperty("enabled_10m", out var e10) && e10.ValueKind is JsonValueKind.True,
                
                Enabled2h = doc.TryGetProperty("enabled_2h", out var e2h) && e2h.ValueKind is JsonValueKind.True,

                RepeatPlays10m = doc.TryGetProperty("repeat_plays_10m", out var rp10m) && rp10m.ValueKind is JsonValueKind.Number
                    ? rp10m.GetInt32() : 3,
                
                RepeatGapMs10m = doc.TryGetProperty("repeat_gap_ms_10m", out var rg10m) && rg10m.ValueKind is JsonValueKind.Number
                    ? rg10m.GetInt32() : 1000,
                
                RepeatPlays2h = doc.TryGetProperty("repeat_plays_2h", out var rp2h) && rp2h.ValueKind is JsonValueKind.Number
                    ? rp2h.GetInt32() : 3,
                
                RepeatGapMs2h = doc.TryGetProperty("repeat_gap_ms_2h", out var rg2h) && rg2h.ValueKind is JsonValueKind.Number
                    ? rg2h.GetInt32() : 1000
            };

            _logger.LogInformation("[SettingsStore] Loaded: Channels={ChCount}, Base={Base}, Lead={Lead}s", 
                ps.Channels.Count, ps.BaseHhmm, ps.LeadSeconds);

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
            {
                Directory.CreateDirectory(dir);
                _logger.LogDebug("[SettingsStore] Created directory: {Dir}", dir);
            }

            var payload = new
            {
                roles_allowed = settings.RolesAllowed.Select(r => r.ToString()).ToArray(),
                channels = settings.Channels.Select(c => c.ToString()).ToArray(),
                base_hhmm = settings.BaseHhmm,
                lead_seconds = settings.LeadSeconds,
                enabled_10m = settings.Enabled10m,
                enabled_2h = settings.Enabled2h,
                repeat_plays_10m = settings.RepeatPlays10m,
                repeat_gap_ms_10m = settings.RepeatGapMs10m,
                repeat_plays_2h = settings.RepeatPlays2h,
                repeat_gap_ms_2h = settings.RepeatGapMs2h
            };

 

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            
            _logger.LogDebug("[SettingsStore] JSON to save: {Json}", json);
        
            await File.WriteAllTextAsync(_filePath, json, ct);
    
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save respawn settings to {Path}", _filePath);
        }
    }
}