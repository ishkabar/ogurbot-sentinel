using System.Net.Http.Json;
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
app.UseAuthorization();

app.MapRazorPages();
app.MapHealthChecks("/health");

// === Proxy endpoints to Worker ===

app.MapGet("/settings", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.GetFromJsonAsync<object>("/settings");
    return Results.Ok(res);
});

app.MapPost("/settings", async (IHttpClientFactory cf, HttpContext ctx) =>
{
    var http = cf.CreateClient("worker");
    var payload = await ctx.Request.ReadFromJsonAsync<object>();
    var res = await http.PostAsJsonAsync("/settings", payload);
    res.EnsureSuccessStatusCode();
    return Results.Ok();
});

app.MapGet("/respawn/next", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.GetFromJsonAsync<object>("/respawn/next");
    return Results.Ok(res);
});

app.MapPost("/respawn/toggle", async (IHttpClientFactory cf, HttpContext ctx) =>
{
    var http = cf.CreateClient("worker");
    var payload = await ctx.Request.ReadFromJsonAsync<object>();
    var res = await http.PostAsJsonAsync("/respawn/toggle", payload);
    res.EnsureSuccessStatusCode();
    var result = await res.Content.ReadFromJsonAsync<object>();
    return Results.Ok(result);
});

app.MapGet("/channels/info", async (IHttpClientFactory cf) =>
{
    var http = cf.CreateClient("worker");
    var res = await http.GetFromJsonAsync<object>("/channels/info");
    return Results.Ok(res);
});

app.Run();