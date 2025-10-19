using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Abstractions.Options;



namespace Ogur.Sentinel.Worker.Services;


public sealed class RespawnSchedulerService
{
    private readonly RespawnState _state;
    private readonly VoiceService2 _voice;
    private readonly ILogger<RespawnSchedulerService> _logger;
    private readonly string _sound10m;
    private readonly string _sound2h;

    public RespawnSchedulerService(
        RespawnState state,
        VoiceService2 voice,
        IOptions<RespawnOptions> opts,
        ILogger<RespawnSchedulerService> logger)
    {
        _state = state;
        _voice = voice;
        _logger = logger;

        // Bind z appsettings + normalizacja do katalogu wykonywalnego
        var o = opts.Value;
        _sound10m = Normalize(o.Sound10m ?? "assets/respawn_10m.wav");
        _sound2h  = Normalize(o.Sound2h  ?? "assets/respawn_2h.wav");

        _logger.LogInformation("[Respawn] Sounds resolved: 10m={S10}, 2h={S2}", _sound10m, _sound2h);
    }

    private static string Normalize(string path)
        => Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    public (DateTimeOffset next10m, DateTimeOffset next2h) ComputeNext(DateTimeOffset nowLocal)
    {
        var baseTime = SchedulingMath.ParseHhmm(_state.BaseHhmm);
        var lead = TimeSpan.FromSeconds(Math.Max(0, _state.LeadSeconds));
        var next10 = SchedulingMath.NextAligned(nowLocal, baseTime, TimeSpan.FromMinutes(10), lead);
        var next2h = SchedulingMath.NextAligned(nowLocal, baseTime, TimeSpan.FromHours(2), lead);
        return (next10, next2h);
    }

    public async Task PlayAsync(bool is10m, CancellationToken ct)
    {
        var sound = is10m ? _sound10m : _sound2h;

        foreach (var ch in _state.Channels)
        {
            try
            {
                await _voice.JoinAndPlayAsync(ch, sound, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Respawn] Play failed on channel {ChannelId}", ch);
            }
        }
    }
}