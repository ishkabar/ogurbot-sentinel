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

    public async Task JoinAndPlayAsync(ulong channelId, string wavPath, int repeatCount = 1, int repeatGapMs = 250,
    CancellationToken ct = default)
{
    var callId = Guid.NewGuid().ToString().Substring(0, 8);
    _logger.LogWarning("[VOICE] 🆔 JoinAndPlayAsync START - CallId: {CallId}, Channel: {ChannelId}, Repeat: {Repeat}", 
        callId, channelId, repeatCount);
    
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

    _logger.LogInformation("[VOICE] 🎤 Joining #{Channel} in {Guild}", targetVc.Name, targetVc.Guild.Name);

    DiscordVoiceClient? voiceClient = null;

    try
    {
        // Step 1: Setup tasks BEFORE connecting
        var voiceServerTcs = new TaskCompletionSource<(string token, string endpoint)>();
        var voiceStateTcs = new TaskCompletionSource<string>(); // session_id
        
        Task VoiceServerHandler(SocketVoiceServer vs)
        {
            _logger.LogWarning("[VOICE] 🔔 VoiceServerHandler triggered - Guild: {Guild}", vs.Guild.Id);
    
            if (vs.Guild.Id == targetVc.Guild.Id)
            {
                _logger.LogWarning("[VOICE] 🔍 VoiceServer RAW:");
                _logger.LogWarning("[VOICE]   Token: '{Token}'", vs.Token ?? "NULL");
                _logger.LogWarning("[VOICE]   Token Length: {Len}", vs.Token?.Length ?? 0);
                _logger.LogWarning("[VOICE]   Endpoint: '{Ep}'", vs.Endpoint ?? "NULL");
        
                _logger.LogInformation("[VOICE] ✅ VoiceServerUpdate - Endpoint: {Ep}", vs.Endpoint);
                voiceServerTcs.TrySetResult((vs.Token!, vs.Endpoint!));
            }
            else
            {
                _logger.LogWarning("[VOICE] Guild mismatch: got {Got}, expected {Expected}", vs.Guild.Id, targetVc.Guild.Id);
            }
    
            return Task.CompletedTask;
        }
        
        Task VoiceStateHandler(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            _logger.LogDebug("[VOICE] 🔔 VoiceStateUpdate: User={UserId}, OldChannel={Old}, NewChannel={New}", 
                user.Id, oldState.VoiceChannel?.Id, newState.VoiceChannel?.Id);
    
            if (user.Id == _client.CurrentUser.Id)
            {
                _logger.LogDebug("[VOICE] 🤖 VoiceState update for BOT itself");
        
                if (newState.VoiceChannel?.Id == channelId)
                {
                    var sessionId = newState.VoiceSessionId;
            
                    _logger.LogInformation("[VOICE] 🔍 Bot joined target channel. SessionId: '{Sid}' (length: {Len}, IsNullOrEmpty: {Empty})", 
                        sessionId, sessionId?.Length ?? 0, string.IsNullOrEmpty(sessionId));
            
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        _logger.LogInformation("[VOICE] ✅ VoiceStateUpdate - Valid SessionId: {Sid}", sessionId);
                        voiceStateTcs.TrySetResult(sessionId);
                    }
                    else
                    {
                        _logger.LogWarning("[VOICE] ⚠️ VoiceStateUpdate - SessionId is null or empty!");
                    }
                }
                else
                {
                    _logger.LogDebug("[VOICE] Bot joined different channel: {ActualChannel} (expected: {ExpectedChannel})", 
                        newState.VoiceChannel?.Id, channelId);
                }
            }
    
            return Task.CompletedTask;
        }

        _client.VoiceServerUpdated += VoiceServerHandler;
        _client.UserVoiceStateUpdated += VoiceStateHandler;

        try
        {
            //_logger.LogDebug("[VOICE] Connecting (normal mode - testing)...");
            //await targetVc.ConnectAsync(selfDeaf: true, selfMute: false);
            // Step 2: Connect with external=true
            
            if (ConnectAudioInternal != null)
            {
                _logger.LogDebug("[VOICE] 📞 Connecting with external=true...");
                var task = (Task)ConnectAudioInternal.Invoke(targetVc.Guild, new object[]
                {
                    channelId,
                    true,  // selfDeaf
                    false, // selfMute
                    true,  // external - CRITICAL! Prevents AudioClient creation
                    false  // disconnect
                })!;

                await task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            else
            {
                _logger.LogError("[VOICE] ❌ Cannot access ConnectAudioAsync!");
                return;
            }

            // Step 3: Wait for BOTH events with timeout
            _logger.LogDebug("[VOICE] ⏳ Waiting for voice events...");
            
            var timeout = Task.Delay(10000, ct);
            var voiceServerTask = voiceServerTcs.Task;
            var voiceStateTask = voiceStateTcs.Task;
            
            await Task.WhenAny(Task.WhenAll(voiceServerTask, voiceStateTask), timeout);
            
            if (!voiceServerTask.IsCompleted || !voiceStateTask.IsCompleted)
            {
                _logger.LogError("[VOICE] ❌ Timeout waiting for voice events");
                return;
            }

            var (token, endpoint) = await voiceServerTask;
            var sessionId = await voiceStateTask;

            _logger.LogInformation("[VOICE] 🔑 SessionId: {SessionId}", sessionId);
            _logger.LogInformation("[VOICE] 🎫 Token preview: {Token}...", 
                token.Substring(0, Math.Min(15, token.Length)));

            // Step 4: Connect IMMEDIATELY - no delay!
            _logger.LogInformation("[VOICE] 🔌 Creating voice client...");
            voiceClient = new DiscordVoiceClient(_voiceLogger);
            
            await voiceClient.ConnectAsync(
                targetVc.Guild.Id,
                _client.CurrentUser.Id,
                channelId,
                sessionId,
                token,
                endpoint,
                ct);

            _logger.LogInformation("[VOICE] ✅ Voice client connected!");

            // Step 5: Play audio
            for (int i = 0; i < repeatCount; i++)
            {
                _logger.LogInformation("[VOICE] 🎵 Playing #{Play}/{Total}...", i + 1, repeatCount);
                await PlayWavAsync(voiceClient, wavPath, ct);

                if (i + 1 < repeatCount)
                {
                    _logger.LogDebug("[VOICE] ⏸️ Gap {Ms}ms...", repeatGapMs);
                    await Task.Delay(repeatGapMs, ct);
                }
            }

            _logger.LogInformation("[VOICE] ✅ Played {File} {Count}x on #{Channel}",
                Path.GetFileName(wavPath), repeatCount, targetVc.Name);
        }
        finally
        {
            _client.VoiceServerUpdated -= VoiceServerHandler;
            _client.UserVoiceStateUpdated -= VoiceStateHandler;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[VOICE] ❌ Failed to play audio");
    }
    finally
    {
        if (voiceClient != null)
        {
            _logger.LogDebug("[VOICE] 🧹 Disposing voice client...");
            await voiceClient.DisposeAsync();
        }

        try
        {
            _logger.LogDebug("[VOICE] 👋 Disconnecting...");
            await targetVc.DisconnectAsync();
        }
        catch { }
    }
    
    _logger.LogWarning("[VOICE] 🆔 JoinAndPlayAsync END - CallId: {CallId}", callId);
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
            catch
            {
            }
        });

        tcs.Task.ContinueWith(_ => _client.VoiceServerUpdated -= Handler);

        return tcs.Task;
    }
}