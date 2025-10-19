using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Worker.Discord;

namespace Ogur.Sentinel.Worker.Services;

/// <summary>
/// Voice service using manual Discord Voice implementation (no Discord.NET voice)
/// </summary>
public sealed class VoiceService2
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordReadyService _ready;
    private readonly ILogger<VoiceService2> _logger;
    private readonly ILogger<DiscordVoiceClient> _voiceLogger;

    private static readonly MethodInfo? ConnectAudioInternal =
        typeof(SocketGuild).GetMethod("ConnectAudioAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public VoiceService2(
        DiscordSocketClient client,
        DiscordReadyService ready,
        ILogger<VoiceService2> logger,
        ILogger<DiscordVoiceClient> voiceLogger)
    {
        _client = client;
        _ready = ready;
        _logger = logger;
        _voiceLogger = voiceLogger;
    }

    public async Task JoinAndPlayAsync(ulong channelId, string wavPath, CancellationToken ct = default)
    {
        await _ready.WaitForStableAsync(ct);

        if (_client.ConnectionState != ConnectionState.Connected || _client.CurrentUser is null)
        {
            _logger.LogWarning("[VOICE] Discord not connected");
            return;
        }

        if (_client.GetChannel(channelId) is not SocketVoiceChannel targetVc)
        {
            _logger.LogWarning("[VOICE] Channel {ChannelId} not found", channelId);
            return;
        }

        if (!File.Exists(wavPath))
        {
            _logger.LogWarning("[VOICE] Audio file not found: {Path}", wavPath);
            return;
        }

        _logger.LogInformation("[VOICE] Joining #{Channel} ({Guild})", targetVc.Name, targetVc.Guild.Name);

        DiscordVoiceClient? voiceClient = null;

        try
        {
            // Step 1: Connect to voice channel with EXTERNAL flag (no AudioClient!)
            var voiceServerTask = WaitForVoiceServerAsync(targetVc.Guild.Id, ct);
            
            // Use reflection to call ConnectAudioAsync with external=true
            if (ConnectAudioInternal != null)
            {
                _logger.LogDebug("[VOICE] Connecting with external=true flag...");
                var task = (Task)ConnectAudioInternal.Invoke(targetVc.Guild, new object[]
                {
                    channelId,
                    true,  // selfDeaf
                    false, // selfMute
                    true,  // external - CRITICAL! This prevents AudioClient creation
                    false  // disconnect
                })!;
                
                await task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            else
            {
                _logger.LogError("[VOICE] Cannot access ConnectAudioAsync via reflection!");
                return;
            }

            _logger.LogDebug("[VOICE] Waiting for VOICE_SERVER_UPDATE...");

            // Step 2: Wait for VOICE_SERVER_UPDATE event
            var voiceServer = await voiceServerTask;
            
            // Step 3: Get session_id from bot's voice state
            await Task.Delay(300, ct);
            var me = targetVc.Guild.CurrentUser as SocketGuildUser;
            var sessionId = me?.VoiceSessionId;

            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogError("[VOICE] Missing session_id after voice state update");
                return;
            }

            _logger.LogInformation("[VOICE] SessionId={Sid} Endpoint={Ep}", sessionId, voiceServer.endpoint);

            // Step 4: Connect our custom voice client
            voiceClient = new DiscordVoiceClient(_voiceLogger);
            await voiceClient.ConnectAsync(
                targetVc.Guild.Id,
                _client.CurrentUser.Id,
                channelId,
                sessionId,
                voiceServer.token,
                voiceServer.endpoint,
                ct);

            // Step 5: Convert WAV to PCM and send
            await PlayWavAsync(voiceClient, wavPath, ct);

            _logger.LogInformation("[VOICE] ✓ Played {File} on #{Channel}", Path.GetFileName(wavPath), targetVc.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE] Failed to play audio");
        }
        finally
        {
            // Cleanup
            if (voiceClient != null)
                await voiceClient.DisposeAsync();

            // Disconnect from voice
            try { await targetVc.DisconnectAsync(); }
            catch { }
        }
    }

    private async Task PlayWavAsync(DiscordVoiceClient client, string wavPath, CancellationToken ct)
    {
        // Use ffmpeg to convert to 48kHz stereo PCM
        using var ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel error -i \"{wavPath}\" -ac 2 -f s16le -ar 48000 pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false
        });

        if (ffmpeg == null || ffmpeg.HasExited)
        {
            _logger.LogError("[VOICE] ffmpeg failed to start");
            return;
        }

        // Read PCM in chunks
        var buffer = new byte[3840]; // 20ms of stereo 48kHz PCM (960 samples * 2 channels * 2 bytes)
        using var pcmStream = ffmpeg.StandardOutput.BaseStream;

        while (!ct.IsCancellationRequested)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await pcmStream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
                if (read == 0) // EOF
                    return;
                totalRead += read;
            }

            await client.SendPcmAsync(buffer, ct);
        }
    }

    private Task<(string token, string endpoint)> WaitForVoiceServerAsync(ulong guildId, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<(string, string)>();
        
        Task Handler(SocketVoiceServer vsu)
        {
            if (vsu.Guild.Id == guildId)
                tcs.TrySetResult((vsu.Token, vsu.Endpoint));
            return Task.CompletedTask;
        }

        _client.VoiceServerUpdated += Handler;

        // Timeout after 5 seconds
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, ct);
                tcs.TrySetException(new TimeoutException("VOICE_SERVER_UPDATE not received"));
            }
            catch { }
        });

        tcs.Task.ContinueWith(_ => _client.VoiceServerUpdated -= Handler);

        return tcs.Task;
    }
}