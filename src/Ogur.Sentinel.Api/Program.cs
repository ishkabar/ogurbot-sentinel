using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

// ✅ Dodaj Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // 8h session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Sentinel.Session";
});

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

// ✅ Dodaj Session middleware
app.UseSession();

app.UseAuthorization();

// ✅ Session-based Auth middleware (zamiast Basic Auth)
app.Use(async (context, next) =>
{
    // Sprawdź czy to /respawn (ale nie /respawn/*)
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
    
    // Przekaż surowy JSON bez przeserializowania
    using var reader = new StreamReader(ctx.Request.Body);
    var jsonContent = await reader.ReadToEndAsync();
    
    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
    var res = await http.PostAsync("/settings", content);
    res.EnsureSuccessStatusCode();
    
    // Wymuś rekalkulację
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

app.MapPost("/logout-handler", (HttpContext ctx) =>
{
    ctx.Session.Clear();
    return Results.Ok();
});

app.Run();