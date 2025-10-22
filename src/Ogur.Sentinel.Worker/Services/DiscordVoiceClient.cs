using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Concentus.Structs;
using Microsoft.Extensions.Logging;
using Sodium;

namespace Ogur.Sentinel.Worker.Services;

/// <summary>
/// Manual Discord Voice WebSocket + UDP implementation
/// Bypasses Discord.NET completely to avoid 4006 errors
/// </summary>
public sealed class DiscordVoiceClient : IAsyncDisposable
{
    private readonly ILogger<DiscordVoiceClient> _logger;
    private ClientWebSocket? _ws;
    private UdpClient? _udp;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private int _heartbeatMs;

    private string? _sessionId;
    private string? _token;
    private ulong _guildId;
    private ulong _userId;
    private string? _endpoint;

    private uint _ssrc;
    private ushort _sequence;
    private uint _timestamp;
    private byte[] _secretKey = Array.Empty<byte>();

    private OpusEncoder? _encoder;
    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const int FrameSize = 960; // 20ms at 48kHz
    private const int BitrateKbps = 128;

 

    public bool IsConnected => _ws?.State == WebSocketState.Open && _udp != null;

    public DiscordVoiceClient(ILogger<DiscordVoiceClient> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(ulong guildId, ulong userId, ulong channelId,
        string sessionId, string token, string endpoint, CancellationToken ct)
    {
        _logger.LogInformation("[VOICE] 🔌 Opening WebSocket to wss://{Endpoint}/?v=4", endpoint);

        _ws = new ClientWebSocket();

        try
        {
            await _ws.ConnectAsync(new Uri($"wss://{endpoint}/?v=4"), ct);
            _logger.LogInformation("[VOICE] ✅ WebSocket connected, state: {State}", _ws.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE] ❌ Failed to connect WebSocket");
            throw;
        }

        // ✅ Send IDENTIFY immediately (like old working version)
        var identify = new
        {
            op = 0,
            d = new
            {
                server_id = guildId.ToString(),
                user_id = userId.ToString(),
                session_id = sessionId,
                token = token // ✅ Use token directly without conversion
            }
        };

        var identifyJson = JsonSerializer.Serialize(identify);

        _logger.LogWarning("[VOICE] 🔍 IDENTIFY Debug:");
        _logger.LogWarning("[VOICE]   server_id: {ServerId}", guildId);
        _logger.LogWarning("[VOICE]   user_id: {UserId}", userId);
        _logger.LogWarning("[VOICE]   session_id: {SessionId}", sessionId);
        _logger.LogWarning("[VOICE]   token: {Token}", token);
        _logger.LogInformation("[VOICE] 📤 Sending IDENTIFY (immediately after connect)");

        await _ws.SendAsync(
            Encoding.UTF8.GetBytes(identifyJson),
            WebSocketMessageType.Text,
            true,
            ct);

        _logger.LogInformation("[VOICE] ⏳ Waiting for READY...");

        // Wait for READY (will skip HELLO in the loop)
        var (ssrc, ip, port, heartbeatInterval, encryptionMode) = await WaitForReadyAsync(ct);
        _ssrc = ssrc;
        _heartbeatMs = heartbeatInterval;

        _logger.LogInformation("[VOICE] ✅ READY received - SSRC: {Ssrc}, IP: {Ip}, Port: {Port}, HB: {Hb}ms",
            ssrc, ip, port, heartbeatInterval);

        // UDP setup
        _logger.LogDebug("[VOICE] 🔌 Setting up UDP to {Ip}:{Port}...", ip, port);
        _udp = new UdpClient();
        _udp.Connect(ip, port);

        // IP Discovery
        _logger.LogDebug("[VOICE] 🔍 Starting IP discovery...");
        var discoveredIp = await DiscoverIpAsync(ct);
        _logger.LogInformation("[VOICE] ✅ Discovered local IP: {Ip}:{Port}", discoveredIp.ip, discoveredIp.port);

        // Send SELECT_PROTOCOL with EXTENSIVE LOGGING
        var selectProtocol = new
        {
            op = 1,
            d = new
            {
                protocol = "udp",
                data = new
                {
                    address = discoveredIp.ip,
                    port = (int)discoveredIp.port, // Cast ushort to int explicitly
                    mode = encryptionMode
                }
            }
        };

        var selectJson = JsonSerializer.Serialize(selectProtocol);
        _logger.LogCritical("[VOICE] 🔬 SELECT_PROTOCOL Full JSON: {Json}", selectJson);
        _logger.LogWarning("[VOICE] 📤 Sending SELECT_PROTOCOL...");
        _logger.LogWarning("[VOICE]   address: {Address}", discoveredIp.ip);
        _logger.LogWarning("[VOICE]   port: {Port} (type: {Type})", discoveredIp.port,
            discoveredIp.port.GetType().Name);
        _logger.LogWarning("[VOICE]   mode: xsalsa20_poly1305");

        await _ws.SendAsync(
            Encoding.UTF8.GetBytes(selectJson),
            WebSocketMessageType.Text,
            true,
            ct);

        // Wait for SESSION_DESCRIPTION (op=4) with DEBUG
        _logger.LogWarning("[VOICE] ⏳ Waiting for SESSION_DESCRIPTION (op=4)...");
        var (mode, secretKey) = await WaitForSessionDescriptionAsync(ct);
        _secretKey = secretKey;
        _logger.LogInformation("[VOICE] ✅ Got secret key, length: {Length}, mode: {Mode}", secretKey.Length, mode);

        // ✅ Initialize Opus encoder (was missing!)
        _logger.LogDebug("[VOICE] 🎵 Initializing Opus encoder...");
        _encoder = new OpusEncoder(SampleRate, Channels, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
        _encoder.Bitrate = BitrateKbps * 1024;

        // Start heartbeat loop
        _logger.LogDebug("[VOICE] ❤️ Starting heartbeat loop ({Ms}ms)...", _heartbeatMs);
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = HeartbeatLoopAsync(_heartbeatMs, _heartbeatCts.Token);

        // ✅ Send SPEAKING indicator (was missing!)
        await SendSpeakingAsync(true, ct);

        _logger.LogInformation("[VOICE] ✅ Voice connection fully established!");
    }

    /// <summary>
    /// Send PCM audio data (16-bit stereo 48kHz)
    /// </summary>
    public async Task SendPcmAsync(byte[] pcm, CancellationToken ct = default)
    {
        if (_encoder == null || _udp == null)
            throw new InvalidOperationException("Not connected");

        _logger.LogInformation("[VOICE] SendPcmAsync called with {Bytes} bytes", pcm.Length);

        // Send SPEAKING before audio
        await SendSpeakingAsync(true, ct);

        // Opus expects samples (short[])
        var sampleCount = pcm.Length / 2;
        var samples = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2, 2));
        }

