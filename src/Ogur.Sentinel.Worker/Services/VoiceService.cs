using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Worker.Discord;

namespace Ogur.Sentinel.Worker.Services;

public sealed class VoiceService
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordReadyService _ready;
    private readonly ILogger<VoiceService> _logger;

    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> GuildLocks = new();
    private static readonly ConcurrentDictionary<ulong, IAudioClient> AudioClients = new();
    private static readonly ConcurrentDictionary<ulong, DateTimeOffset> LastAttemptUtc = new();

    public VoiceService(DiscordSocketClient client, DiscordReadyService ready, ILogger<VoiceService> logger)
    {
        _client = client;
        _ready = ready;
        _logger = logger;
    }

    public async Task JoinAndPlayAsync(ulong channelId, string wavPath, CancellationToken ct = default)
    {
        await _ready.WaitForStableAsync(ct);

        if (_client.ConnectionState != ConnectionState.Connected || _client.CurrentUser is null)
        {
            _logger.LogWarning("Discord not connected (state={State}).", _client.ConnectionState);
            return;
        }

        if (_client.GetChannel(channelId) is not SocketVoiceChannel targetVc)
        {
            _logger.LogWarning("Channel {ChannelId} not found or not a voice channel.", channelId);
            return;
        }

        if (!File.Exists(wavPath))
        {
            _logger.LogWarning("Audio file not found: {Path}", wavPath);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var last = LastAttemptUtc.GetOrAdd(targetVc.Guild.Id, DateTimeOffset.MinValue);
        if (now - last < TimeSpan.FromSeconds(5))
        {
            _logger.LogWarning("Join throttled for guild {GuildId}.", targetVc.Guild.Id);
            return;
        }
        LastAttemptUtc[targetVc.Guild.Id] = now;

        var gate = GuildLocks.GetOrAdd(targetVc.Guild.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (AudioClients.TryGetValue(targetVc.Guild.Id, out var existing) &&
                existing.ConnectionState == ConnectionState.Connected &&
                (targetVc.Guild.CurrentUser as SocketGuildUser)?.VoiceChannel?.Id == targetVc.Id)
            {
                _logger.LogInformation("[VOICE] Reusing audio in #{Channel}.", targetVc.Name);
                await PlayOnceAsync(existing, targetVc, wavPath, ct);
                return;
            }

            var me = targetVc.Guild.CurrentUser as SocketGuildUser;
            var currentVc = me?.VoiceChannel;
            if (currentVc is not null && currentVc.Id != targetVc.Id)
            {
                try { await currentVc.DisconnectAsync(); } catch { /* ignore */ }
                await Task.Delay(200, ct);
            }

            IAudioClient? audio = null;
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts && audio is null; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    _logger.LogInformation("[VOICE] Connecting to #{Channel} (attempt {Attempt}/{Max})...",
                        targetVc.Name, attempt, maxAttempts);

                    // TWARDY FIX: wymuś disconnect:true przy zestawianiu sesji
                    audio = await ConnectWithForcedDisconnectAsync(targetVc, selfDeaf: true, selfMute: false, ct);

                    if (audio.ConnectionState == ConnectionState.Connected)
                    {
                        _logger.LogInformation("[VOICE] Connected to #{Channel}.", targetVc.Name);
                        break;
                    }

                    throw new TimeoutException("Voice connect returned not-Connected.");
                }
                catch (Exception ex) when (IsSessionInvalid4006(ex))
                {
                    _logger.LogWarning(ex, "[VOICE] 4006 on connect (attempt {Attempt}/{Max}). Hard teardown & backoff.",
                        attempt, maxAttempts);
                    await HardTeardownAsync(targetVc, audio);
                    audio = null;
                    await Task.Delay(TimeSpan.FromSeconds(2 + attempt), ct);
                }
                catch (TimeoutException tex)
                {
                    _logger.LogWarning(tex, "[VOICE] Start timeout (attempt {Attempt}/{Max}).", attempt, maxAttempts);
                    await HardTeardownAsync(targetVc, audio);
                    audio = null;
                    await Task.Delay(TimeSpan.FromSeconds(1 + attempt), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[VOICE] Start failed (attempt {Attempt}/{Max}).", attempt, maxAttempts);
                    await HardTeardownAsync(targetVc, audio);
                    audio = null;
                    await Task.Delay(TimeSpan.FromSeconds(1 + attempt / 2.0), ct);
                }
            }

            if (audio is null || audio.ConnectionState != ConnectionState.Connected)
            {
                _logger.LogError("[VOICE] Giving up after {Max} attempts. Channel=#{Channel}", maxAttempts, targetVc.Name);
                return;
            }

            AudioClients[targetVc.Guild.Id] = audio;

            await PlayOnceAsync(audio, targetVc, wavPath, ct);

            try { await targetVc.DisconnectAsync(); } catch { /* ignore */ }
            AudioClients.TryRemove(targetVc.Guild.Id, out _);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task HardTeardownAsync(SocketVoiceChannel targetVc, IAudioClient? audio)
    {
        try { await targetVc.DisconnectAsync(); } catch { /* ignore */ }
        if (audio != null)
        {
            try { await audio.StopAsync(); } catch { /* ignore */ }
            try { (audio as IDisposable)?.Dispose(); } catch { /* ignore */ }
        }
        await Task.Delay(200);
    }

    private async Task PlayOnceAsync(IAudioClient audio, SocketVoiceChannel vc, string wavPath, CancellationToken ct)
    {
        using var ffmpeg = StartFfmpeg(wavPath);
        if (ffmpeg is null || ffmpeg.HasExited)
        {
            _logger.LogError("ffmpeg failed to start. Check installation and PATH.");
            return;
        }

        await using var pcm = audio.CreatePCMStream(AudioApplication.Mixed);
        try
        {
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(pcm, ct);
            await pcm.FlushAsync(ct);
        }
        finally
        {
            try { await pcm.DisposeAsync(); } catch { /* ignore */ }
        }

        _logger.LogInformation("Played {File} on #{Channel} ({Guild}).",
            Path.GetFileName(wavPath), vc.Name, vc.Guild.Name);
    }

    private static bool IsSessionInvalid4006(Exception ex)
    {
        var t = ex.GetType();
        if (t.FullName != "Discord.Net.WebSocketClosedException") return false;
        var prop = t.GetProperty("CloseCode");
        var val = prop?.GetValue(ex);
        return (val is int i && i == 4006) || (val is ushort u && u == 4006);
    }

    private static Process? StartFfmpeg(string path) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel error -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false
        });
    
    private static readonly MethodInfo? ConnectAudioInternal =
        typeof(SocketGuild).GetMethod("ConnectAudioAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(ulong), typeof(bool), typeof(bool), typeof(bool), typeof(bool) },
            modifiers: null);

    private static async Task<IAudioClient> ConnectWithForcedDisconnectAsync(
        SocketVoiceChannel vc, bool selfDeaf, bool selfMute, CancellationToken ct)
    {
        var guild = vc.Guild;

        if (ConnectAudioInternal != null)
        {
            // external:false, disconnect:true
            var task = (Task<IAudioClient>)ConnectAudioInternal.Invoke(guild, new object[]
            {
                vc.Id, selfDeaf, selfMute, /*external*/ false, /*disconnect*/ true
            })!;

            // .NET 8: cooperative cancellation
            return await task.WaitAsync(ct).ConfigureAwait(false);
        }

        // Fallback: public API
        return await vc.ConnectAsync(selfDeaf: selfDeaf, selfMute: selfMute).ConfigureAwait(false);
    }
}
