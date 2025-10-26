/*
using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Worker.Discord;
using System.Threading;

namespace Ogur.Sentinel.Worker.Services;

/// <summary>
/// Voice service using custom Discord Voice implementation
/// Uses Discord.NET to get credentials, then immediately disconnects and uses custom client
/// </summary>
public sealed class VoiceService2
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordReadyService _ready;
    private readonly ILogger<VoiceService2> _logger;
    private readonly ILogger<DiscordVoiceClient> _voiceLogger;
    private readonly SemaphoreSlim _voiceLock = new(1, 1);

    public VoiceService2(
        DiscordSocketClient client,
        DiscordReadyService ready,
        ILogger<VoiceService2> logger,
        ILogger<DiscordVoiceClient> voiceLogger)
    {
        _logger = logger;
        _voiceLogger = voiceLogger;
        _client = client;
        _ready = ready;
    }

    public async Task JoinAndPlayAsync(ulong channelId, string wavPath, int repeatCount = 1, int repeatGapMs = 250,
        CancellationToken ct = default)
    {
        var callId = Guid.NewGuid().ToString().Substring(0, 8);
        _logger.LogDebug("[VOICE-SVC] 📥 JoinAndPlayAsync ENTRY - CallId: {CallId}", callId);

        await _voiceLock.WaitAsync(ct);

        try
        {
            await _ready.WaitForStableAsync(ct);

            if (_client.ConnectionState != ConnectionState.Connected || _client.CurrentUser is null)
            {
                _logger.LogWarning("[VOICE-SVC] ❌ Discord not connected");
                return;
            }

            var channel = _client.GetChannel(channelId);
            if (channel is not SocketVoiceChannel targetVc)
            {
                _logger.LogWarning("[VOICE-SVC] ❌ Channel not found");
                return;
            }

            _logger.LogDebug("[VOICE-SVC] ✅ Found: #{Name} in {Guild}", targetVc.Name, targetVc.Guild.Name);

            // Disconnect if already in voice
            var currentVoiceChannel = targetVc.Guild.CurrentUser?.VoiceChannel;
            if (currentVoiceChannel != null)
            {
                _logger.LogDebug("[VOICE-SVC] ⏳ Disconnecting from previous channel...");
                await currentVoiceChannel.DisconnectAsync();
                await Task.Delay(1000, ct);
            }

            if (!File.Exists(wavPath))
            {
                _logger.LogWarning("[VOICE-SVC] ❌ Audio file not found: {Path}", wavPath);
                return;
            }

            DiscordVoiceClient? voiceClient = null;

            try
            {
                // Setup event listeners BEFORE connecting
                _logger.LogDebug("[VOICE-SVC] 📡 Setting up event listeners...");
                var voiceServerTask = WaitForVoiceServerAsync(targetVc.Guild.Id, ct);
                var sessionIdTask = WaitForSessionIdAsync(targetVc.Guild.Id, ct);

                // First connection - trigger events and get initial session
                _logger.LogDebug("[VOICE-SVC] 📞 First connect - triggering events...");
                _ = targetVc.ConnectAsync(selfDeaf: true, selfMute: false, external: false);

                // Wait for first session/token
                await Task.WhenAll(voiceServerTask, sessionIdTask);
                await voiceServerTask;
                await sessionIdTask;

                _logger.LogDebug("[VOICE-SVC] ✅ First session received, disconnecting...");
                await targetVc.DisconnectAsync();
                
                _logger.LogDebug("[VOICE-SVC] ⏳ Waiting 3s for session to expire...");
                await Task.Delay(3000, ct);

                // SECOND connection - get FRESH session
                _logger.LogDebug("[VOICE-SVC] 📞 Second connect - getting FRESH session...");
                var voiceServerTask2 = WaitForVoiceServerAsync(targetVc.Guild.Id, ct);
                var sessionIdTask2 = WaitForSessionIdAsync(targetVc.Guild.Id, ct);
                
                _ = targetVc.ConnectAsync(selfDeaf: true, selfMute: false, external: false);

                // Wait for FRESH credentials
                await Task.WhenAll(voiceServerTask2, sessionIdTask2);
                var (token, endpoint) = await voiceServerTask2;
                var sessionId = await sessionIdTask2;

                _logger.LogDebug("[VOICE-SVC] ✅ Got FRESH credentials - disconnecting Discord.NET IMMEDIATELY");
                await targetVc.DisconnectAsync();
                
                _logger.LogDebug("[VOICE-SVC] ⏳ Waiting 1s for Discord cleanup...");
                await Task.Delay(1000, ct);

                _logger.LogDebug("[VOICE-SVC] 🔌 Connecting custom voice client...");

                // Create and connect OUR voice client with the credentials
                voiceClient = new DiscordVoiceClient(_voiceLogger);
                await voiceClient.ConnectAsync(
                    targetVc.Guild.Id,
                    _client.CurrentUser.Id,
                    channelId,
                    sessionId,
                    token,
                    endpoint,
                    ct);

                _logger.LogDebug("[VOICE-SVC] ✅ Custom voice client connected!");

                // Play audio
                for (int i = 0; i < repeatCount; i++)
                {
                    _logger.LogDebug("[VOICE-SVC] 🎵 Playing {Current}/{Total}...", i + 1, repeatCount);
                    await PlayWavAsync(voiceClient, wavPath, ct);

                    if (i + 1 < repeatCount)
                    {
                        await Task.Delay(repeatGapMs, ct);
                    }
                }

                _logger.LogDebug("[VOICE-SVC] ✅ Playback complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VOICE-SVC] ❌ Voice operation failed");
            }
            finally
            {
                if (voiceClient != null)
                {
                    await voiceClient.DisposeAsync();
                }

                // Final disconnect via Discord.NET
                try
                {
                    await targetVc.DisconnectAsync();
                }
                catch { }
            }
        }
        finally
        {
            _voiceLock.Release();
        }
    }

    private async Task PlayWavAsync(DiscordVoiceClient client, string wavPath, CancellationToken ct)
    {
        _logger.LogDebug("[VOICE-SVC] 🎵 PlayWavAsync ENTRY");

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel error -i \"{wavPath}\" -ac 2 -f s16le -ar 48000 pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var ffmpeg = Process.Start(psi);
        if (ffmpeg == null || ffmpeg.HasExited)
        {
            _logger.LogError("[VOICE-SVC] ❌ ffmpeg failed to start");
            return;
        }

        var buffer = new byte[3840];
        using var pcmStream = ffmpeg.StandardOutput.BaseStream;
        var frameCount = 0;

        while (!ct.IsCancellationRequested)
        {
            var totalRead = 0;

            while (totalRead < buffer.Length)
            {
                var read = await pcmStream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);

                if (read == 0) // EOF
                {
                    if (totalRead > 0)
                    {
                        await client.SendPcmAsync(buffer.AsMemory(0, totalRead).ToArray(), ct);
                        frameCount++;
                    }

                    _logger.LogDebug("[VOICE-SVC] 🎬 EOF - sent {Count} frames", frameCount);
                    await ffmpeg.WaitForExitAsync(ct);
                    return;
                }

                totalRead += read;
            }

            await client.SendPcmAsync(buffer, ct);
            frameCount++;
        }
    }

    private Task<string> WaitForSessionIdAsync(ulong guildId, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string>();

        Task Handler(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            if (user.Id == _client.CurrentUser?.Id &&
                after.VoiceChannel?.Guild.Id == guildId &&
                !string.IsNullOrEmpty(after.VoiceSessionId))
            {
                _logger.LogDebug("[VOICE-SVC] 🔑 Session ID: {SessionId}", after.VoiceSessionId);
                tcs.TrySetResult(after.VoiceSessionId);
            }

            return Task.CompletedTask;
        }

        _client.UserVoiceStateUpdated += Handler;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, ct);
                tcs.TrySetException(new TimeoutException("Session ID timeout"));
            }
            catch { }
        });

        tcs.Task.ContinueWith(_ => _client.UserVoiceStateUpdated -= Handler);

        return tcs.Task;
    }

    private Task<(string token, string endpoint)> WaitForVoiceServerAsync(ulong guildId, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<(string, string)>();

        Task Handler(SocketVoiceServer vsu)
        {
            if (vsu.Guild.Id == guildId)
            {
                _logger.LogDebug("[VOICE-SVC] 🔑 Token received");
                tcs.TrySetResult((vsu.Token, vsu.Endpoint));
            }

            return Task.CompletedTask;
        }

        _client.VoiceServerUpdated += Handler;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, ct);
                tcs.TrySetException(new TimeoutException("VOICE_SERVER_UPDATE timeout"));
            }
            catch { }
        });

        tcs.Task.ContinueWith(_ => _client.VoiceServerUpdated -= Handler);

        return tcs.Task;
    }
}
*/