        // Encode in 20ms frames (960 samples per channel = 1920 samples stereo)
        var frameSamples = FrameSize * Channels;
        var opusBuffer = new byte[4000];
        var frameCount = 0;

        for (int offset = 0; offset < samples.Length; offset += frameSamples)
        {
            var remaining = Math.Min(frameSamples, samples.Length - offset);
            if (remaining < frameSamples)
            {
                // Pad last frame with silence
                var padded = new short[frameSamples];
                Array.Copy(samples, offset, padded, 0, remaining);
                samples = padded;
                offset = 0;
            }

            // Encode to Opus
            var encoded = _encoder.Encode(samples, offset, FrameSize, opusBuffer, 0, opusBuffer.Length);

            // Send RTP packet
            await SendRtpPacketAsync(opusBuffer.AsMemory(0, encoded), ct);

            _timestamp += FrameSize;
            frameCount++;

            // Rate limiting: 20ms per frame
            await Task.Delay(20, ct);
        }

        _logger.LogInformation("[VOICE] Sent {Count} audio frames", frameCount);

        // Stop speaking after audio
        await SendSpeakingAsync(false, ct);
    }

    /// <summary>
    /// Send RTP packet over UDP with XSalsa20Poly1305 encryption
    /// </summary>
    private async Task SendRtpPacketAsync(Memory<byte> opus, CancellationToken ct)
    {
        const int headerSize = 12;
        var nonce = new byte[24];

        // RTP Header
        var header = new byte[headerSize];
        header[0] = 0x80; // Version 2
        header[1] = 0x78; // Payload type 120 (Opus)

        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), _sequence++);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), _timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), _ssrc);

        // Nonce = RTP header + 12 zero bytes
        Array.Copy(header, 0, nonce, 0, headerSize);

        // Encrypt opus data with libsodium XSalsa20Poly1305
        var encrypted = SecretBox.Create(opus.ToArray(), nonce, _secretKey);

        // Final packet = header + encrypted
        var packet = new byte[headerSize + encrypted.Length];
        Array.Copy(header, 0, packet, 0, headerSize);
        Array.Copy(encrypted, 0, packet, headerSize, encrypted.Length);

        await _udp!.SendAsync(packet, ct);
    }

    private async Task SendSpeakingAsync(bool speaking, CancellationToken ct)
    {
        var payload = new
        {
            op = 5,
            d = new
            {
                speaking = speaking ? 1 : 0,
                delay = 0,
                ssrc = _ssrc
            }
        };

        await SendJsonAsync(payload, ct);
        _logger.LogDebug("[VOICE] Sent SPEAKING: {Speaking}", speaking);
    }

    private async Task<(uint ssrc, string ip, int port, int heartbeatInterval, string encryptionMode)> WaitForReadyAsync(CancellationToken ct)
    {
        while (true)
        {
            var msg = await ReceiveJsonAsync(ct);

            if (!msg.TryGetProperty("op", out var opProp))
            {
                _logger.LogWarning("[VOICE] Message has no 'op' field!");
                continue;
            }

            var op = opProp.ValueKind == JsonValueKind.String
                ? int.Parse(opProp.GetString()!)
                : opProp.GetInt32();

            if (op == 8) // HELLO
            {
                _logger.LogInformation("[VOICE] 👋 HELLO received, continuing...");
                continue; // Skip HELLO, wait for READY
            }

            if (op == 2) // READY
            {
                _logger.LogInformation("[VOICE] 🎯 Got READY (op=2)");
                var d = msg.GetProperty("d");

                // ✅ LOG FULL READY PAYLOAD
                _logger.LogCritical("[VOICE] 🔬 READY Full payload: {Json}", d.GetRawText());

                // ✅ Extract and log available encryption modes
                string encryptionMode = "xsalsa20_poly1305"; // default fallback

                if (d.TryGetProperty("modes", out var modesArray) && modesArray.GetArrayLength() > 0)
                {
                    _logger.LogCritical("[VOICE] 🔐 Available encryption modes:");
        
                    // ✅ Szukaj xsalsa20_poly1305 w liście
                    bool foundMode = false;
                    for (int i = 0; i < modesArray.GetArrayLength(); i++)
                    {
                        var mode = modesArray[i].GetString();
                        _logger.LogCritical("[VOICE]   [{Index}] {Mode}", i, mode);
            
                        // ✅ Wybierz xsalsa20_poly1305 jeśli jest dostępny
                        if (mode == "xsalsa20_poly1305" && !foundMode)
                        {
                            encryptionMode = mode;
                            foundMode = true;
                        }
                    }
        
                    _logger.LogCritical("[VOICE] ✅ Selected encryption mode: {Mode}", encryptionMode);
                }
                else
                {
                    _logger.LogWarning("[VOICE] ⚠️ No 'modes' field in READY! Using default: {Mode}", encryptionMode);
                }

                var ssrcProp = d.GetProperty("ssrc");
                var ssrc = ssrcProp.ValueKind == JsonValueKind.String
                    ? uint.Parse(ssrcProp.GetString()!)
                    : ssrcProp.GetUInt32();

                var ip = d.GetProperty("ip").GetString()!;

                var portProp = d.GetProperty("port");
                var port = portProp.ValueKind == JsonValueKind.String
                    ? int.Parse(portProp.GetString()!)
                    : portProp.GetInt32();

                // Get heartbeat_interval from READY
                int heartbeatInterval = 41250; // default
                if (d.TryGetProperty("heartbeat_interval", out var hbProp))
                {
                    heartbeatInterval = hbProp.ValueKind switch
                    {
                        JsonValueKind.Number => (int)hbProp.GetDouble(),
                        JsonValueKind.String => int.Parse(hbProp.GetString()!),
                        _ => 41250
                    };
                }

                _logger.LogInformation("[VOICE] 🎯 SSRC: {Ssrc}, IP: {Ip}, Port: {Port}, HB: {Hb}ms, Mode: {Mode}",
                    ssrc, ip, port, heartbeatInterval, encryptionMode);

                return (ssrc, ip, port, heartbeatInterval, encryptionMode);
            }

            _logger.LogDebug("[VOICE] ⏭️ Skipping opcode {Op}", op);
        }
    }

    private async Task<(string ip, ushort port)> DiscoverIpAsync(CancellationToken ct)
    {
        // IP Discovery packet: 74 bytes
        var packet = new byte[74];
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(0), 0x1); // Type
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), 70); // Length
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), _ssrc);

        await _udp!.SendAsync(packet, ct);

        var response = await _udp.ReceiveAsync(ct);
        var data = response.Buffer;

        // Parse null-terminated IP string starting at byte 8
        var ipStart = 8;
        var ipEnd = Array.IndexOf(data, (byte)0, ipStart);
        var ip = Encoding.ASCII.GetString(data, ipStart, ipEnd - ipStart);

        var port = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(data.Length - 2));

        return (ip, port);
    }

    private async Task<(string mode, byte[] secret_key)> WaitForSessionDescriptionAsync(CancellationToken ct)
    {
        while (true)
        {
            _logger.LogDebug("[VOICE] 🔍 Waiting for next WebSocket message...");
            var msg = await ReceiveJsonAsync(ct);

            _logger.LogCritical("[VOICE] 📨 Received message: {Json}", msg.GetRawText());

            if (!msg.TryGetProperty("op", out var opProp))
            {
                _logger.LogWarning("[VOICE] Message has no 'op' field!");
                continue;
            }

            var op = opProp.GetInt32();
            _logger.LogWarning("[VOICE] Opcode: {Op}", op);

            if (op == 4) // SESSION_DESCRIPTION
            {
                _logger.LogInformation("[VOICE] ✅ Got SESSION_DESCRIPTION (op=4)");
                var d = msg.GetProperty("d");
                var mode = d.GetProperty("mode").GetString()!;
                var keyArray = d.GetProperty("secret_key");
                var key = new byte[keyArray.GetArrayLength()];
                for (int i = 0; i < key.Length; i++)
                    key[i] = keyArray[i].GetByte();
                return (mode, key);
            }

            _logger.LogDebug("[VOICE] ⏭️ Skipping opcode {Op}, continuing...", op);
        }
    }

    private async Task HeartbeatLoopAsync(int intervalMs, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(intervalMs, ct);

                var heartbeat = new { op = 3, d = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                await SendJsonAsync(heartbeat, ct);
                _logger.LogTrace("[VOICE] Heartbeat sent");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE] Heartbeat failed");
        }
    }

    private async Task SendJsonAsync(object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws!.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private async Task<JsonElement> ReceiveJsonAsync(CancellationToken ct)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            _logger.LogWarning("[VOICE] ⚠️ WebSocket is not open (state: {State})", _ws?.State);
            throw new InvalidOperationException($"WebSocket is in {_ws?.State} state");
        }

        using var ms = new MemoryStream();
        var buffer = new byte[8192];

        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogWarning("[VOICE] ❌ Discord closed WebSocket. Status: {Status}, Reason: {Reason}",
                    result.CloseStatus, result.CloseStatusDescription);
                throw new WebSocketException($"Discord closed connection: {result.CloseStatusDescription}");
            }

            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        ms.Seek(0, SeekOrigin.Begin);
        var json = Encoding.UTF8.GetString(ms.ToArray());

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("[VOICE] ⚠️ Received empty WebSocket message");
            return default;
        }

        _logger.LogDebug("[VOICE] 📥 Received: {Json}", json.Length > 500 ? json.Substring(0, 500) + "..." : json);

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[VOICE] ❌ Failed to parse JSON. Raw (first 200 chars): {Raw}",
                json.Length > 200 ? json.Substring(0, 200) : json);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _heartbeatCts?.Cancel();
        if (_heartbeatTask != null)
            await _heartbeatTask;

        _udp?.Dispose();

        if (_ws != null)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }
            catch
            {
            }

            _ws.Dispose();
        }

        _encoder?.Dispose();
    }
}