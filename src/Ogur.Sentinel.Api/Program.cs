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

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Name = ".Sentinel.Session";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    });

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

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    // ✅ Session middleware
    app.UseSession();

    app.UseAuthorization();

    // Auth middleware - redirect to login if not authenticated
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/respawn", StringComparison.OrdinalIgnoreCase))
        {
            var isAuthenticated = context.Session.GetString("IsAuthenticated");
            
            if (isAuthenticated != "true")
            {
                context.Response.Redirect("/Login");
                return;
            }
        }
        
        await next();
    });

    // Role-based access control - Viewer = read-only
    app.Use(async (context, next) =>
    {
        var role = context.Session.GetString("Role");
        var method = context.Request.Method;
        var path = context.Request.Path.Value?.ToLower() ?? "";
        
        // Viewer może tylko GET
        if (role == "Viewer" && method != "GET" && path.StartsWith("/respawn"))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Read-only access" });
            return;
        }
        
        await next();
    });

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
