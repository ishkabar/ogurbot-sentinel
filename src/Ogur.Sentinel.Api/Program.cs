using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.DataProtection;
using Ogur.Sentinel.Abstractions;
using Ogur.Sentinel.Core;


var builder = WebApplication.CreateBuilder(args);

var keysPath = builder.Environment.IsDevelopment()
    ? Path.Combine(builder.Environment.ContentRootPath, "keys")
    : "/app/keys"; 
Directory.CreateDirectory(keysPath);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("Ogur.Sentinel.Api");

//builder.Services.AddDistributedMemoryCache();
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

// === Proxy endpoints to Worker ===

app.MapGet("/settings", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.GetFromJsonAsync<JsonElement>("/settings");
    return Results.Ok(res);
});

app.MapPost("/settings", async (IHttpClientFactory cf, HttpContext ctx) =>
{
    var http = cf.CreateClient("worker");
    
    using var reader = new StreamReader(ctx.Request.Body);
    var jsonContent = await reader.ReadToEndAsync();
    
    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
    var res = await http.PostAsync("/settings", content);
    res.EnsureSuccessStatusCode();
    
    try
    {
        await http.PostAsync("/respawn/recalculate", null);
    }
    catch { /* ignore */ }
    
    return Results.Ok();
});

app.MapGet("/respawn/next", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.GetFromJsonAsync<JsonElement>("/respawn/next");
    return Results.Ok(res);
});

app.MapGet("/settings/limits", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.GetFromJsonAsync<JsonElement>("/settings/limits");
    return Results.Ok(res);
});

app.MapPost("/respawn/sync", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.PostAsync("/respawn/sync", null);
    res.EnsureSuccessStatusCode();
    var result = await res.Content.ReadFromJsonAsync<JsonElement>();
    return Results.Ok(result);
});

app.MapPost("/respawn/toggle", async (IHttpClientFactory cf, HttpContext ctx) =>
{
    var http = cf.CreateClient("worker");
    var payload = await ctx.Request.ReadFromJsonAsync<JsonElement>();
    var res = await http.PostAsJsonAsync("/respawn/toggle", payload);
    res.EnsureSuccessStatusCode();
    var result = await res.Content.ReadFromJsonAsync<JsonElement>();
    return Results.Ok(result);
});

app.MapPost("/respawn/recalculate", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.PostAsync("/respawn/recalculate", null);
    res.EnsureSuccessStatusCode();
    var result = await res.Content.ReadFromJsonAsync<JsonElement>();
    return Results.Ok(result);
});

app.MapGet("/channels/info", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.GetFromJsonAsync<JsonElement>("/channels/info");
    return Results.Ok(res);
});

app.MapGet("/version", (IVersionHelper versionHelper) =>
{
    var assembly = typeof(Program).Assembly;
    return Results.Ok(new 
    { 
        version = versionHelper.GetShortVersion(assembly),
        build_time = versionHelper.GetBuildTime(assembly)
    });
});

app.MapGet("/worker/version", async (IHttpClientFactory cf) =>
{
    try
    {
        var http = cf.CreateClient("worker");
        var res = await http.GetFromJsonAsync<JsonElement>("/version");
        return Results.Ok(res);
    }
    catch
    {
        return Results.Json(new { version = "disconnected", build_time = "-" });
    }
});

app.Run();