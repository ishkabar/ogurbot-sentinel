using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Abstractions.Options;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Worker;
using Ogur.Sentinel.Worker.Discord;
using Ogur.Sentinel.Worker.Discord.Modules;
using Ogur.Sentinel.Worker.Http;
using Ogur.Sentinel.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings", "appsettings.json");
builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: true);

// --- Logging ---
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// SettingsOptions – zostaw jak masz:
builder.Services.AddOptions<SettingsOptions>()
    .Bind(builder.Configuration.GetSection("Settings"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.DiscordToken), "DiscordToken is required")
    .Validate(o => o.BreakChannelId != 0, "BreakChannelId is required")
    .Validate(o => o.BreakRoleId != 0, "BreakRoleId is required")
    .Validate(o => o.LeaveChannelId != 0, "LeaveChannelId is required")
    .ValidateOnStart();

// RespawnOptions – bind + normalizacja ścieżek + walidacja:
builder.Services.AddOptions<RespawnOptions>()
    .Bind(builder.Configuration.GetSection("Respawn"))
    .PostConfigure(o =>
    {
        o.Sound10m = NormalizePath(o.Sound10m ?? "assets/respawn_10m.wav");
        o.Sound2h  = NormalizePath(o.Sound2h  ?? "assets/respawn_2h.wav");
        o.SettingsFile ??= "appsettings/respawn.settings.json";
    })
    .Validate(o => !string.IsNullOrWhiteSpace(o.SettingsFile), "Respawn.SettingsFile is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Sound10m), "Respawn.Sound10m is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Sound2h), "Respawn.Sound2h is required")
    .ValidateOnStart();

// --- helper ---
static string NormalizePath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return Path.Combine(AppContext.BaseDirectory, "assets");

    // usuń ewentualne wiodące separatory, żeby nie był traktowany jako absolutny
    var clean = path.TrimStart('/', '\\');

    // jeśli już był absolutny (np. w Dockerze), zostaw go
    if (Path.IsPathRooted(path) && !path.StartsWith("/assets") && !path.StartsWith("\\assets"))
        return path;

    return Path.Combine(AppContext.BaseDirectory, clean);
}


// --- Discord client ---
var discordCfg = new DiscordSocketConfig
{
    GatewayIntents =
        GatewayIntents.Guilds |
        GatewayIntents.GuildMessages |
        GatewayIntents.GuildVoiceStates,
    LogLevel = LogSeverity.Info,
    AlwaysDownloadUsers = false,
};
builder.Services.AddSingleton(new DiscordSocketClient(discordCfg));
builder.Services.AddSingleton<DiscordReadyService>();    // <— NOWE

// --- Core state ---
builder.Services.AddSingleton<RespawnState>();

// --- Infra/services ---
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<LeaveService>();
builder.Services.AddSingleton<RespawnSchedulerService>();

// --- Slash modules (for CommandRegistrationService) ---
builder.Services.AddSingleton<RespawnModule>();
builder.Services.AddSingleton<LeaveModule>();
builder.Services.AddSingleton<AdminBreakModule>();

// --- Hosted services ---
builder.Services.AddHostedService<DiscordBotHostedService>();
builder.Services.AddHostedService<CommandRegistrationService>();

// --- Minimal internal HTTP (in-proc Kestrel) ---
builder.Services.AddSingleton<InternalEndpoints>();

builder.Services.AddSingleton<VoiceService>();
builder.Services.AddSingleton<RespawnSchedulerService>();
builder.Services.AddHostedService<RespawnWorker>();

var app = builder.Build();

// Initialize persisted settings → state
var store = app.Services.GetRequiredService<SettingsStore>();
var state = app.Services.GetRequiredService<RespawnState>();
var opts  = app.Services.GetRequiredService<IOptions<SettingsOptions>>().Value;
var persisted = await store.LoadAsync();
state.ApplyPersisted(persisted);

// Map internal endpoints (intentionally no auth; expose only on private network in compose)
app.Services.GetRequiredService<InternalEndpoints>().Map(app);

// Run
await app.RunAsync();
