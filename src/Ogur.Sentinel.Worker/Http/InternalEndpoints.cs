using System;
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

namespace Ogur.Sentinel.Worker.Http;

public sealed class InternalEndpoints
{
    private readonly RespawnState _state;
    private readonly RespawnSchedulerService _scheduler;
    private readonly SettingsStore _store;
    private readonly SettingsOptions _opts;
    private readonly DiscordSocketClient _discord;

    public InternalEndpoints(
        RespawnState state, 
        RespawnSchedulerService scheduler, 
        SettingsStore store, 
        IOptions<SettingsOptions> opts,
        DiscordSocketClient discord)
    {
        _state = state;
        _scheduler = scheduler;
        _store = store;
        _opts = opts.Value;
        _discord = discord;
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
            channels = _state.Channels,
            enabled_10m = _state.Enabled10m,
            enabled_2h = _state.Enabled2h
        }));

        webapp.MapPost("/settings", async (PersistedSettings s) =>
        {
            _state.ApplyPersisted(s);
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

        webapp.MapPost("/respawn/toggle", (bool? enable10m, bool? enable2h) =>
        {
            if (enable10m is not null) _state.Enabled10m = enable10m.Value;
            if (enable2h is not null) _state.Enabled2h = enable2h.Value;
            return Results.Ok(new { _state.Enabled10m, _state.Enabled2h });
        });
        
        webapp.MapGet("/channels/info", () =>
        {
            // Sprawdź czy bot jest ready
            //if (_discord.ConnectionState != ConnectionState.Connected)
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
                    id = chId,
                    name = channel?.Name ?? "Unknown Channel",
                    guild = channel?.Guild?.Name ?? "Unknown Guild"
                };
            }).ToList();

            return Results.Ok(infos);
        });

        _ = webapp.RunAsync();
    }
}