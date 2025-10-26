using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;

namespace Ogur.Sentinel.Worker.Services;


public sealed class VoiceService3
{
    private readonly GatewayClient _client;
    private readonly ILogger<VoiceService3> _logger;
    private readonly SemaphoreSlim _voiceLock = new(1, 1);

    public VoiceService3(GatewayClient client, ILogger<VoiceService3> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task JoinAndPlayAsync(
        ulong channelId, 
        string wavPath, 
        int repeatCount = 1, 
        int repeatGapMs = 250,
        CancellationToken ct = default)
    {
        var callId = Guid.NewGuid().ToString()[..8];
        _logger.LogDebug("[VOICE-SVC] 📥 JoinAndPlayAsync ENTRY - CallId: {CallId}", callId);

        await _voiceLock.WaitAsync(ct);

        try
        {
            // Validate file exists
            if (!File.Exists(wavPath))
            {
                _logger.LogWarning("[VOICE-SVC] ❌ Audio file not found: {Path}", wavPath);
                return;
            }

            // Find voice channel and guild
            VoiceGuildChannel? voiceChannel = null;
            ulong guildId = 0;
            
            foreach (var guild in _client.Cache?.Guilds.Values ?? Enumerable.Empty<Guild>())
            {
                var channel = guild.Channels.Values.OfType<VoiceGuildChannel>().FirstOrDefault(c => c.Id == channelId);
                if (channel != null)
                {
                    voiceChannel = channel;
                    guildId = guild.Id;
                    break;
                }
            }

            if (voiceChannel == null)
            {
                _logger.LogWarning("[VOICE-SVC] ❌ Voice channel {ChannelId} not found in cache", channelId);
                return;
            }

            _logger.LogDebug("[VOICE-SVC] ✅ Found voice channel: {Name} in guild {GuildId}", voiceChannel.Name, guildId);

            // Sprawdź czy bot już jest na jakimś kanale voice w tym guildzie
            var currentUserId = _client.Cache?.User?.Id;
            if (currentUserId.HasValue)
            {
                var guild = _client.Cache?.Guilds.Values.FirstOrDefault(g => g.Id == guildId);
                if (guild?.VoiceStates.TryGetValue(currentUserId.Value, out var voiceState) == true && voiceState.ChannelId.HasValue)
                {
                    _logger.LogWarning("[VOICE-SVC] ⚠️ Bot already in voice channel {CurrentChannelId}, disconnecting first...", voiceState.ChannelId);
                    
                    // Rozłącz się z obecnego kanału
                    await _client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null)
                    {
                        SelfMute = false,
                        SelfDeaf = false
                    });
                    
                    // Poczekaj dłużej na pełne rozłączenie (Discord potrzebuje czasu)
                    _logger.LogDebug("[VOICE-SVC] ⏳ Waiting 2s for full disconnect...");
                    await Task.Delay(2000, ct);
                    
                    _logger.LogDebug("[VOICE-SVC] ✅ Should be disconnected now");
                }
            }

            VoiceClient? voiceClient = null;

            try
            {
                // Connect to voice channel using extension method
                _logger.LogDebug("[VOICE-SVC] 🔌 Connecting to voice channel...");
                
                voiceClient = await _client.JoinVoiceChannelAsync(
                    guildId, 
                    channelId,
                    new VoiceClientConfiguration
                    {
                        // Zwiększ timeout
                        Logger = new VoiceLogger(_logger)
                    },
                    ct);
                
                // Start the voice client
                await voiceClient.StartAsync(ct);
                
                _logger.LogDebug("[VOICE-SVC] ✅ Voice client started, waiting for ready...");
                
                // Wait a bit for connection to stabilize
                await Task.Delay(500, ct);

                _logger.LogDebug("[VOICE-SVC] ✅ Voice client connected!");

                // Enter speaking state
                await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));
                
                _logger.LogDebug("[VOICE-SVC] ✅ Entered speaking state");

