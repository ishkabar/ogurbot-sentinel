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
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
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
using Microsoft.AspNetCore.StaticFiles;
using Ogur.Sentinel.Api.Services;


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

    var costumesJsonPath = builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "files", "costumes", "costumes.json")
        : "/app/files/costumes/costumes.json";

    var costumesAdminTokenPath = builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "appsettings", "costumes-admin-token.txt")
        : "/app/appsettings/costumes-admin-token.txt";

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
    builder.Services.AddSingleton<OreMarkLogger>();
    builder.Services.AddSingleton<OreVisitLogger>();
    builder.Services.AddSingleton<UpgradeChanceService>();
    builder.Services.AddHttpClient("zrzutka");

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'self'; " +
            "connect-src 'self' https://api.github.com blob:; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "frame-src 'self' https://zrzutka.pl;"
        );
        await next();
    });

    var contentTypeProvider = new FileExtensionContentTypeProvider();
    contentTypeProvider.Mappings[".glb"] = "model/gltf-binary";
    contentTypeProvider.Mappings[".gltf"] = "model/gltf+json";

    app.UseStaticFiles();

// ✅ /files dla downloadów
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = contentTypeProvider,
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name.EndsWith(".exe") || ctx.File.Name.EndsWith(".zip") || ctx.File.Name.EndsWith(".rar"))
            {
                ctx.Context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{ctx.File.Name}\"");
            }

            if (ctx.File.Name.EndsWith(".js") || ctx.File.Name.EndsWith(".css"))
            {
                ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store");
            }
        }
    });

    app.UseRouting();

    var calibProtector = app.Services.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("CostumesCalibration")
        .ToTimeLimitedDataProtector();

    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/baerim/costumes"))
        {
            var expected = CalibrationAuth.ReadToken(costumesAdminTokenPath);

            if (ctx.Request.Query.TryGetValue("auth", out var candidate))
            {
                if (CalibrationAuth.TokenMatches(candidate.ToString(), expected))
                {
                    ctx.Response.Cookies.Append(
                        CalibrationAuth.CookieName,
                        calibProtector.Protect(CalibrationAuth.Fingerprint(expected), TimeSpan.FromHours(12)),
                        new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = !app.Environment.IsDevelopment(),
                            SameSite = SameSiteMode.Strict,
                            Path = "/baerim",
                            MaxAge = TimeSpan.FromHours(12)
                        });
                }

                ctx.Response.Redirect(ctx.Request.Path.Value ?? "/baerim/costumes/");
                return;
            }

            ctx.Items[CalibrationAuth.ItemKey] = CalibrationAuth.IsUnlocked(ctx, calibProtector, expected);
        }

        await next();
    });

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

    app.MapGet("/baerim/upgrade-data", (UpgradeChanceService service) => Results.Ok(service.GetData()));

    app.MapPost("/baerim/costumes-calibration", async (HttpContext context) =>
    {
        var expectedToken = CalibrationAuth.ReadToken(costumesAdminTokenPath);
        if (!CalibrationAuth.IsUnlocked(context, calibProtector, expectedToken))
        {
            return Results.Unauthorized();
        }

        var body = await context.Request.ReadFromJsonAsync<CalibrationRequest>();
        if (body is null || string.IsNullOrEmpty(body.ItemId) || string.IsNullOrEmpty(body.CharacterId) ||
            string.IsNullOrEmpty(body.Slot))
        {
            return Results.BadRequest(new { error = "Invalid request body" });
        }

        var json = await File.ReadAllTextAsync(costumesJsonPath);
        var doc = JsonNode.Parse(json)!.AsObject();
        var categories = doc["categories"]!.AsArray();

        JsonObject? targetItem = null;
        foreach (var category in categories)
        {
            if (category!["slot"]!.ToString() != body.Slot) continue;
            foreach (var item in category["items"]!.AsArray())
            {
                if (item!["id"]?.ToString() == body.ItemId)
                {
                    targetItem = item.AsObject();
                    break;
                }
            }

            if (targetItem != null) break;
        }

        if (targetItem is null)
        {
            return Results.NotFound(new { error = $"Item '{body.ItemId}' not found in slot '{body.Slot}'" });
        }

        var filesNode = targetItem["files"]?.AsObject();
        if (filesNode is null)
        {
            return Results.BadRequest(new { error = "Item has no 'files' map" });
        }

        var currentFileNode = filesNode[body.CharacterId];
        if (currentFileNode is null)
        {
            return Results.BadRequest(new { error = $"No file entry for character '{body.CharacterId}'" });
        }

        var existingFilePath = currentFileNode is JsonObject existingObj
            ? existingObj["file"]!.ToString()
            : currentFileNode.ToString();

        filesNode[body.CharacterId] = new JsonObject
        {
            ["file"] = existingFilePath,
            ["pos"] = new JsonArray(body.Pos.Select(p => (JsonNode)JsonValue.Create(p)).ToArray()),
            ["scale"] = body.Scale
        };

        await File.WriteAllTextAsync(costumesJsonPath,
            doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        logger.Info("💾 Costume calibration saved: {Slot}/{ItemId}/{Char} pos=[{X},{Y},{Z}] scale={Scale}",
            body.Slot, body.ItemId, body.CharacterId, body.Pos[0], body.Pos[1], body.Pos[2], body.Scale);

        return Results.Ok(new { message = "Saved" });
    });

    // === Proxy Endpoints to Worker ===
    app.MapProxyEndpoints();
    app.MapDiscordAuthEndpoints();


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

record CalibrationRequest(string ItemId, string CharacterId, string Slot, double[] Pos, double Scale);

static class CalibrationAuth
{
    public const string CookieName = "costumes_calib";
    public const string ItemKey = "CalibUnlocked";

    static byte[] Hash(string s) => SHA256.HashData(Encoding.UTF8.GetBytes(s));

    public static string ReadToken(string path) =>
        File.Exists(path) ? File.ReadAllText(path).Trim() : "";

    public static string Fingerprint(string token) => Convert.ToHexString(Hash(token));

    public static bool TokenMatches(string? candidate, string expected) =>
        !string.IsNullOrEmpty(candidate) && expected.Length > 0 &&
        CryptographicOperations.FixedTimeEquals(Hash(candidate), Hash(expected));

    public static bool IsUnlocked(HttpContext ctx, ITimeLimitedDataProtector protector, string expected)
    {
        if (expected.Length == 0) return false;
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value)) return false;
        try
        {
            var payload = protector.Unprotect(value);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(payload),
                Encoding.UTF8.GetBytes(Fingerprint(expected)));
        }
        catch
        {
            return false;
        }
    }
}