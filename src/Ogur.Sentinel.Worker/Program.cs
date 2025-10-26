using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Extensions.Logging;
using Ogur.Sentinel.Abstractions;
using Ogur.Sentinel.Abstractions.Options;
using Ogur.Sentinel.Core;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Worker;
using Ogur.Sentinel.Worker.Discord;
using Ogur.Sentinel.Worker.Http;
using Ogur.Sentinel.Worker.Services;

// ✅ Create NLog logger early for startup logging
var nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings", "nlog.config");
var logger = LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath).GetCurrentClassLogger();

try
{
    logger.Info("🚀 Starting Ogur.Sentinel.Worker...");

    var builder = Host.CreateApplicationBuilder(args);

    // --- Configuration ---
    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings", "appsettings.json");
    builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: true);

    // --- Logging (NLog) ---
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.Logging.AddNLog();

    // --- Options Configuration ---
    ConfigureOptions(builder.Services, builder.Configuration);

    // --- Core Domain Services ---
    ConfigureCoreServices(builder.Services);

    // --- Discord Services (Netcord) ---
    builder.Services.AddDiscordServices(builder.Configuration);

    // --- Application Workers ---
    builder.Services.AddHostedService<RespawnWorker>();

    // --- Internal HTTP Endpoints ---
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<InternalEndpoints>();

    var app = builder.Build();

    // --- Initialize Persisted State ---
    await InitializeRespawnState(app.Services);

    // --- Map Internal Endpoints ---
    app.Services.GetRequiredService<InternalEndpoints>().Map(app);

    logger.Info("✅ Worker application configured successfully");

    // --- Run ---
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "❌ Worker application stopped due to exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}

// ========== Helper Methods ==========

static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
{
    // SettingsOptions
    services.AddOptions<SettingsOptions>()
        .Bind(configuration.GetSection("Settings"))
        .Validate(o => !string.IsNullOrWhiteSpace(o.DiscordToken), "DiscordToken is required")
        .Validate(o => o.BreakChannelId != 0, "BreakChannelId is required")
        .Validate(o => o.BreakRoleId != 0, "BreakRoleId is required")
        .Validate(o => o.LeaveChannelId != 0, "LeaveChannelId is required")
        .ValidateOnStart();

    // RespawnOptions
    services.AddOptions<RespawnOptions>()
        .Bind(configuration.GetSection("Respawn"))
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
}

static void ConfigureCoreServices(IServiceCollection services)
{
    // Core state
    services.AddSingleton<RespawnState>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<RespawnOptions>>();
        return new RespawnState
        {
            MaxChannels = opts.Value.MaxChannels
        };
    });

    // Domain services
    services.AddSingleton<SettingsStore>();
    services.AddSingleton<LeaveService>();
    services.AddSingleton<RespawnSchedulerService>();
    services.AddSingleton<WikiSyncService>();
    services.AddSingleton<IVersionHelper, VersionHelper>();
}

static async Task InitializeRespawnState(IServiceProvider services)
{
    var store = services.GetRequiredService<SettingsStore>();
    var state = services.GetRequiredService<RespawnState>();
    var persisted = await store.LoadAsync();
    state.ApplyPersisted(persisted);
}

static string NormalizePath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return Path.Combine(AppContext.BaseDirectory, "assets");

    var clean = path.TrimStart('/', '\\');

    if (Path.IsPathRooted(path) && !path.StartsWith("/assets") && !path.StartsWith("\\assets"))
        return path;

    return Path.Combine(AppContext.BaseDirectory, clean);
}