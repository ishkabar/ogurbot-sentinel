using System;
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

    public InternalEndpoints(RespawnState state, RespawnSchedulerService scheduler, SettingsStore store, IOptions<SettingsOptions> opts)
    {
        _state = state;
        _scheduler = scheduler;
        _store = store;
        _opts = opts.Value;
    }

    public void Map(IHost app)
    {
        var web = app.Services.GetRequiredService<IConfiguration>()["Urls"] ?? "http://0.0.0.0:9090";

        var builder = WebApplication.CreateBuilder();      // bez WebApplicationOptions
        builder.WebHost.UseUrls(web);                      // <- tu ustaw URL
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

        webapp.MapGet("/respawn/next", () =>
        {
            var nowLocal = DateTimeOffset.Now; // host TZ (ustaw TZ w kontenerze)
            var (n10, n2h) = _scheduler.ComputeNext(nowLocal);
            return Results.Ok(new { next10m = n10, next2h = n2h });
        });

        webapp.MapPost("/respawn/toggle", (bool? enable10m, bool? enable2h) =>
        {
            if (enable10m is not null) _state.Enabled10m = enable10m.Value;
            if (enable2h is not null) _state.Enabled2h = enable2h.Value;
            return Results.Ok(new { _state.Enabled10m, _state.Enabled2h });
        });

        //webapp.RunAsync(); // fire & forget
        _ = webapp.RunAsync();
    }
}