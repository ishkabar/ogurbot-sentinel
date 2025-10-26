using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.AspNetCore.DataProtection;
using Ogur.Sentinel.Abstractions;
using Ogur.Sentinel.Core;
using Ogur.Sentinel.Api.Http;
using NLog.Extensions.Logging;
using NLog;
using NLog.Web;
using Ogur.Sentinel.Abstractions.Options;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Abstractions.Auth;
using Ogur.Sentinel.Core.Auth;
using Microsoft.AspNetCore.Http;
using Ogur.Sentinel.Api.Middleware;
using Microsoft.Extensions.FileProviders;


// ✅ Load NLog config from appsettings directory
var nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings", "nlog.config");
var logger = LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath).GetCurrentClassLogger();

try
{
    logger.Info("🚀 Starting Ogur.Sentinel.Api...");

    var builder = WebApplication.CreateBuilder(args);

    var keysPath = builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "keys")
        : "/app/keys";
    Directory.CreateDirectory(keysPath);

    var appsettingsPath = builder.Environment.IsDevelopment()
        ? "appsettings.json"
        : "/app/appsettings/appsettings.json";

    var usersFilePath = builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "appsettings", "users.json")
        : "/app/appsettings/users.json";

    logger.Info("👥 Users file path: {Path}", usersFilePath);
    logger.Info("👥 File exists before registration: {Exists}", File.Exists(usersFilePath));


    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile(appsettingsPath, optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    logger.Info("🔐 Auth:AdminUser = {AdminUser}", builder.Configuration["Auth:AdminUser"]);
    logger.Info("🔐 Auth:AdminPassword length = {Length}", builder.Configuration["Auth:AdminPassword"]?.Length ?? 0);

    // --- NLog Configuration ---
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.Logging.AddNLog();

    builder.Services.AddRazorPages();
    builder.Services.AddHealthChecks();

    builder.Services.AddSingleton<UserStore>(sp =>
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var userStoreLogger = loggerFactory.CreateLogger<UserStore>();
        return new UserStore(usersFilePath, userStoreLogger);
    });
    builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("Ogur.Sentinel.Api");

    var redisConn = builder.Configuration["Redis:ConnectionString"];
    if (!string.IsNullOrEmpty(redisConn))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConn;
            options.InstanceName = "OgurSentinel:";
        });
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
    }

    builder.Services.AddSingleton<IVersionHelper, VersionHelper>();

    builder.Services.AddHttpClient("worker", (sp, http) =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        http.BaseAddress = new Uri(cfg["Worker:BaseUrl"] ?? "http://localhost:9090");
    });


    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection(); // ✅ Tu na początku
    app.UseStaticFiles(); // wwwroot (css, js, lib)

// ✅ /files dla downloadów
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name.EndsWith(".exe") || ctx.File.Name.EndsWith(".zip") || ctx.File.Name.EndsWith(".rar"))
            {
                ctx.Context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{ctx.File.Name}\"");
            }
        }
    });

    app.UseRouting();

// ✅ Auth middleware OSTATNIE przed MapRazorPages
    app.UseAuthMiddleware();

    app.MapRazorPages();
    app.MapHealthChecks("/health");

    // === Local API Endpoints ===

    app.MapGet("/version", (IVersionHelper versionHelper) =>
    {
        var assembly = typeof(Program).Assembly;
        return Results.Ok(new
        {
            version = versionHelper.GetShortVersion(assembly),
            build_time = versionHelper.GetBuildTime(assembly)
        });
    });

    // === API Authentication Endpoints ===

    app.MapPost("/api/auth/login", async (
        HttpContext context,
        UserStore userStore, // ← Inject UserStore
        ITokenStore tokenStore) =>
    {
        var request = await context.Request.ReadFromJsonAsync<LoginRequest>();

        if (request == null)
        {
            logger.Warn("❌ Request body is null");
            return Results.BadRequest(new { error = "Invalid request body" });
        }

        // ✅ Sprawdź użytkownika w JSON
        logger.Info("🔍 Login attempt: username='{Username}', password length={Length}",
            request.Username, request.Password?.Length ?? 0);
        var user = userStore.ValidateUser(request.Username, request.Password);

        if (user != null)
        {
            logger.Info("✅ User validated: {Username}, Role: {Role}", user.Username, user.Role);

            var token = Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)
            );

            var tokenData = new TokenData
            {
                Username = user.Username,
                Role = user.Role,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            await tokenStore.AddAsync(token, tokenData);

            logger.Info("✅ Login SUCCESS for user: {User} (role: {Role})", user.Username, user.Role);

            return Results.Ok(new
            {
                token,
                role = user.Role,
                expiresIn = 86400,
                expiresAt = tokenData.ExpiresAt
            });
        }

        logger.Warn("❌ Failed login attempt for user: {User}", request?.Username ?? "null");
        return Results.Unauthorized();
    });

    app.MapGet("/api/auth/validate", async (HttpContext context, ITokenStore tokenStore) =>
    {
        var token = context.Request.Headers["Authorization"]
            .ToString()
            .Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        var (success, tokenData) = await tokenStore.TryGetAsync(token);

        if (success && tokenData != null && tokenData.ExpiresAt > DateTime.UtcNow)
        {
            return Results.Ok(new
            {
                valid = true,
                username = tokenData.Username,
                role = tokenData.Role,
                expiresAt = tokenData.ExpiresAt
            });
        }

        return Results.Unauthorized();
    });

    app.MapPost("/api/auth/logout", async (HttpContext context, ITokenStore tokenStore) =>
    {
        var token = context.Request.Headers["Authorization"]
            .ToString()
            .Replace("Bearer ", "");

        if (!string.IsNullOrEmpty(token))
        {
            await tokenStore.RemoveAsync(token);
            logger.Info("🔓 API token invalidated");
        }

        return Results.Ok(new { message = "Logged out" });
    });

    app.MapPost("/api/auth/reload-users", (UserStore userStore) =>
    {
        userStore.Reload();
        return Results.Ok(new { message = "Users reloaded" });
    });

    // === Proxy Endpoints to Worker ===
    app.MapProxyEndpoints();


    logger.Info("✅ API application configured successfully");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "❌ API application stopped due to exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}