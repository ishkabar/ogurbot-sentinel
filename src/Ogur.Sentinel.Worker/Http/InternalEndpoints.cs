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
	private readonly IVersionHelper _versionHelper;


    public InternalEndpoints(
        RespawnState state, 
        RespawnSchedulerService scheduler, 
        SettingsStore store, 
        IOptions<SettingsOptions> opts,
        DiscordSocketClient discord,
        IOptions<RespawnOptions> respawnOpts,
        WikiSyncService wikiSync,
    IVersionHelper versionHelper)
    {
        _state = state;
        _scheduler = scheduler;
        _store = store;
        _opts = opts.Value;
        _discord = discord;
        _respawnOpts = respawnOpts.Value;
        _wikiSync = wikiSync;
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
            last_sync_at = _state.LastSyncAt
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
                LastSyncAt = _state.LastSyncAt
            };
    
            _state.ApplyPersisted(settings);
            await _store.SaveAsync(_state.ToPersisted());
            return Results.Ok();
        });
        
        webapp.MapPost("/respawn/recalculate", () =>
        {
            _scheduler.RecalculateNext();
            var nowLocal = DateTimeOffset.Now;
            var (n10, n2h) = _scheduler.ComputeNext(nowLocal);
            return Results.Ok(new { 
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