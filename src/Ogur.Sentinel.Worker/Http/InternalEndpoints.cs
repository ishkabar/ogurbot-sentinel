using System;
using System.Reflection;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
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

namespace Ogur.Sentinel.Worker.Http;

public sealed class InternalEndpoints
{
    private readonly RespawnState _state;
    private readonly RespawnSchedulerService _scheduler;
    private readonly SettingsStore _store;
    private readonly SettingsOptions _opts;
    private readonly DiscordSocketClient _discord;
    private readonly RespawnOptions _respawnOpts;
    private readonly WikiSyncService _wikiSync;
    private readonly VoiceService2 _voice;
    private readonly IVersionHelper _versionHelper;


    public InternalEndpoints(
        RespawnState state,
        RespawnSchedulerService scheduler,
        SettingsStore store,
        IOptions<SettingsOptions> opts,
        DiscordSocketClient discord,
        IOptions<RespawnOptions> respawnOpts,
        WikiSyncService wikiSync,
        VoiceService2 voice,
        IVersionHelper versionHelper)
    {
        _state = state;
        _scheduler = scheduler;
        _store = store;
        _opts = opts.Value;
        _discord = discord;
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

        webapp.MapGet("/settings", () => Results.Ok(new
        {
            base_hhmm = _state.BaseHhmm,
            lead_seconds = _state.LeadSeconds,
            channels = _state.Channels.Select(c => c.ToString()).ToArray(),
            enabled_10m = _state.Enabled10m,
            enabled_2h = _state.Enabled2h,
            use_synced_time = _state.UseSyncedTime,
            synced_base_time = _state.SyncedBaseTime,
            last_sync_at = _state.LastSyncAt, 
            repeat_gap_ms = _state.RepeatGapMs 
        }));

        webapp.MapPost("/settings", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();
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
                RepeatPlays = doc.RootElement.TryGetProperty("repeat_plays", out var rp) 
                    ? rp.GetInt32() 
                    : _state.RepeatPlays,
                RepeatGapMs = doc.RootElement.TryGetProperty("repeat_gap_ms", out var rg) 
                    ? rg.GetInt32() 
                    : _state.RepeatGapMs
            };

            _state.ApplyPersisted(settings);
            await _store.SaveAsync(_state.ToPersisted());
            _state.NotifySettingsChanged();
            return Results.Ok();
        });
        
        webapp.MapPatch("/settings", async (HttpContext ctx) =>
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
    
            if (doc.RootElement.TryGetProperty("repeat_plays", out var repeatPlays))
                _state.RepeatPlays = repeatPlays.GetInt32();
    
            if (doc.RootElement.TryGetProperty("repeat_gap_ms", out var repeatGap))
                _state.RepeatGapMs = repeatGap.GetInt32();

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

            return Results.Ok(new { _state.Enabled10m, _state.Enabled2h });
        });

        webapp.MapGet("/settings/limits", () => Results.Ok(new
        {
            max_channels = _respawnOpts.MaxChannels
        }));

        webapp.MapGet("/channels/info", () =>
        {
            if (_discord.LoginState != LoginState.LoggedIn || _discord.CurrentUser is null)
            {
                return Results.Ok(new[]
                {
                    new { id = 0UL, name = "Bot not connected", guild = "" }
                });
            }

            var infos = _state.Channels.Select(chId =>
            {
                var channel = _discord.GetChannel(chId) as SocketVoiceChannel;

                if (channel == null)
                {
                    foreach (var guild in _discord.Guilds)
                    {
                        channel = guild.GetVoiceChannel(chId);
                        if (channel != null) break;
                    }
                }

                return new
                {
                    id = chId.ToString(),
                    name = channel?.Name ?? "Unknown Channel",
                    guild = channel?.Guild?.Name ?? "Unknown Guild"
                };
            }).ToList();

            return Results.Ok(infos);
        });

        webapp.MapGet("/channels/voice", () =>
        {
            if (_discord.LoginState != LoginState.LoggedIn || _discord.CurrentUser is null)
            {
                return Results.Json(new { error = "Bot not connected" }, statusCode: 503);
            }

            var channels = new List<object>();

            foreach (var guild in _discord.Guilds)
            {
                var voiceChannels = guild.VoiceChannels
                    .OrderBy(c => c.Position)
                    .Select(c => {
                     
                        var usersInChannel = guild.Users.Count(u => u.VoiceChannel?.Id == c.Id);
                
                        return new
                        {
                            id = c.Id.ToString(),
                            name = c.Name,
                            guildId = guild.Id.ToString(),
                            guildName = guild.Name,
                            categoryId = c.CategoryId?.ToString(),
                            categoryName = guild.CategoryChannels
                                .FirstOrDefault(cat => cat.Id == c.CategoryId)?.Name,
                            userCount = usersInChannel, 
                            position = c.Position
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

        webapp.MapPost("/respawn/test-sound", async (HttpContext ctx) =>
        {
            var sound = ctx.Request.Query["sound"].ToString();
            var useSettings = ctx.Request.Query["use_settings"].ToString() == "true";

            var channelId = _state.Channels.FirstOrDefault();
            if (channelId == 0)
            {
                return Results.BadRequest(new { error = "No channels configured" });
            }

            var soundPath = sound == "2h" ? _respawnOpts.Sound2h : _respawnOpts.Sound10m;
            if (!Path.IsPathRooted(soundPath))
            {
                soundPath = Path.Combine(AppContext.BaseDirectory, soundPath);
            }

            if (!File.Exists(soundPath))
            {
                return Results.NotFound(new { error = "Sound file not found" });
            }

            try
            {
                var repeatPlays = useSettings && _state.RepeatPlays > 0 ? _state.RepeatPlays : 1;
                var repeatGapMs = useSettings ? _state.RepeatGapMs : 0;  // ← Jeśli nie useSettings to 0, inaczej z settings

                _ = Task.Run(async () => 
                {
                    try
                    {
                        await _voice.JoinAndPlayAsync(channelId, soundPath, repeatPlays, repeatGapMs, CancellationToken.None);
                    }
                    catch { }
                });

                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
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