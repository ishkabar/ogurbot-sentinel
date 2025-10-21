using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Abstractions.Options;
using Ogur.Sentinel.Core.Respawn;

namespace Ogur.Sentinel.Worker.Services;

public sealed class WikiSyncService
{
    private readonly RespawnState _state;
    private readonly SettingsStore _store;
    private readonly HttpClient _http;
    private readonly RespawnOptions _opts;
    private readonly ILogger<WikiSyncService> _logger;
    
    private static readonly Regex TimeRegex = new(@"(\d{2}:\d{2}:\d{2})\s*-\s*Akademia", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public WikiSyncService(
        RespawnState state,
        SettingsStore store,
        HttpClient http,
        IOptions<RespawnOptions> opts,
        ILogger<WikiSyncService> logger)
    {
        _state = state;
        _store = store;
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<bool> SyncAsync(CancellationToken ct = default)
    {
        if (!_opts.SyncEnabled || string.IsNullOrWhiteSpace(_opts.SyncUrl))
        {
            _logger.LogDebug("[WikiSync] Sync disabled or no URL");
            return false;
        }

        try
        {
            _logger.LogInformation("[WikiSync] Fetching from {Url}", _opts.SyncUrl);
            
            var html = await _http.GetStringAsync(_opts.SyncUrl, ct);
            
            var match = TimeRegex.Match(html);
            if (!match.Success)
            {
                _logger.LogWarning("[WikiSync] Could not find 'XX:XX:XX - Akademia' pattern in HTML");
                return false;
            }

            var time = match.Groups[1].Value;
            
            // Waliduj format
            SchedulingMath.ParseHhmm(time);
            
            _state.SyncedBaseTime = time;
            _state.LastSyncAt = DateTimeOffset.Now;
            
            await _store.SaveAsync(_state.ToPersisted());
            
            _logger.LogInformation("[WikiSync] ✓ Synced base time: {Time}", time);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WikiSync] Sync failed");
            return false;
        }
    }
}