                // Play audio with repeats
                for (int i = 0; i < repeatCount; i++)
                {
                    _logger.LogDebug("[VOICE-SVC] 🎵 Playing {Current}/{Total}...", i + 1, repeatCount);
                    
                    await PlayWavAsync(voiceClient, wavPath, ct);

                    if (i + 1 < repeatCount)
                    {
                        _logger.LogTrace("[VOICE-SVC] ⏸️ Gap {Ms}ms before next play", repeatGapMs);
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
                    _logger.LogDebug("[VOICE-SVC] 🔌 Disconnecting voice client...");
                    voiceClient.Dispose();
                    _logger.LogTrace("[VOICE-SVC] ✅ Voice client disposed");
                }
                
                // Wyślij voice state update żeby rozłączyć się z kanału
                _logger.LogDebug("[VOICE-SVC] 🔌 Leaving voice channel...");
                await _client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null)
                {
                    SelfMute = false,
                    SelfDeaf = false
                });
                _logger.LogTrace("[VOICE-SVC] ✅ Left voice channel");
            }
        }
        finally
        {
            _voiceLock.Release();
        }
    }

    private async Task PlayWavAsync(VoiceClient voiceClient, string wavPath, CancellationToken ct)
    {
        _logger.LogDebug("[VOICE-SVC] 🎵 PlayWavAsync ENTRY - File: {Path}", wavPath);

        try
        {
            await using var reader = new WaveFileReader(wavPath);
            
            _logger.LogDebug("[VOICE-SVC] 📊 WAV Format: {Format}, {Channels}ch, {SampleRate}Hz", 
                reader.WaveFormat.Encoding, 
                reader.WaveFormat.Channels, 
                reader.WaveFormat.SampleRate);

            // NetCord wymaga konwersji do formatu Opus (PCM 48kHz stereo)
            IWaveProvider pcmProvider = reader;
            
            if (reader.WaveFormat.SampleRate != 48000 || reader.WaveFormat.Channels != 2)
            {
                _logger.LogDebug("[VOICE-SVC] 🔄 Resampling to 48kHz stereo...");
                
                // Konwertuj do sample provider
                ISampleProvider sampleProvider = reader.ToSampleProvider();
                
                // Mono -> Stereo jeśli potrzeba
                if (reader.WaveFormat.Channels == 1)
                {
                    sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
                }
                else if (reader.WaveFormat.Channels > 2)
                {
                    // Jeśli więcej niż 2 kanały, zmniejsz do stereo
                    sampleProvider = new MultiplexingSampleProvider(new[] { sampleProvider }, 2);
                }
                
                // Resample do 48kHz
                if (reader.WaveFormat.SampleRate != 48000)
                {
                    sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 48000);
                }
                
                // Konwertuj z powrotem do WaveProvider (16-bit PCM)
                pcmProvider = new SampleToWaveProvider16(sampleProvider);
            }

            // Utwórz output stream (Opus) - automatycznie enkoduje
            using var outputStream = voiceClient.CreateOutputStream();
            using var opusStream = new OpusEncodeStream(outputStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);
            
            _logger.LogDebug("[VOICE-SVC] 📤 Streaming audio...");

            // Kopiuj PCM data do Opus stream (automatycznie enkoduje i wysyła)
            byte[] buffer = new byte[3840]; // 20ms @ 48kHz stereo
            int bytesRead;
            
            while ((bytesRead = pcmProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                await opusStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            }

            await opusStream.FlushAsync(ct);
            
            _logger.LogTrace("[VOICE-SVC] ✅ Audio streaming complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE-SVC] ❌ PlayWavAsync failed");
            throw;
        }
    }
}

// Simple logger adapter for VoiceClient
internal class VoiceLogger : NetCord.Logging.IVoiceLogger
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public VoiceLogger(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger;
    }

    public bool IsEnabled(NetCord.Logging.LogLevel level)
    {
        var msLogLevel = level switch
        {
            NetCord.Logging.LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            NetCord.Logging.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            NetCord.Logging.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            NetCord.Logging.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            NetCord.Logging.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            NetCord.Logging.LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
        
        return _logger.IsEnabled(msLogLevel);
    }

    public void Log<TState>(NetCord.Logging.LogLevel level, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msLogLevel = level switch
        {
            NetCord.Logging.LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            NetCord.Logging.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            NetCord.Logging.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            NetCord.Logging.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            NetCord.Logging.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            NetCord.Logging.LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };

        _logger.Log(msLogLevel, exception, "[VOICE-CLIENT] {Message}", formatter(state, exception));
    }
}