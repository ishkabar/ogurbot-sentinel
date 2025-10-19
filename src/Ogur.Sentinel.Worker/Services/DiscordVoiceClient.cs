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

    /// <summary>
    /// Connect to Discord Voice
    /// </summary>
    public async Task ConnectAsync(
        ulong guildId,
        ulong userId,
        ulong channelId,
        string sessionId,
        string token,
        string endpoint,
        CancellationToken ct = default)
    {
        _guildId = guildId;
        _userId = userId;
        _sessionId = sessionId;
        _token = token;
        _endpoint = endpoint?.Replace(":80", "");

        if (string.IsNullOrEmpty(_endpoint))
            throw new InvalidOperationException("Voice endpoint is null");

        _logger.LogInformation("[VOICE] Connecting to {Endpoint}", _endpoint);

        // 1. WebSocket connection
        _ws = new ClientWebSocket();
        var wsUrl = $"wss://{_endpoint}/?v=4";
        await _ws.ConnectAsync(new Uri(wsUrl), ct);
        _logger.LogDebug("[VOICE] WebSocket connected");

        // 2. Send IDENTIFY
        await SendIdentifyAsync(ct);

        // 3. Wait for READY opcode 2
        var ready = await WaitForReadyAsync(ct);
        _ssrc = ready.ssrc;
        var ip = ready.ip;
        var port = ready.port;

        _logger.LogInformation("[VOICE] READY: ssrc={Ssrc} ip={Ip} port={Port}", _ssrc, ip, port);

        // 4. UDP connection for RTP
        _udp = new UdpClient();
        _udp.Connect(ip, port);

        // 5. IP Discovery
        var (localIp, localPort) = await DiscoverIpAsync(ct);
        _logger.LogDebug("[VOICE] IP Discovery: {Ip}:{Port}", localIp, localPort);

        // 6. Send SELECT_PROTOCOL
        await SendSelectProtocolAsync(localIp, localPort, ct);

        // 7. Wait for SESSION_DESCRIPTION (opcode 4)
        var sessionDesc = await WaitForSessionDescriptionAsync(ct);
        _secretKey = sessionDesc.secret_key;
        _logger.LogInformation("[VOICE] Got secret key ({Len} bytes)", _secretKey.Length);

        // 8. Start heartbeat
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = HeartbeatLoopAsync(ready.heartbeat_interval, _heartbeatCts.Token);

        // 9. Initialize Opus encoder
        _encoder = new OpusEncoder(SampleRate, Channels, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
        _encoder.Bitrate = BitrateKbps * 1024;

        // 10. Send SPEAKING indicator
        await SendSpeakingAsync(true, ct);

        _logger.LogInformation("[VOICE] ✓ Connected and ready to send audio!");
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

    private async Task SendIdentifyAsync(CancellationToken ct)
    {
        var identify = new
        {
            op = 0,
            d = new
            {
                server_id = _guildId.ToString(),
                user_id = _userId.ToString(),
                session_id = _sessionId,
                token = _token
            }
        };

        await SendJsonAsync(identify, ct);
        _logger.LogDebug("[VOICE] Sent IDENTIFY");
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

    private async Task<(uint ssrc, string ip, int port, int heartbeat_interval)> WaitForReadyAsync(CancellationToken ct)
    {
        while (true)
        {
            var msg = await ReceiveJsonAsync(ct);
            
            // DUMP CAŁEGO JSON
            _logger.LogInformation("[VOICE] RAW JSON: {Json}", msg.GetRawText());
            
            if (!msg.TryGetProperty("op", out var opProp))
            {
                _logger.LogWarning("[VOICE] Message has no 'op' field!");
                continue;
            }
                
            var op = opProp.ValueKind == JsonValueKind.String 
                ? int.Parse(opProp.GetString()!) 
                : opProp.GetInt32();
            
            _logger.LogInformation("[VOICE] Opcode: {Op}", op);
            
            if (op == 8) // HELLO
            {
                _logger.LogInformation("[VOICE] HELLO received, continuing...");
                continue;
            }
            
            if (op == 2) // READY
            {
                _logger.LogInformation("[VOICE] READY received!");
                var d = msg.GetProperty("d");
                
                _logger.LogInformation("[VOICE] d section: {D}", d.GetRawText());
                
                var ssrcProp = d.GetProperty("ssrc");
                _logger.LogInformation("[VOICE] ssrc raw: {Ssrc} (type: {Type})", ssrcProp.GetRawText(), ssrcProp.ValueKind);
                var ssrc = ssrcProp.ValueKind == JsonValueKind.String
                    ? uint.Parse(ssrcProp.GetString()!)
                    : ssrcProp.GetUInt32();
                    
                var ip = d.GetProperty("ip").GetString()!;
                
                var portProp = d.GetProperty("port");
                _logger.LogInformation("[VOICE] port raw: {Port} (type: {Type})", portProp.GetRawText(), portProp.ValueKind);
                var port = portProp.ValueKind == JsonValueKind.String
                    ? int.Parse(portProp.GetString()!)
                    : portProp.GetInt32();
                
                var hb = 41250;
                
                return (ssrc, ip, port, hb);
            }
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

    private async Task SendSelectProtocolAsync(string ip, ushort port, CancellationToken ct)
    {
        var select = new
        {
            op = 1,
            d = new
            {
                protocol = "udp",
                data = new
                {
                    address = ip,
                    port = port,
                    mode = "xsalsa20_poly1305"
                }
            }
        };

        await SendJsonAsync(select, ct);
        _logger.LogDebug("[VOICE] Sent SELECT_PROTOCOL");
    }

    private async Task<(string mode, byte[] secret_key)> WaitForSessionDescriptionAsync(CancellationToken ct)
    {
        while (true)
        {
            var msg = await ReceiveJsonAsync(ct);
            var op = msg.GetProperty("op").GetInt32();
            if (op == 4) // SESSION_DESCRIPTION
            {
                var d = msg.GetProperty("d");
                var mode = d.GetProperty("mode").GetString()!;
                var keyArray = d.GetProperty("secret_key");
                var key = new byte[keyArray.GetArrayLength()];
                for (int i = 0; i < key.Length; i++)
                    key[i] = keyArray[i].GetByte();
                return (mode, key);
            }
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
        catch (OperationCanceledException) { }
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
        var buffer = new byte[8192];
        var result = await _ws!.ReceiveAsync(buffer, ct);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    public async ValueTask DisposeAsync()
    {
        _heartbeatCts?.Cancel();
        if (_heartbeatTask != null)
            await _heartbeatTask;

        _udp?.Dispose();

        if (_ws != null)
        {
            try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None); }
            catch { }
            _ws.Dispose();
        }

        _encoder?.Dispose();
    }
}