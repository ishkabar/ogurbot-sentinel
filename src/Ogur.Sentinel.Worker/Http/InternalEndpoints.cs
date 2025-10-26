using System;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Abstractions.Options;
using Ogur.Sentinel.Abstractions.Respawn;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Worker.Services;
using Ogur.Sentinel.Abstractions;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using Ogur.Sentinel.Worker.Discord;
using NetCord;
using System.Linq;
using NetCord.Gateway.Voice;


namespace Ogur.Sentinel.Worker.Http;

public sealed class InternalEndpoints
{
    private readonly RespawnState _state;
    private readonly RespawnSchedulerService _scheduler;
    private readonly SettingsStore _store;
    private readonly SettingsOptions _opts;
    private readonly GatewayClient _client;
    private readonly RespawnOptions _respawnOpts;
    private readonly WikiSyncService _wikiSync;
    private readonly VoiceService3 _voice;
    private readonly IVersionHelper _versionHelper;


    public InternalEndpoints(
        RespawnState state,
        RespawnSchedulerService scheduler,
        SettingsStore store,
        IOptions<SettingsOptions> opts,
        GatewayClient client,
        IOptions<RespawnOptions> respawnOpts,
        WikiSyncService wikiSync,
        VoiceService3 voice,
        IVersionHelper versionHelper)
    {
        _state = state;
        _scheduler = scheduler;
        _store = store;
        _opts = opts.Value;
        _client = client;
        _respawnOpts = respawnOpts.Value;
        _wikiSync = wikiSync;
        _voice = voice;
        _versionHelper = versionHelper;
    }

