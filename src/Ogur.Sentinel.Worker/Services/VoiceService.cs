using System.Diagnostics;
using System.Runtime.InteropServices;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Logging;

namespace Ogur.Sentinel.Worker.Services;

public sealed class VoiceService
{
    private readonly DiscordClient _client;
    private readonly ILogger<VoiceService> _logger;

    public VoiceService(DiscordClient client, ILogger<VoiceService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PlayOnceAsync(ulong channelId, string wavPath, CancellationToken ct)
    {
        VoiceNextConnection? conn = null;

        try
        {
            _logger.LogInformation("[Voice] Start PlayOnceAsync channel={ChannelId} path={Path}", channelId, wavPath);

            if (!File.Exists(wavPath))
            {
                _logger.LogWarning("[Voice] File not found: {Path}", wavPath);
                return;
            }

            await WaitReadyAsync(ct);
            _logger.LogInformation("[Voice] Client ready. Guilds={Count}", _client.Guilds.Count);

            if (!EnsureCodecsAvailable())
            {
                _logger.LogError("[Voice] Opus/Sodium missing");
                return;
            }

            var channel = await _client.GetChannelAsync(channelId);
            if (channel is null || channel.Type != DiscordChannelType.Voice)
            {
                _logger.LogWarning("[Voice] Invalid channel {ChannelId}", channelId);
                return;
            }

            var guild = channel.Guild ?? _client.Guilds.Values.FirstOrDefault(g => g.Channels.ContainsKey(channel.Id));
            if (guild is null)
            {
                _logger.LogWarning("[Voice] Cannot resolve guild for channel {ChannelId}", channelId);
                return;
            }

            var vnext = _client.GetVoiceNext();
            if (vnext is null)
            {
                _logger.LogError("[Voice] VoiceNext not enabled");
                return;
            }

            try { vnext.GetConnection(guild)?.Disconnect(); } catch { }
            await Task.Delay(300, ct);

            _logger.LogInformation("[Voice] Connecting to voice channel {ChannelId}", channelId);

            try
            {
                _logger.LogInformation("[Voice] Calling ConnectAsync with 30s patience...");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var connectTask = channel.ConnectAsync();

                var completedTask = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(30), cts.Token));

                if (completedTask == connectTask)
                {
                    conn = await connectTask;
                    if (conn != null)
                    {
                        _logger.LogInformation("[Voice] ConnectAsync completed successfully!");
                    }
                    else
                    {
                        _logger.LogError("[Voice] ConnectAsync returned null");
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning("[Voice] ConnectAsync timeout, trying GetConnection...");
                    conn = vnext.GetConnection(guild);

                    if (conn is null)
                    {
                        _logger.LogError("[Voice] GetConnection also returned null");
                        return;
                    }
                    _logger.LogWarning("[Voice] Got connection via GetConnection fallback");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Voice] Connect failed");
                return;
            }

            _logger.LogInformation("[Voice] Connection ready. TargetChannel={Target}", conn.TargetChannel?.Id ?? 0);

            await Task.Delay(500, ct);

            var transmit = conn.GetTransmitSink();

            using var ff = StartFfmpeg(wavPath);
            if (ff is null || ff.HasExited)
            {
                _logger.LogError("[Voice] ffmpeg failed to start");
                return;
            }

            try { await conn.SendSpeakingAsync(true); } catch { }

            await using var pcmStream = ff.StandardOutput.BaseStream;

            _logger.LogInformation("[Voice] Streaming audio: {Path}", Path.GetFileName(wavPath));

            var buffer = new byte[3840];
            int bytesRead;
            while ((bytesRead = await pcmStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await transmit.WriteAsync(buffer, 0, bytesRead, ct);
            }

            await transmit.FlushAsync(ct);

            _logger.LogInformation("[Voice] Streaming finished: {Path}", Path.GetFileName(wavPath));

            try { await conn.SendSpeakingAsync(false); } catch { }

            await Task.Delay(200, ct);
            conn.Disconnect();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[Voice] Streaming canceled");
            try { conn?.Disconnect(); } catch { }
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogError(ex, "[Voice] Native codecs missing");
            try { conn?.Disconnect(); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Voice] Unhandled exception");
            try { conn?.Disconnect(); } catch { }
        }
    }

    private async Task WaitReadyAsync(CancellationToken ct)
    {
        if (_client.Guilds.Count > 0)
            return;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handler(DiscordClient _, SessionCreatedEventArgs __)
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        }

        _client.SessionCreated += Handler;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
        }
        finally
        {
            _client.SessionCreated -= Handler;
        }
    }

    private static readonly string[] OpusCandidates =
        OperatingSystem.IsWindows() ? new[] { "opus.dll" } :
        OperatingSystem.IsMacOS() ? new[] { "libopus.0.dylib", "libopus.dylib" } :
        new[] { "libopus.so.0", "libopus.so" };

    private static readonly string[] SodiumCandidates =
        OperatingSystem.IsWindows() ? new[] { "libsodium.dll", "sodium.dll" } :
        OperatingSystem.IsMacOS() ? new[] { "libsodium.23.dylib", "libsodium.dylib" } :
        new[] { "libsodium.so.23", "libsodium.so" };

    private bool EnsureCodecsAvailable()
    {
        bool opusOk = TryLoadAny(OpusCandidates, out _);
        bool sodiumOk = TryLoadAny(SodiumCandidates, out _);

        if (opusOk && sodiumOk)
            return true;

        if (!opusOk)
            _logger.LogError("[Voice] Opus not found");
        if (!sodiumOk)
            _logger.LogError("[Voice] Sodium not found");

        return false;
    }

    private static bool TryLoadAny(string[] candidates, out string loadedName)
    {
        foreach (var name in candidates)
        {
            if (NativeLibrary.TryLoad(name, out var handle))
            {
                try { NativeLibrary.Free(handle); } catch { }
                loadedName = name;
                return true;
            }
        }
        loadedName = string.Empty;
        return false;
    }

    private static Process? StartFfmpeg(string wavPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel panic -i \"{wavPath}\" -ac 2 -f s16le -ar 48000 pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        return Process.Start(psi);
    }
}