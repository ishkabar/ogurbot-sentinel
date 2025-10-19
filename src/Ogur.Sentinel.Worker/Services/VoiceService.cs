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
            _logger.LogWarning("[VOICE] Discord not connected (state={State}).", _client.ConnectionState);
            return;
        }

        if (_client.GetChannel(channelId) is not SocketVoiceChannel targetVc)
        {
            _logger.LogWarning("[VOICE] Channel {ChannelId} not found or not a voice channel.", channelId);
            return;
        }

        if (!File.Exists(wavPath))
        {
            _logger.LogWarning("[VOICE] Audio file not found: {Path}", wavPath);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var last = LastAttemptUtc.GetOrAdd(targetVc.Guild.Id, DateTimeOffset.MinValue);
        if (now - last < TimeSpan.FromSeconds(10)) // Zwiększone z 5s do 10s
        {
            _logger.LogWarning("[VOICE] Join throttled for guild {GuildId}.", targetVc.Guild.Id);
            return;
        }
        LastAttemptUtc[targetVc.Guild.Id] = now;

        var gate = GuildLocks.GetOrAdd(targetVc.Guild.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            IAudioClient? audio = null;
            const int maxAttempts = 3; // Zmniejszone do 3 ale z dłuższymi delay

            for (var attempt = 1; attempt <= maxAttempts && audio is null; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    _logger.LogInformation("[VOICE] === ATTEMPT {Attempt}/{Max} to #{Channel} ===",
                        attempt, maxAttempts, targetVc.Name);

                    // KROK 1: NUCLEAR CLEANUP - zniszcz wszystko
                    _logger.LogDebug("[VOICE] Step 1: Nuclear cleanup...");
                    await NuclearCleanupAsync(targetVc);

                    // KROK 2: DŁUGI DELAY - daj Discordowi czas na zapomnienie sesji
                    var delay = attempt == 1 ? 2000 : (3000 + (attempt * 1000));
                    _logger.LogDebug("[VOICE] Step 2: Waiting {Ms}ms for Discord to clear session...", delay);
                    await Task.Delay(delay, ct);

                    // KROK 3: CONNECT z disconnect:true
                    _logger.LogInformation("[VOICE] Step 3: Connecting with forced disconnect...");
                    audio = await ConnectWithForcedDisconnectAsync(targetVc, selfDeaf: true, selfMute: false, ct);

                    if (audio.ConnectionState == ConnectionState.Connected)
                    {
                        _logger.LogInformation("[VOICE] ✓ Connected successfully to #{Channel}!", targetVc.Name);
                        break;
                    }

                    _logger.LogWarning("[VOICE] Connection returned state: {State}", audio.ConnectionState);
                    await DisposeAudioAsync(audio);
                    audio = null;
                }
                catch (Exception ex) when (IsSessionInvalid4006(ex))
                {
                    _logger.LogError("[VOICE] ✗ 4006 Session Invalid on attempt {Attempt}/{Max}",
                        attempt, maxAttempts);

                    if (audio != null)
                        await DisposeAudioAsync(audio);
                    audio = null;

                    // Po 4006 - MEGA DŁUGI backoff
                    var backoff = TimeSpan.FromSeconds(5 + (attempt * 2));
                    _logger.LogWarning("[VOICE] Waiting {Sec:F1}s after 4006 before retry...", backoff.TotalSeconds);
                    await Task.Delay(backoff, ct);

                    // Dodatkowy nuclear cleanup po 4006
                    await NuclearCleanupAsync(targetVc);
                }
                catch (TimeoutException tex)
                {
                    _logger.LogWarning(tex, "[VOICE] Timeout on attempt {Attempt}/{Max}", attempt, maxAttempts);
                    if (audio != null)
                        await DisposeAudioAsync(audio);
                    audio = null;
                    await Task.Delay(TimeSpan.FromSeconds(2 + attempt), ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "[VOICE] Failed on attempt {Attempt}/{Max}", attempt, maxAttempts);
                    if (audio != null)
                        await DisposeAudioAsync(audio);
                    audio = null;
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
            }

            if (audio is null || audio.ConnectionState != ConnectionState.Connected)
            {
                _logger.LogError("[VOICE] ✗✗✗ FAILED after {Max} attempts to #{Channel} ✗✗✗",
                    maxAttempts, targetVc.Name);
                return;
            }

            // Play audio
            await PlayOnceAsync(audio, targetVc, wavPath, ct);

            // Cleanup
            await DisposeAudioAsync(audio);
            try { await targetVc.DisconnectAsync(); } catch { }
            await NuclearCleanupAsync(targetVc);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// NUCLEAR OPTION - niszczy WSZYSTKO związane z audio dla tej gildii
    /// </summary>
    private async Task NuclearCleanupAsync(SocketVoiceChannel vc)
    {
        var guild = vc.Guild;
        _logger.LogDebug("[VOICE] NuclearCleanup for guild {GuildId}", guild.Id);

        // 1. Discord API disconnect
        try
        {
            await vc.DisconnectAsync();
            _logger.LogDebug("[VOICE] DisconnectAsync completed");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[VOICE] DisconnectAsync threw");
        }

        // 2. Czekaj aż bot FAKTYCZNIE opuści voice (polling CurrentUser.VoiceChannel)
        for (int i = 0; i < 30; i++) // 3s max
        {
            var me = guild.CurrentUser as SocketGuildUser;
            if (me?.VoiceChannel is null)
            {
                _logger.LogDebug("[VOICE] Bot confirmed left voice after {Ms}ms", i * 100);
                break;
            }
            await Task.Delay(100);
        }

        // 3. REFLECTION: Zniszcz _audioClient w SocketGuild
        try
        {
            var audioClientField = typeof(SocketGuild).GetField("_audioClient",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (audioClientField != null)
            {
                var existingClient = audioClientField.GetValue(guild) as IAudioClient;
                if (existingClient != null)
                {
                    _logger.LogDebug("[VOICE] Found _audioClient, destroying...");

                    try { await existingClient.StopAsync(); } catch { }
                    try { (existingClient as IDisposable)?.Dispose(); } catch { }

                    // WYZERUJ pole - wymusza nowy IAudioClient
                    audioClientField.SetValue(guild, null);
                    _logger.LogDebug("[VOICE] _audioClient nuked");
                }
            }

            // 4. Reset _audioLock semaphore
            var audioLockField = typeof(SocketGuild).GetField("_audioLock",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (audioLockField != null)
            {
                var lockObj = audioLockField.GetValue(guild);
                if (lockObj != null)
                {
                    try { (lockObj as IDisposable)?.Dispose(); } catch { }
                    audioLockField.SetValue(guild, new SemaphoreSlim(1, 1));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[VOICE] Reflection cleanup failed (continuing)");
        }

        // 5. DŁUGI oddech - Discord backend musi zapomnieć o sesji
        await Task.Delay(800);
        _logger.LogDebug("[VOICE] NuclearCleanup complete");
    }

    private async Task DisposeAudioAsync(IAudioClient audio)
    {
        try
        {
            if (audio.ConnectionState != ConnectionState.Disconnected)
            {
                await audio.StopAsync();
                // Czekaj na disconnect
                for (int i = 0; i < 20; i++) // 2s max
                {
                    if (audio.ConnectionState == ConnectionState.Disconnected)
                        break;
                    await Task.Delay(100);
                }
            }
        }
        catch { }

        try { (audio as IDisposable)?.Dispose(); } catch { }
    }

    private async Task PlayOnceAsync(IAudioClient audio, SocketVoiceChannel vc, string wavPath, CancellationToken ct)
    {
        using var ffmpeg = StartFfmpeg(wavPath);
        if (ffmpeg is null || ffmpeg.HasExited)
        {
            _logger.LogError("[VOICE] ffmpeg failed to start");
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
            try { await pcm.DisposeAsync(); } catch { }
        }

        _logger.LogInformation("[VOICE] ✓ Played {File} on #{Channel} ({Guild})",
            Path.GetFileName(wavPath), vc.Name, vc.Guild.Name);
    }

    private static bool IsSessionInvalid4006(Exception ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException!)
        {
            var t = cur.GetType();
            if (t.FullName == "Discord.Net.WebSocketClosedException")
            {
                var prop = t.GetProperty("CloseCode");
                var val = prop?.GetValue(cur);
                if ((val is int i && i == 4006) || (val is ushort u && u == 4006))
                    return true;
            }
        }
        return false;
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
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static async Task<IAudioClient> ConnectWithForcedDisconnectAsync(
        SocketVoiceChannel vc, bool selfDeaf, bool selfMute, CancellationToken ct)
    {
        var guild = vc.Guild;

        if (ConnectAudioInternal != null)
        {
            var task = (Task<IAudioClient>)ConnectAudioInternal.Invoke(guild, new object[]
            {
                vc.Id,
                selfDeaf,
                selfMute,
                false, // external
                true   // disconnect - WYMUSZA nowy session_id
            })!;

            return await task.WaitAsync(TimeSpan.FromSeconds(20), ct);
        }

        // Fallback
        try { await vc.DisconnectAsync(); } catch { }
        await Task.Delay(1000, ct);
        return await vc.ConnectAsync(selfDeaf: selfDeaf, selfMute: selfMute)
            .WaitAsync(TimeSpan.FromSeconds(20), ct);
    }
}