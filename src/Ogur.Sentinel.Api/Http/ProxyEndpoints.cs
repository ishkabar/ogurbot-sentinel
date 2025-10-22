using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Ogur.Sentinel.Api.Http;

public static class ProxyEndpoints
{
    public static void MapProxyEndpoints(this WebApplication app)
    {
        // === Settings ===
        
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
        
        app.MapPatch("/settings", async (IHttpClientFactory cf, HttpContext ctx) =>
        {
            var http = cf.CreateClient("worker");
    
            using var reader = new StreamReader(ctx.Request.Body);
            var jsonContent = await reader.ReadToEndAsync();
    
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch, "/settings") { Content = content };
            var res = await http.SendAsync(request);
            res.EnsureSuccessStatusCode();
    
            return Results.Ok();
        });

        app.MapGet("/settings/limits", async (IHttpClientFactory cf) =>
        {
            var http = cf.CreateClient("worker");
            var res = await http.GetFromJsonAsync<JsonElement>("/settings/limits");
            return Results.Ok(res);
        });

        // === Respawn ===
        
        app.MapGet("/respawn/next", async (IHttpClientFactory cf) =>
        {
            var http = cf.CreateClient("worker");
            var res = await http.GetFromJsonAsync<JsonElement>("/respawn/next");
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

        // === Channels ===
        
        app.MapGet("/channels/info", async (IHttpClientFactory cf) =>
        {
            var http = cf.CreateClient("worker");
            var res = await http.GetFromJsonAsync<JsonElement>("/channels/info");
            return Results.Ok(res);
        });

        app.MapGet("/channels/voice", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var res = await http.GetFromJsonAsync<JsonElement>("/channels/voice");
                return Results.Ok(res);
            }
            catch
            {
                return Results.Json(new { error = "Worker unavailable", channels = new object[] { } }, statusCode: 503);
            }
        });
        
        app.MapPost("/sounds/upload", async (IHttpClientFactory cf, HttpContext ctx) =>
        {
            var http = cf.CreateClient("worker");
    
            using var content = new MultipartFormDataContent();
            var form = await ctx.Request.ReadFormAsync();
    
            var file = form.Files.GetFile("file");
            if (file != null)
            {
                var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "file", file.FileName);
            }
    
            content.Add(new StringContent(form["sound_type"].ToString()), "sound_type");
    
            var response = await http.PostAsync("/sounds/upload", content);
    
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(result);
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Json(error, statusCode: (int)response.StatusCode);
            }
        });

        app.MapPost("/respawn/test-sound", async (IHttpClientFactory cf, HttpContext ctx) =>
        {
            var http = cf.CreateClient("worker");
            var sound = ctx.Request.Query["sound"];
            var useSettings = ctx.Request.Query["use_settings"];
    
            var response = await http.PostAsync($"/respawn/test-sound?sound={sound}&use_settings={useSettings}", null);
    
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(result);
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Json(error, statusCode: (int)response.StatusCode);
            }
        });
        
        // === Version ===
        
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
    }
}