    public void Map(IHost app)
    {
        var web = app.Services.GetRequiredService<IConfiguration>()["Urls"] ?? "http://0.0.0.0:9090";

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(web);
        builder.Services.AddRouting();

        var webapp = builder.Build();

        webapp.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

        webapp.MapGet("/settings", (ILogger<InternalEndpoints> logger) =>
        {
            var result = new
            {
                roles_allowed = _state.RolesAllowed,
                channels = _state.Channels.Select(c => c.ToString()).ToArray(),//_state.Channels,
                base_hhmm = _state.BaseHhmm,
                lead_seconds = _state.LeadSeconds,
                enabled_10m = _state.Enabled10m,
                enabled_2h = _state.Enabled2h,
                use_synced_time = _state.UseSyncedTime,
                synced_base_time = _state.SyncedBaseTime,
                last_sync_at = _state.LastSyncAt,
                repeat_plays_10m = _state.RepeatPlays10m,
                repeat_gap_ms_10m = _state.RepeatGapMs10m,
                repeat_plays_2h = _state.RepeatPlays2h,
                repeat_gap_ms_2h = _state.RepeatGapMs2h
            };

            logger.LogInformation("📤 GET /settings: RepeatPlays10m={RepeatPlays10m}, RepeatPlays2h={RepeatPlays2h}",
                _state.RepeatPlays10m, _state.RepeatPlays2h);

            return Results.Ok(result);
        });

        webapp.MapPost("/settings", async (HttpContext ctx, ILogger<InternalEndpoints> logger) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();

            logger.LogInformation("📝 POST /settings received: {Json}", json);

            var doc = JsonDocument.Parse(json);

            var channels = doc.RootElement.GetProperty("channels").EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String
                    ? ulong.Parse(x.GetString()!)
                    : (ulong)x.GetUInt64())
                .ToList();

            var settings = new PersistedSettings
            {
                Channels = channels,
                BaseHhmm = doc.RootElement.GetProperty("base_hhmm").GetString()!,
                LeadSeconds = doc.RootElement.GetProperty("lead_seconds").GetInt32(),
                RolesAllowed = new List<ulong>(),
                Enabled10m = _state.Enabled10m,
                Enabled2h = _state.Enabled2h,
                UseSyncedTime = doc.RootElement.TryGetProperty("use_synced_time", out var useSynced)
                    ? useSynced.GetBoolean()
                    : _state.UseSyncedTime,
                SyncedBaseTime = _state.SyncedBaseTime,
                LastSyncAt = _state.LastSyncAt,

                // ✅ Nowe pola zamiast starych
                RepeatPlays10m = doc.RootElement.TryGetProperty("repeat_plays_10m", out var rp10m)
                    ? rp10m.GetInt32()
                    : _state.RepeatPlays10m,
                RepeatGapMs10m = doc.RootElement.TryGetProperty("repeat_gap_ms_10m", out var rg10m)
                    ? rg10m.GetInt32()
                    : _state.RepeatGapMs10m,
                RepeatPlays2h = doc.RootElement.TryGetProperty("repeat_plays_2h", out var rp2h)
                    ? rp2h.GetInt32()
                    : _state.RepeatPlays2h,
                RepeatGapMs2h = doc.RootElement.TryGetProperty("repeat_gap_ms_2h", out var rg2h)
                    ? rg2h.GetInt32()
                    : _state.RepeatGapMs2h
            };

            _state.ApplyPersisted(settings);
            await _store.SaveAsync(_state.ToPersisted());
            _state.NotifySettingsChanged();

            logger.LogInformation("✅ Settings saved: RepeatPlays10m={RepeatPlays10m}, RepeatPlays2h={RepeatPlays2h}",
                settings.RepeatPlays10m, settings.RepeatPlays2h);

            return Results.Ok();
        });

        webapp.MapPatch("/settings", async (HttpContext ctx, ILogger<InternalEndpoints> logger) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();
            var doc = JsonDocument.Parse(json);

            // Aktualizuj tylko przesłane pola
            if (doc.RootElement.TryGetProperty("channels", out var channelsElem))
            {
                var channels = channelsElem.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String
                        ? ulong.Parse(x.GetString()!)
                        : (ulong)x.GetUInt64())
                    .ToList();
                _state.SetChannels(channels);
            }

            if (doc.RootElement.TryGetProperty("base_hhmm", out var baseElem))
                _state.BaseHhmm = baseElem.GetString()!;

            if (doc.RootElement.TryGetProperty("lead_seconds", out var leadElem))
                _state.LeadSeconds = leadElem.GetInt32();

            if (doc.RootElement.TryGetProperty("enabled_10m", out var e10m))
                _state.Enabled10m = e10m.GetBoolean();

            if (doc.RootElement.TryGetProperty("enabled_2h", out var e2h))
                _state.Enabled2h = e2h.GetBoolean();

            if (doc.RootElement.TryGetProperty("use_synced_time", out var useSynced))
                _state.UseSyncedTime = useSynced.GetBoolean();

            if (doc.RootElement.TryGetProperty("repeat_plays_10m", out var rp10m))
                _state.RepeatPlays10m = rp10m.GetInt32();

            if (doc.RootElement.TryGetProperty("repeat_gap_ms_10m", out var rg10m))
                _state.RepeatGapMs10m = rg10m.GetInt32();

            if (doc.RootElement.TryGetProperty("repeat_plays_2h", out var rp2h))
                _state.RepeatPlays2h = rp2h.GetInt32();

            if (doc.RootElement.TryGetProperty("repeat_gap_ms_2h", out var rg2h))
                _state.RepeatGapMs2h = rg2h.GetInt32();

            await _store.SaveAsync(_state.ToPersisted());
            _state.NotifySettingsChanged();
            return Results.Ok();
        });

        webapp.MapPost("/respawn/recalculate", () =>
        {
            _scheduler.RecalculateNext();
            var nowLocal = DateTimeOffset.Now;
            var (n10, n2h) = _scheduler.ComputeNext(nowLocal);
            return Results.Ok(new
            {
                message = "Recalculated",
                next10m = n10,
                next2h = n2h
            });
        });

        webapp.MapGet("/respawn/next", () =>
        {
            var nowLocal = DateTimeOffset.Now;
            var (n10, n2h) = _scheduler.ComputeNext(nowLocal);
            return Results.Ok(new { next10m = n10, next2h = n2h });
        });

        webapp.MapPost("/respawn/sync", async () =>
        {
            var success = await _wikiSync.SyncAsync();
            return Results.Ok(new
            {
                success,
                synced_time = _state.SyncedBaseTime,
                last_sync = _state.LastSyncAt
            });
        });

        webapp.MapPost("/respawn/toggle", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("enable10m", out var e10) && e10.ValueKind != JsonValueKind.Null)
                _state.Enabled10m = e10.GetBoolean();

            if (doc.RootElement.TryGetProperty("enable2h", out var e2h) && e2h.ValueKind != JsonValueKind.Null)
                _state.Enabled2h = e2h.GetBoolean();

            await _store.SaveAsync(_state.ToPersisted());
            _state.NotifySettingsChanged();
            return Results.Ok(new { enabled10m = _state.Enabled10m, enabled2h = _state.Enabled2h });
        });

        // GET /channels/info - Informacje o skonfigurowanych kanałach respawn


        webapp.MapPost("/channels/info", async (HttpContext ctx, ILogger<InternalEndpoints> logger) =>
        {
            if (_client.Cache?.User is null)
            {
                return Results.Json(new { error = "Bot not connected" }, statusCode: 503);
            }

            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();
            var doc = JsonDocument.Parse(json);

            var channelIds = doc.RootElement.GetProperty("channel_ids").EnumerateArray()
                .Select(x => ulong.Parse(x.GetString()!))
                .ToList();

            logger.LogInformation("📥 /channels/info requested for: {Channels}", string.Join(", ", channelIds));


            var infos = channelIds.Select(chId =>
            {
                IGuildChannel? channel = null;

                foreach (var guild in _client.Cache.Guilds.Values)
                {
                    if (guild.Channels.TryGetValue(chId, out var ch) && ch is VoiceGuildChannel)
                    {
                        channel = ch;
                        break;
                    }
                }

                var guildName = channel != null && _client.Cache.Guilds.TryGetValue(channel.GuildId, out var g)
                    ? g.Name
                    : "Unknown Guild";

                var result = new
                {
                    id = chId.ToString(),
                    name = channel?.Name ?? "Unknown Channel",
                    guild = guildName
                };

                logger.LogInformation("  ➡️ Channel {Id}: {Name} ({Guild})", result.id, result.name, result.guild);

                return result;
            }).ToList();

            return Results.Ok(infos);
        });

        webapp.MapGet("/channels/voice", () =>
        {
            if (_client.Cache?.User is null)
            {
                return Results.Json(new { error = "Bot not connected" }, statusCode: 503);
            }

            var channels = new List<object>();

            foreach (var guild in _client.Cache.Guilds.Values)
            {
                var voiceChannels = guild.Channels.Values
                    .OfType<VoiceGuildChannel>()
                    .OrderBy(c => c.Position ?? 0)
                    .Select(c =>
                    {
                        // Policz użytkowników w kanale
                        var usersInChannel = guild.VoiceStates.Values.Count(vs => vs.ChannelId == c.Id);

                        // Znajdź kategorię
                        string? categoryName = null;
                        if (c.ParentId.HasValue && guild.Channels.TryGetValue(c.ParentId.Value, out var parent))
                        {
                            // Kategoria to kanał który nie jest ani Voice ani Text
                            if (parent is not VoiceGuildChannel && parent is not TextGuildChannel)
                            {
                                categoryName = parent.Name;
                            }
                        }

                        return new
                        {
                            id = c.Id.ToString(),
                            name = c.Name,
                            guildId = guild.Id.ToString(),
                            guildName = guild.Name,
                            categoryId = c.ParentId?.ToString(),
                            categoryName = categoryName,
                            userCount = usersInChannel,
                            position = c.Position ?? 0
                        };
                    });

                channels.AddRange(voiceChannels);
            }

            return Results.Ok(new { channels });
        });

        webapp.MapPost("/sounds/upload", async (HttpContext ctx) =>
        {
            if (!ctx.Request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Invalid content type" });
            }

            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            var soundType = form["sound_type"].ToString();

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "No file uploaded" });
            }

            if (!file.FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Only .wav files are supported" });
            }

            if (file.Length > 5 * 1024 * 1024) // 5MB
            {
                return Results.BadRequest(new { error = "File too large (max 5MB)" });
            }

            try
            {
                var targetPath = soundType == "10m"
                    ? _respawnOpts.Sound10m ?? "assets/respawn_10m.wav"
                    : _respawnOpts.Sound2h ?? "assets/respawn_2h.wav";

                // Normalize path
                if (!Path.IsPathRooted(targetPath))
                {
                    targetPath = Path.Combine(AppContext.BaseDirectory, targetPath);
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save file (replace existing)
                using (var stream = new FileStream(targetPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Results.Ok(new { success = true, path = targetPath });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 500);
            }
        });

        webapp.MapGet("/settings/limits", () => Results.Ok(new
        {
            max_channels = _respawnOpts.MaxChannels
        }));

        webapp.MapPost("/respawn/test-sound", async (HttpContext ctx, ILogger<InternalEndpoints> logger) =>
        {
            var sound = ctx.Request.Query["sound"].ToString();
            var useSettings = ctx.Request.Query["use_settings"].ToString() == "true";

            logger.LogDebug("[TEST-SOUND] 📥 Request: sound={Sound}, useSettings={UseSettings}", sound, useSettings);

            var channelId = _state.Channels.FirstOrDefault();
            if (channelId == 0)
            {
                logger.LogWarning("[TEST-SOUND] ❌ No channels configured");
                return Results.BadRequest(new { error = "No channels configured" });
            }

            logger.LogDebug("[TEST-SOUND] Channel: {ChannelId}", channelId);

            var soundPath = sound == "2h" ? _respawnOpts.Sound2h : _respawnOpts.Sound10m;
            if (!Path.IsPathRooted(soundPath))
            {
                soundPath = Path.Combine(AppContext.BaseDirectory, soundPath);
            }

            logger.LogDebug("[TEST-SOUND] Sound path: {Path}", soundPath);

            if (!File.Exists(soundPath))
            {
                logger.LogWarning("[TEST-SOUND] ❌ File not found: {Path}", soundPath);
                return Results.NotFound(new { error = "Sound file not found" });
            }

            try
            {
                // ✅ Użyj odpowiednich settings dla danego dźwięku
                var repeatPlays = useSettings
                    ? (sound == "2h" ? _state.RepeatPlays2h : _state.RepeatPlays10m)
                    : 1;
                var repeatGapMs = useSettings
                    ? (sound == "2h" ? _state.RepeatGapMs2h : _state.RepeatGapMs10m)
                    : 0;

                logger.LogDebug("[TEST-SOUND] Params: sound={Sound}, repeat={Repeat}, gap={Gap}ms", sound, repeatPlays,
                    repeatGapMs);

                // Fire-and-forget with logging
                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger.LogInformation("[TEST-SOUND] 🎵 Starting voice playback...");
                        await _voice.JoinAndPlayAsync(channelId, soundPath, repeatPlays, repeatGapMs,
                            CancellationToken.None);
                        logger.LogInformation("[TEST-SOUND] ✅ Voice playback complete");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[TEST-SOUND] ❌ Voice playback failed: {Message}", ex.Message);
                    }
                });

                logger.LogDebug("[TEST-SOUND] 📤 Returning OK (voice task started in background)");
                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[TEST-SOUND] ❌ Endpoint failed: {Message}", ex.Message);
                return Results.Problem(ex.Message, statusCode: 500);
            }
        });

        webapp.MapGet("/version", () =>
        {
            var assembly = typeof(InternalEndpoints).Assembly;
            return Results.Ok(new
            {
                version = _versionHelper.GetShortVersion(assembly),
                build_time = _versionHelper.GetBuildTime(assembly)
            });
        });

        _ = webapp.RunAsync();
    }
}