/*
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
/// ULTRA VERBOSE VERSION FOR DEBUGGING 4006
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
        _logger.LogTrace("[VOICE-CLIENT] 🏗️ Constructor called");
    }

    public async Task ConnectAsync(ulong guildId, ulong userId, ulong channelId,
        string sessionId, string token, string endpoint, CancellationToken ct)
    {
        _logger.LogDebug("[VOICE-CLIENT] 📥 ConnectAsync ENTRY");
        _logger.LogDebug("[VOICE-CLIENT]   guildId: {GuildId} (type: {Type})", guildId, guildId.GetType().Name);
        _logger.LogDebug("[VOICE-CLIENT]   userId: {UserId} (type: {Type})", userId, userId.GetType().Name);
        _logger.LogDebug("[VOICE-CLIENT]   channelId: {ChannelId}", channelId);
        _logger.LogDebug("[VOICE-CLIENT]   sessionId: '{SessionId}' (len: {Len})", sessionId, sessionId?.Length ?? 0);
        _logger.LogDebug("[VOICE-CLIENT]   token: '{Token}' (len: {Len})", token, token?.Length ?? 0);
        _logger.LogDebug("[VOICE-CLIENT]   endpoint: '{Endpoint}'", endpoint);

        // Store params
        _logger.LogTrace("[VOICE-CLIENT] 💾 Storing parameters to instance fields...");
        _guildId = guildId;
        _userId = userId;
        _sessionId = sessionId;
        _token = token;
        _endpoint = endpoint;
        _logger.LogTrace("[VOICE-CLIENT] ✅ Parameters stored");

        // Create WebSocket
        _logger.LogDebug("[VOICE-CLIENT] 🔌 Creating ClientWebSocket instance...");
        _ws = new ClientWebSocket();
        _logger.LogTrace("[VOICE-CLIENT]   WebSocket created, State: {State}", _ws.State);

        // Build URI
        var uri = $"wss://{endpoint}/?v=4";
        _logger.LogDebug("[VOICE-CLIENT] 🌐 Connecting to: {Uri}", uri);

        try
        {
            var connectTask = _ws.ConnectAsync(new Uri(uri), ct);
            _logger.LogTrace("[VOICE-CLIENT]   ConnectAsync task started, waiting...");
            await connectTask;
            _logger.LogDebug("[VOICE-CLIENT] ✅ WebSocket connected, State: {State}", _ws.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE-CLIENT] ❌ WebSocket connect failed");
            _logger.LogDebug("[VOICE-CLIENT]   Exception type: {Type}", ex.GetType().Name);
            _logger.LogDebug("[VOICE-CLIENT]   Message: {Message}", ex.Message);
            throw;
        }

        // Build resume payload
        _logger.LogDebug("[VOICE-CLIENT] 📦 Building resume payload...");
        _logger.LogTrace("[VOICE-CLIENT]   Creating anonymous object...");
        
        var resume = new
        {
            op = 7,  // RESUME  
            d = new
            {
                server_id = guildId,
                session_id = sessionId,
                token = token
                // NOTE: NO user_id in RESUME!
            }
        };

        _logger.LogTrace("[VOICE-CLIENT]   resume object created, serializing...");
        var resumeJson = JsonSerializer.Serialize(resume);
        _logger.LogCritical("[VOICE-CLIENT] 🚨 RAW resume JSON: {Json}", resumeJson);
        _logger.LogDebug("[VOICE-CLIENT]   JSON length: {Len} bytes", resumeJson.Length);

        _logger.LogDebug("[VOICE-CLIENT] 🔍 resume Payload Breakdown:");
        _logger.LogDebug("[VOICE-CLIENT]   op: 0");
        _logger.LogDebug("[VOICE-CLIENT]   d.server_id: {ServerId} (raw type: {Type})", guildId, guildId.GetType().Name);
        _logger.LogDebug("[VOICE-CLIENT]   d.user_id: {UserId} (raw type: {Type})", userId, userId.GetType().Name);
        _logger.LogDebug("[VOICE-CLIENT]   d.session_id: '{SessionId}'", sessionId);
        _logger.LogDebug("[VOICE-CLIENT]   d.token: '{Token}'", token);

        // Send resume
        _logger.LogDebug("[VOICE-CLIENT] 📤 Sending resume to Discord...");
        var resumeBytes = Encoding.UTF8.GetBytes(resumeJson);
        _logger.LogTrace("[VOICE-CLIENT]   Byte array length: {Len}", resumeBytes.Length);
        _logger.LogTrace("[VOICE-CLIENT]   First 50 bytes (hex): {Hex}", 
            BitConverter.ToString(resumeBytes.Take(Math.Min(50, resumeBytes.Length)).ToArray()));

        try
        {
            await _ws.SendAsync(resumeBytes, WebSocketMessageType.Text, true, ct);
            _logger.LogDebug("[VOICE-CLIENT] ✅ resume sent successfully");
            _logger.LogTrace("[VOICE-CLIENT]   WebSocket State after send: {State}", _ws.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE-CLIENT] ❌ Failed to send resume");
            throw;
        }

        // Wait for READY
        _logger.LogDebug("[VOICE-CLIENT] ⏳ Calling WaitForReadyAsync()...");
        var (ssrc, ip, port, heartbeatInterval, encryptionMode) = await WaitForReadyAsync(ct);
        
        _logger.LogDebug("[VOICE-CLIENT] 📥 WaitForReadyAsync() returned:");
        _logger.LogDebug("[VOICE-CLIENT]   ssrc: {Ssrc} (type: {Type})", ssrc, ssrc.GetType().Name);
        _logger.LogDebug("[VOICE-CLIENT]   ip: '{Ip}'", ip);
        _logger.LogDebug("[VOICE-CLIENT]   port: {Port} (type: {Type})", port, port.GetType().Name);
        _logger.LogDebug("[VOICE-CLIENT]   heartbeatInterval: {HB}ms", heartbeatInterval);
        _logger.LogDebug("[VOICE-CLIENT]   encryptionMode: '{Mode}'", encryptionMode);

        _logger.LogTrace("[VOICE-CLIENT] 💾 Storing SSRC and heartbeat interval...");
        _ssrc = ssrc;
        _heartbeatMs = heartbeatInterval;
        _logger.LogTrace("[VOICE-CLIENT]   _ssrc = {Ssrc}", _ssrc);
        _logger.LogTrace("[VOICE-CLIENT]   _heartbeatMs = {HB}", _heartbeatMs);

        // UDP setup
        _logger.LogDebug("[VOICE-CLIENT] 🔌 Setting up UDP connection...");
        _logger.LogTrace("[VOICE-CLIENT]   Creating UdpClient...");
        _udp = new UdpClient();
        _logger.LogTrace("[VOICE-CLIENT]   Connecting to {Ip}:{Port}...", ip, port);
        _udp.Connect(ip, port);
        _logger.LogDebug("[VOICE-CLIENT] ✅ UDP connected to {Ip}:{Port}", ip, port);

        // IP Discovery
        _logger.LogDebug("[VOICE-CLIENT] 🔍 Starting IP discovery...");
        var discoveredIp = await DiscoverIpAsync(ct);
        _logger.LogDebug("[VOICE-CLIENT] ✅ IP discovery complete:");
        _logger.LogDebug("[VOICE-CLIENT]   Local IP: '{Ip}' (len: {Len})", discoveredIp.ip, discoveredIp.ip.Length);
        _logger.LogDebug("[VOICE-CLIENT]   Local Port: {Port} (type: {Type})", discoveredIp.port, discoveredIp.port.GetType().Name);

        // Build SELECT_PROTOCOL
        _logger.LogDebug("[VOICE-CLIENT] 📦 Building SELECT_PROTOCOL payload...");
        var selectProtocol = new
        {
            op = 1,
            d = new
            {
                protocol = "udp",
                data = new
                {
                    address = discoveredIp.ip,
                    port = (int)discoveredIp.port,
                    mode = encryptionMode
                }
            }
        };

        var selectJson = JsonSerializer.Serialize(selectProtocol);
        _logger.LogCritical("[VOICE-CLIENT] 🚨 RAW SELECT_PROTOCOL JSON: {Json}", selectJson);
        _logger.LogDebug("[VOICE-CLIENT]   protocol: 'udp'");
        _logger.LogDebug("[VOICE-CLIENT]   address: '{Address}'", discoveredIp.ip);
        _logger.LogDebug("[VOICE-CLIENT]   port: {Port} (cast to int from ushort)", (int)discoveredIp.port);
        _logger.LogDebug("[VOICE-CLIENT]   mode: '{Mode}'", encryptionMode);

        // Send SELECT_PROTOCOL
        _logger.LogDebug("[VOICE-CLIENT] 📤 Sending SELECT_PROTOCOL...");
        var selectBytes = Encoding.UTF8.GetBytes(selectJson);
        await _ws.SendAsync(selectBytes, WebSocketMessageType.Text, true, ct);
        _logger.LogDebug("[VOICE-CLIENT] ✅ SELECT_PROTOCOL sent");

        // Wait for SESSION_DESCRIPTION
        _logger.LogDebug("[VOICE-CLIENT] ⏳ Calling WaitForSessionDescriptionAsync()...");
        var (mode, secretKey) = await WaitForSessionDescriptionAsync(ct);
        _logger.LogDebug("[VOICE-CLIENT] 📥 WaitForSessionDescriptionAsync() returned:");
        _logger.LogDebug("[VOICE-CLIENT]   mode: '{Mode}'", mode);
        _logger.LogDebug("[VOICE-CLIENT]   secret_key length: {Len} bytes", secretKey.Length);
        _logger.LogTrace("[VOICE-CLIENT]   secret_key (hex): {Hex}", BitConverter.ToString(secretKey));

        _logger.LogTrace("[VOICE-CLIENT] 💾 Storing secret key...");
        _secretKey = secretKey;
        _logger.LogTrace("[VOICE-CLIENT]   _secretKey stored, length: {Len}", _secretKey.Length);

        // Initialize Opus encoder
        _logger.LogDebug("[VOICE-CLIENT] 🎵 Initializing Opus encoder...");
        _logger.LogTrace("[VOICE-CLIENT]   SampleRate: {Rate}, Channels: {Ch}, Application: AUDIO", SampleRate, Channels);
        _encoder = new OpusEncoder(SampleRate, Channels, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
        _logger.LogTrace("[VOICE-CLIENT]   Setting bitrate to {Kbps}kbps ({Bps} bps)", BitrateKbps, BitrateKbps * 1024);
        _encoder.Bitrate = BitrateKbps * 1024;
        _logger.LogDebug("[VOICE-CLIENT] ✅ Opus encoder initialized");

        // Start heartbeat
        _logger.LogDebug("[VOICE-CLIENT] ❤️ Starting heartbeat loop...");
        _logger.LogTrace("[VOICE-CLIENT]   Interval: {Ms}ms", _heartbeatMs);
        _logger.LogTrace("[VOICE-CLIENT]   Creating CancellationTokenSource...");
        _heartbeatCts = new CancellationTokenSource();
        _logger.LogTrace("[VOICE-CLIENT]   Starting HeartbeatLoopAsync task...");
        _heartbeatTask = HeartbeatLoopAsync(_heartbeatMs, _heartbeatCts.Token);
        _logger.LogDebug("[VOICE-CLIENT] ✅ Heartbeat loop started");

        // Send initial SPEAKING
        _logger.LogDebug("[VOICE-CLIENT] 🗣️ Sending initial SPEAKING=true...");
        await SendSpeakingAsync(true, ct);
        _logger.LogDebug("[VOICE-CLIENT] ✅ Initial SPEAKING sent");

        _logger.LogDebug("[VOICE-CLIENT] 🎉 ConnectAsync COMPLETE - Voice connection fully established!");
    }

    public async Task SendPcmAsync(byte[] pcm, CancellationToken ct = default)
    {
        _logger.LogTrace("[VOICE-CLIENT] 📥 SendPcmAsync ENTRY, bytes: {Len}", pcm.Length);

        if (_encoder == null || _udp == null)
        {
            _logger.LogError("[VOICE-CLIENT] ❌ Not connected: encoder={Encoder}, udp={Udp}", 
                _encoder != null, _udp != null);
            throw new InvalidOperationException("Not connected");
        }

        // Send SPEAKING
        _logger.LogTrace("[VOICE-CLIENT] 🗣️ Sending SPEAKING=true before audio...");
        await SendSpeakingAsync(true, ct);

        // Convert bytes to samples
        _logger.LogTrace("[VOICE-CLIENT] 🔄 Converting PCM bytes to samples...");
        var sampleCount = pcm.Length / 2;
        _logger.LogTrace("[VOICE-CLIENT]   Sample count: {Count}", sampleCount);
        var samples = new short[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2, 2));
        }
        _logger.LogTrace("[VOICE-CLIENT] ✅ Converted {Count} samples", sampleCount);

        // Encode and send frames
        var frameSamples = FrameSize * Channels;
        var opusBuffer = new byte[4000];
        var frameCount = 0;

        _logger.LogDebug("[VOICE-CLIENT] 🎵 Starting frame encoding loop...");
        _logger.LogTrace("[VOICE-CLIENT]   Frame size: {Size} samples, Total samples: {Total}", frameSamples, samples.Length);

        for (int offset = 0; offset < samples.Length; offset += frameSamples)
        {
            var remaining = Math.Min(frameSamples, samples.Length - offset);
            _logger.LogTrace("[VOICE-CLIENT]   Frame {N}: offset={Off}, remaining={Rem}", frameCount, offset, remaining);

            if (remaining < frameSamples)
            {
                _logger.LogTrace("[VOICE-CLIENT]     Padding last frame with {Pad} silent samples", frameSamples - remaining);
                var padded = new short[frameSamples];
                Array.Copy(samples, offset, padded, 0, remaining);
                samples = padded;
                offset = 0;
            }

            // Encode
            _logger.LogTrace("[VOICE-CLIENT]     Encoding frame...");
            var encoded = _encoder.Encode(samples, offset, FrameSize, opusBuffer, 0, opusBuffer.Length);
            _logger.LogTrace("[VOICE-CLIENT]     Encoded {Bytes} bytes", encoded);

            // Send RTP
            await SendRtpPacketAsync(opusBuffer.AsMemory(0, encoded), ct);
            
            _timestamp += FrameSize;
            frameCount++;
        }

        _logger.LogDebug("[VOICE-CLIENT] ✅ Sent {Count} frames", frameCount);
    }

    private async Task SendSpeakingAsync(bool speaking, CancellationToken ct)
    {
        _logger.LogTrace("[VOICE-CLIENT] 🗣️ SendSpeakingAsync: {Speaking}", speaking);
        
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

        var json = JsonSerializer.Serialize(payload);
        _logger.LogTrace("[VOICE-CLIENT]   SPEAKING JSON: {Json}", json);
        
        await SendJsonAsync(payload, ct);
        _logger.LogTrace("[VOICE-CLIENT] ✅ SPEAKING sent");
    }

    private async Task SendRtpPacketAsync(Memory<byte> opus, CancellationToken ct)
    {
        _logger.LogTrace("[VOICE-CLIENT] 📤 SendRtpPacketAsync ENTRY, opus bytes: {Len}", opus.Length);
    
        const int headerSize = 12;
        var nonce = new byte[24];

        // RTP Header
        _logger.LogTrace("[VOICE-CLIENT]   Creating RTP header...");
        var header = new byte[headerSize];
        header[0] = 0x80; // Version 2
        header[1] = 0x78; // Payload type 120 (Opus)

        _logger.LogTrace("[VOICE-CLIENT]   Sequence: {Seq}", _sequence);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), _sequence++);
    
        _logger.LogTrace("[VOICE-CLIENT]   Timestamp: {Ts}", _timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), _timestamp);
    
        _logger.LogTrace("[VOICE-CLIENT]   SSRC: {Ssrc}", _ssrc);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), _ssrc);

        // Nonce = RTP header + 12 zero bytes
        _logger.LogTrace("[VOICE-CLIENT]   Creating nonce from header...");
        Array.Copy(header, 0, nonce, 0, headerSize);

        // Encrypt opus data with libsodium XSalsa20Poly1305
        _logger.LogTrace("[VOICE-CLIENT]   Encrypting {Bytes} bytes with xsalsa20_poly1305...", opus.Length);
        var encrypted = SecretBox.Create(opus.ToArray(), nonce, _secretKey);
        _logger.LogTrace("[VOICE-CLIENT]   Encrypted length: {Len}", encrypted.Length);

        // Final packet = header + encrypted
        _logger.LogTrace("[VOICE-CLIENT]   Building final packet...");
        var packet = new byte[headerSize + encrypted.Length];
        Array.Copy(header, 0, packet, 0, headerSize);
        Array.Copy(encrypted, 0, packet, headerSize, encrypted.Length);
        _logger.LogTrace("[VOICE-CLIENT]   Final packet length: {Len}", packet.Length);

        _logger.LogTrace("[VOICE-CLIENT]   Sending UDP packet...");
        await _udp!.SendAsync(packet, ct);
        _logger.LogTrace("[VOICE-CLIENT] ✅ RTP packet sent");
    }

    private async Task<(uint ssrc, string ip, int port, int heartbeatInterval, string encryptionMode)> WaitForReadyAsync(CancellationToken ct)
    {
        _logger.LogDebug("[VOICE-CLIENT] 🔍 WaitForReadyAsync ENTRY - waiting for READY event...");
        
        while (true)
        {
            _logger.LogTrace("[VOICE-CLIENT]   Calling ReceiveJsonAsync()...");
            var msg = await ReceiveJsonAsync(ct);
            
            if (!msg.TryGetProperty("op", out var opProp))
            {
                _logger.LogWarning("[VOICE-CLIENT]   Message has no 'op' field, skipping");
                continue;
            }

            var op = opProp.GetInt32();
            _logger.LogDebug("[VOICE-CLIENT]   Received opcode: {Op}", op);

            if (op == 8) // HELLO
            {
                _logger.LogDebug("[VOICE-CLIENT] 👋 HELLO received");
                var d = msg.GetProperty("d");
                var hbInterval = d.GetProperty("heartbeat_interval").GetDouble();
                _logger.LogDebug("[VOICE-CLIENT]   heartbeat_interval from HELLO: {HB}ms", hbInterval);
                continue;
            }

            if (op == 2) // READY
            {
                _logger.LogDebug("[VOICE-CLIENT] ✅ READY received!");
                _logger.LogCritical("[VOICE-CLIENT] 🚨 FULL READY PAYLOAD: {Json}", msg.GetRawText());
                
                var d = msg.GetProperty("d");

                // Parse encryption mode
                string encryptionMode = "xsalsa20_poly1305";
                if (d.TryGetProperty("modes", out var modesArray))
                {
                    _logger.LogDebug("[VOICE-CLIENT]   Available encryption modes:");
                    bool foundMode = false;
                    for (int i = 0; i < modesArray.GetArrayLength(); i++)
                    {
                        var mode = modesArray[i].GetString();
                        _logger.LogDebug("[VOICE-CLIENT]     [{Index}] {Mode}", i, mode);
            
                        if (mode == "xsalsa20_poly1305" && !foundMode)
                        {
                            encryptionMode = mode;
                            foundMode = true;
                        }
                    }
                    _logger.LogDebug("[VOICE-CLIENT]   Selected: {Mode}", encryptionMode);
                }

                // Parse SSRC
                var ssrcProp = d.GetProperty("ssrc");
                _logger.LogTrace("[VOICE-CLIENT]   SSRC property ValueKind: {Kind}", ssrcProp.ValueKind);
                var ssrc = ssrcProp.ValueKind == JsonValueKind.String
                    ? uint.Parse(ssrcProp.GetString()!)
                    : ssrcProp.GetUInt32();
                _logger.LogDebug("[VOICE-CLIENT]   SSRC: {Ssrc}", ssrc);

                // Parse IP
                var ip = d.GetProperty("ip").GetString()!;
                _logger.LogDebug("[VOICE-CLIENT]   IP: '{Ip}'", ip);

                // Parse port
                var portProp = d.GetProperty("port");
                _logger.LogTrace("[VOICE-CLIENT]   Port property ValueKind: {Kind}", portProp.ValueKind);
                var port = portProp.ValueKind == JsonValueKind.String
                    ? int.Parse(portProp.GetString()!)
                    : portProp.GetInt32();
                _logger.LogDebug("[VOICE-CLIENT]   Port: {Port}", port);

                // Parse heartbeat interval
                int heartbeatInterval = 41250;
                if (d.TryGetProperty("heartbeat_interval", out var hbProp))
                {
                    _logger.LogTrace("[VOICE-CLIENT]   HB property ValueKind: {Kind}", hbProp.ValueKind);
                    heartbeatInterval = hbProp.ValueKind switch
                    {
                        JsonValueKind.Number => (int)hbProp.GetDouble(),
                        JsonValueKind.String => int.Parse(hbProp.GetString()!),
                        _ => 41250
                    };
                }
                _logger.LogDebug("[VOICE-CLIENT]   Heartbeat interval: {HB}ms", heartbeatInterval);

                _logger.LogDebug("[VOICE-CLIENT] 📤 WaitForReadyAsync RETURN");
                return (ssrc, ip, port, heartbeatInterval, encryptionMode);
            }

            _logger.LogTrace("[VOICE-CLIENT]   Skipping opcode {Op}, continuing...", op);
        }
    }

    private async Task<(string ip, ushort port)> DiscoverIpAsync(CancellationToken ct)
    {
        _logger.LogDebug("[VOICE-CLIENT] 🔍 DiscoverIpAsync ENTRY");
        
        // Build packet
        _logger.LogTrace("[VOICE-CLIENT]   Creating 74-byte IP discovery packet...");
        var packet = new byte[74];
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(0), 0x1);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), 70);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), _ssrc);
        _logger.LogTrace("[VOICE-CLIENT]   Packet: Type=0x1, Length=70, SSRC={Ssrc}", _ssrc);

        // Send
        _logger.LogTrace("[VOICE-CLIENT]   Sending UDP packet...");
        await _udp!.SendAsync(packet, ct);
        _logger.LogTrace("[VOICE-CLIENT]   Sent, waiting for response...");

        // Receive
        var response = await _udp.ReceiveAsync(ct);
        var data = response.Buffer;
        _logger.LogTrace("[VOICE-CLIENT]   Received {Len} bytes", data.Length);

        // Parse IP
        var ipStart = 8;
        var ipEnd = Array.IndexOf(data, (byte)0, ipStart);
        var ip = Encoding.ASCII.GetString(data, ipStart, ipEnd - ipStart);
        _logger.LogDebug("[VOICE-CLIENT]   Parsed IP: '{Ip}' (bytes {Start}-{End})", ip, ipStart, ipEnd);

        // Parse port
        var port = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(data.Length - 2));
        _logger.LogDebug("[VOICE-CLIENT]   Parsed Port: {Port}", port);

        _logger.LogDebug("[VOICE-CLIENT] 📤 DiscoverIpAsync RETURN");
        return (ip, port);
    }

    private async Task<(string mode, byte[] secret_key)> WaitForSessionDescriptionAsync(CancellationToken ct)
    {
        _logger.LogDebug("[VOICE-CLIENT] 🔍 WaitForSessionDescriptionAsync ENTRY");
        
        while (true)
        {
            _logger.LogTrace("[VOICE-CLIENT]   Calling ReceiveJsonAsync()...");
            var msg = await ReceiveJsonAsync(ct);

            if (!msg.TryGetProperty("op", out var opProp))
            {
                _logger.LogWarning("[VOICE-CLIENT]   Message has no 'op' field");
                continue;
            }

            var op = opProp.GetInt32();
            _logger.LogDebug("[VOICE-CLIENT]   Received opcode: {Op}", op);

            if (op == 4) // SESSION_DESCRIPTION
            {
                _logger.LogDebug("[VOICE-CLIENT] ✅ SESSION_DESCRIPTION received");
                _logger.LogCritical("[VOICE-CLIENT] 🚨 FULL SESSION_DESCRIPTION: {Json}", msg.GetRawText());
                
                var d = msg.GetProperty("d");
                var mode = d.GetProperty("mode").GetString()!;
                _logger.LogDebug("[VOICE-CLIENT]   Mode: '{Mode}'", mode);
                
                var keyArray = d.GetProperty("secret_key");
                var keyLen = keyArray.GetArrayLength();
                _logger.LogDebug("[VOICE-CLIENT]   Secret key array length: {Len}", keyLen);
                
                var key = new byte[keyLen];
                for (int i = 0; i < key.Length; i++)
                    key[i] = keyArray[i].GetByte();
                
                _logger.LogDebug("[VOICE-CLIENT] 📤 WaitForSessionDescriptionAsync RETURN");
                return (mode, key);
            }

            _logger.LogTrace("[VOICE-CLIENT]   Skipping opcode {Op}, continuing...", op);
        }
    }

    private async Task HeartbeatLoopAsync(int intervalMs, CancellationToken ct)
    {
        _logger.LogDebug("[VOICE-CLIENT] ❤️ HeartbeatLoopAsync ENTRY, interval: {Ms}ms", intervalMs);
        
        try
        {
            while (!ct.IsCancellationRequested)
            {
                _logger.LogTrace("[VOICE-CLIENT]   Waiting {Ms}ms...", intervalMs);
                await Task.Delay(intervalMs, ct);

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var heartbeat = new { op = 3, d = timestamp };
                _logger.LogTrace("[VOICE-CLIENT]   Sending heartbeat (op=3, d={Ts})...", timestamp);
                
                await SendJsonAsync(heartbeat, ct);
                _logger.LogTrace("[VOICE-CLIENT]   ❤️ Heartbeat sent");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[VOICE-CLIENT] ❤️ Heartbeat cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VOICE-CLIENT] ❌ Heartbeat failed");
        }
        
        _logger.LogDebug("[VOICE-CLIENT] ❤️ HeartbeatLoopAsync EXIT");
    }

    private async Task SendJsonAsync(object payload, CancellationToken ct)
    {
        _logger.LogTrace("[VOICE-CLIENT] 📤 SendJsonAsync ENTRY");
        
        var json = JsonSerializer.Serialize(payload);
        _logger.LogTrace("[VOICE-CLIENT]   JSON: {Json}", json);
        _logger.LogTrace("[VOICE-CLIENT]   Length: {Len} bytes", json.Length);
        
        var bytes = Encoding.UTF8.GetBytes(json);
        _logger.LogTrace("[VOICE-CLIENT]   Encoded to {Len} bytes", bytes.Length);
        _logger.LogTrace("[VOICE-CLIENT]   WebSocket State before send: {State}", _ws?.State);
        
        await _ws!.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        _logger.LogTrace("[VOICE-CLIENT]   Sent, WebSocket State after send: {State}", _ws.State);
    }

    private async Task<JsonElement> ReceiveJsonAsync(CancellationToken ct)
    {
        _logger.LogTrace("[VOICE-CLIENT] 📥 ReceiveJsonAsync ENTRY");
        _logger.LogTrace("[VOICE-CLIENT]   WebSocket State: {State}", _ws?.State);
        
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            _logger.LogError("[VOICE-CLIENT] ❌ WebSocket not open: {State}", _ws?.State);
            throw new InvalidOperationException($"WebSocket is in {_ws?.State} state");
        }

        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        var chunkCount = 0;

        do
        {
            _logger.LogTrace("[VOICE-CLIENT]   Receiving chunk {N}...", chunkCount++);
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            _logger.LogTrace("[VOICE-CLIENT]     Received {Bytes} bytes, MessageType: {Type}, EndOfMessage: {End}",
                result.Count, result.MessageType, result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogError("[VOICE-CLIENT] ❌ Discord closed WebSocket!");
                _logger.LogError("[VOICE-CLIENT]   CloseStatus: {Status}", result.CloseStatus);
                _logger.LogError("[VOICE-CLIENT]   CloseStatusDescription: '{Desc}'", result.CloseStatusDescription);
                throw new WebSocketException($"Discord closed connection: {result.CloseStatusDescription}");
            }

            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        ms.Seek(0, SeekOrigin.Begin);
        var json = Encoding.UTF8.GetString(ms.ToArray());
        _logger.LogDebug("[VOICE-CLIENT] 📥 Received complete message, length: {Len} chars", json.Length);
        _logger.LogTrace("[VOICE-CLIENT]   Raw JSON: {Json}", json.Length > 1000 ? json.Substring(0, 1000) + "..." : json);

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("[VOICE-CLIENT] ⚠️ Received empty message");
            return default;
        }

        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            _logger.LogTrace("[VOICE-CLIENT] ✅ JSON parsed successfully");
            return element;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[VOICE-CLIENT] ❌ JSON parse failed");
            _logger.LogError("[VOICE-CLIENT]   Raw (first 500 chars): {Raw}", 
                json.Length > 500 ? json.Substring(0, 500) : json);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("[VOICE-CLIENT] 🧹 DisposeAsync ENTRY");
        
        // Cancel heartbeat
        if (_heartbeatCts != null)
        {
            _logger.LogTrace("[VOICE-CLIENT]   Cancelling heartbeat...");
            _heartbeatCts.Cancel();
            
            if (_heartbeatTask != null)
            {
                _logger.LogTrace("[VOICE-CLIENT]   Waiting for heartbeat task...");
                await _heartbeatTask;
            }
        }

        // Close UDP
        if (_udp != null)
        {
            _logger.LogTrace("[VOICE-CLIENT]   Disposing UDP...");
            _udp.Dispose();
        }

        // Close WebSocket
        if (_ws != null)
        {
            _logger.LogTrace("[VOICE-CLIENT]   Closing WebSocket (State: {State})...", _ws.State);
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VOICE-CLIENT]   WebSocket close failed");
            }

            _logger.LogTrace("[VOICE-CLIENT]   Disposing WebSocket...");
            _ws.Dispose();
        }

        // Dispose encoder
        if (_encoder != null)
        {
            _logger.LogTrace("[VOICE-CLIENT]   Disposing Opus encoder...");
            _encoder.Dispose();
        }

        _logger.LogDebug("[VOICE-CLIENT] 🧹 DisposeAsync COMPLETE");
    }
}
*/