using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.VoiceNext;
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

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOptions<SettingsOptions>()
    .Bind(builder.Configuration.GetSection("Settings"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.DiscordToken), "DiscordToken is required")
    .Validate(o => o.BreakChannelId != 0, "BreakChannelId is required")
    .Validate(o => o.BreakRoleId != 0, "BreakRoleId is required")
    .Validate(o => o.LeaveChannelId != 0, "LeaveChannelId is required")
    .ValidateOnStart();

builder.Services.AddOptions<RespawnOptions>()
    .Bind(builder.Configuration.GetSection("Respawn"))
    .PostConfigure(o =>
    {
        o.Sound10m = NormalizePath(o.Sound10m ?? "assets/respawn_10m.wav");
        o.Sound2h = NormalizePath(o.Sound2h ?? "assets/respawn_2h.wav");
        o.SettingsFile ??= "appsettings/respawn.settings.json";
    })
    .Validate(o => !string.IsNullOrWhiteSpace(o.SettingsFile), "Respawn.SettingsFile is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Sound10m), "Respawn.Sound10m is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Sound2h), "Respawn.Sound2h is required")
    .ValidateOnStart();

static string NormalizePath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return Path.Combine(AppContext.BaseDirectory, "assets");
    var clean = path.TrimStart('/', '\\');
    if (Path.IsPathRooted(path) && !path.StartsWith("/assets") && !path.StartsWith("\\assets"))
        return path;
    return Path.Combine(AppContext.BaseDirectory, clean);
}

// DSharpPlus 5.0 - nowy builder pattern
var discordBuilder = DiscordClientBuilder.CreateDefault(
    builder.Configuration.GetSection("Settings:DiscordToken").Value!,
    DiscordIntents.Guilds | DiscordIntents.GuildVoiceStates
);

discordBuilder.ConfigureEventHandlers(b => b
    .HandleSessionReady((client, args) =>
    {
        var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
        logger.LogInformation("[Discord] Ready as {User}", args.User.Username);
        return Task.CompletedTask;
    })
);

// VoiceNext
discordBuilder.UseVoiceNext(new VoiceNextConfiguration
{
    EnableIncoming = false
});

// Commands (nowy system zamiast SlashCommands)
discordBuilder.UseCommands((_, extension) =>
{
    var sp = builder.Services.BuildServiceProvider();
    
    // Zarejestruj command module
    extension.AddCommands(typeof(RespawnModule));
    
    // Event handlers
    extension.CommandExecuted += (_, e) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("[Commands] {Command} executed by {User}", 
            e.Context.Command?.Name ?? "unknown", e.Context.User.Username);
        return Task.CompletedTask;
    };

    extension.CommandErrored += (_, e) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogError(e.Exception, "[Commands] Error in {Command}", 
            e.Context.Command?.Name ?? "unknown");
        return Task.CompletedTask;
    };
});

builder.Services.AddSingleton(sp => discordBuilder.Build());

// Core state / services
builder.Services.AddSingleton<RespawnState>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<LeaveService>();
builder.Services.AddSingleton<RespawnSchedulerService>();
builder.Services.AddSingleton<VoiceService>();

// Hosted services
builder.Services.AddHostedService<DiscordBotHostedService>();
builder.Services.AddHostedService<RespawnWorker>();

builder.Services.AddSingleton<InternalEndpoints>();

var app = builder.Build();

var store = app.Services.GetRequiredService<SettingsStore>();
var state = app.Services.GetRequiredService<RespawnState>();
var persisted = await store.LoadAsync();
state.ApplyPersisted(persisted);

app.Services.GetRequiredService<InternalEndpoints>().Map(app);

await app.RunAsync();