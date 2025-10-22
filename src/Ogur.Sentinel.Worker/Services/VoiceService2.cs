using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Worker.Discord;
using System.Threading;

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
    private readonly SemaphoreSlim _voiceLock = new(1, 1);

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
    await _voiceLock.WaitAsync(ct);
    
    try
    {
        var callId = Guid.NewGuid().ToString().Substring(0, 8);
        _logger.LogWarning(
            "[VOICE] 🆔 JoinAndPlayAsync START - CallId: {CallId}, Channel: {ChannelId}, Repeat: {Repeat}",
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
        
        var currentVoiceChannel = targetVc.Guild.CurrentUser?.VoiceChannel;
        if (currentVoiceChannel != null)
        {
            _logger.LogWarning("[VOICE] ⚠️ Bot already in voice channel {Ch}, forcing disconnect...", currentVoiceChannel.Id);
            try 
            { 
                await currentVoiceChannel.DisconnectAsync(); 
                await Task.Delay(1000, ct); // Poczekaj na full disconnect
            }
            catch { }
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
            // Step 1: Connect to voice channel with EXTERNAL flag
            var voiceServerTask = WaitForVoiceServerAsync(targetVc.Guild.Id, ct);

            if (ConnectAudioInternal != null)
            {
                _logger.LogDebug("[VOICE] 📞 Connecting with external=true flag...");
                var task = (Task)ConnectAudioInternal.Invoke(targetVc.Guild, new object[]
                {
                    channelId,
                    true,  // selfDeaf
                    false, // selfMute
                    true,  // external
                    false  // disconnect
                })!;

                await task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            else
            {
                _logger.LogError("[VOICE] ❌ Cannot access ConnectAudioAsync via reflection!");
                return;
            }

            _logger.LogDebug("[VOICE] ⏳ Waiting for VOICE_SERVER_UPDATE...");

            // Step 2: Wait for VOICE_SERVER_UPDATE event
            var voiceServer = await voiceServerTask;

            // Step 3: Get session_id from bot's voice state
            await Task.Delay(500, ct);
            var me = targetVc.Guild.CurrentUser as SocketGuildUser;
            var sessionId = me?.VoiceSessionId;

            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogError("[VOICE] ❌ Missing session_id after voice state update");
                return;
            }

            _logger.LogInformation("[VOICE] 🔑 SessionId: {SessionId}", sessionId);
            _logger.LogInformation("[VOICE] 🎫 Token: {Token}", voiceServer.token);
            _logger.LogInformation("[VOICE] 🌐 Endpoint: {Endpoint}", voiceServer.endpoint);

            // Step 4: Connect voice client
            _logger.LogInformation("[VOICE] 🔌 Creating voice client...");
            voiceClient = new DiscordVoiceClient(_voiceLogger);

            await voiceClient.ConnectAsync(
                targetVc.Guild.Id,
                _client.CurrentUser.Id,
                channelId,
                sessionId,
                voiceServer.token,
                voiceServer.endpoint,
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE] ❌ Failed to play audio");
        }
        finally
        {
            // Cleanup
            if (voiceClient != null)
                await voiceClient.DisposeAsync();

            // Disconnect from voice
            try { await targetVc.DisconnectAsync(); }
            catch { }
    
            _logger.LogWarning("[VOICE] 🆔 JoinAndPlayAsync END - CallId: {CallId}", callId);
        }
    }
    finally
    {
        _voiceLock.Release();
    }
}
private async Task PlayWavAsync(DiscordVoiceClient client, string wavPath, CancellationToken ct)
{
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
            
                _logger.LogDebug("[VOICE] 🎬 EOF - sent {Count} frames total", frameCount);
                await ffmpeg.WaitForExitAsync(ct);
                return;
            }
            totalRead += read;
        }

        await client.SendPcmAsync(buffer, ct);
        frameCount++;
    }
}

    private Task<(string token, string endpoint)> WaitForVoiceServerAsync(ulong guildId, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<(string, string)>();

        Task Handler(SocketVoiceServer vsu)
        {
            if (vsu.Guild.Id == guildId)
            {
                _logger.LogDebug("[VOICE] 📨 VoiceServerUpdate received: token={Token}, endpoint={Endpoint}",
                    vsu.Token, vsu.Endpoint);
                tcs.TrySetResult((vsu.Token, vsu.Endpoint));
            }

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