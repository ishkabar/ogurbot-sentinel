using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Ogur.Sentinel.Core.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace Ogur.Sentinel.Api.Http;

public static class ProxyEndpoints
{
    private const string OreAdminDiscordId = "822151223116824588";
    // ========== API endpoints (WPF token) ==========

    public static void MapProxyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/users", (HttpContext context, UserStore userStore) =>
        {
            var role = context.Items["Role"] as string;

            if (role != "Admin")
            {
                return Results.Forbid();
            }

            var users = userStore.GetAllUsers()
                .Select(u => new { u.Username, u.Role });

            return Results.Ok(users);
        });

        app.MapGet("/api/settings", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/settings");
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}" }, statusCode: 503);
            }
        });

        app.MapGet("/api/respawn/next", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/respawn/next");
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}" }, statusCode: 503);
            }
        });

        // === Settings ===

        app.MapGet("/settings", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/settings");
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}" }, statusCode: 503);
            }
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
            catch
            {
                /* ignore */
            }

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
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/settings/limits");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Results.Json(new { error = "Endpoint not available", max_channels = 3 }, statusCode: 404);
                    //return Results.Json(new { error = "Endpoint not available", limits = new object { } }, statusCode: 404);
                }

                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch
            {
                return Results.Json(new { error = "Worker unavailable", max_channels = 3 }, statusCode: 503);
                //return Results.Json(new { error = "Worker unavailable", limits = new object { } }, statusCode: 503);
            }
        });

        // === Respawn ===

        app.MapGet("/respawn/next", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/respawn/next");
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}" }, statusCode: 503);
            }
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

        app.MapPost("/channels/info", async (IHttpClientFactory cf, HttpContext ctx) =>
        {
            try
            {
                var http = cf.CreateClient("worker");

                // Przekaż całe body z requestu do workera
                using var reader = new StreamReader(ctx.Request.Body);
                var jsonContent = await reader.ReadToEndAsync();

                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                var response = await http.PostAsync("/channels/info", content);

                if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                {
                    return Results.Json(new { error = "Method not allowed" }, statusCode: 405);
                }

                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch
            {
                return Results.Json(new { error = "Worker unavailable" }, statusCode: 503);
            }
        });

        app.MapGet("/channels/voice", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/channels/voice");

                if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                {
                    // Worker nie wspiera GET na tym endpoincie - zwróć pustą listę
                    return Results.Json(new { channels = new object[] { } });
                }

                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
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

        // === Guilds / Ogur Role ===

        app.MapGet("/guilds", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/guilds");
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}", guilds = new object[] { } },
                    statusCode: 503);
            }
        });

        app.MapGet("/guilds/{guildId}/members/search", async (string guildId, HttpContext ctx, IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var query = ctx.Request.Query["query"].ToString();
                var limit = ctx.Request.Query["limit"].ToString();

                var url = $"/guilds/{guildId}/members/search?query={Uri.EscapeDataString(query)}";
                if (!string.IsNullOrEmpty(limit))
                {
                    url += $"&limit={Uri.EscapeDataString(limit)}";
                }

                var response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}", members = new object[] { } },
                    statusCode: 503);
            }
        });

        app.MapPost("/guilds/{guildId}/roles/ogur", async (string guildId, HttpContext ctx, IHttpClientFactory cf) =>
        {
            var role = ctx.Items["Role"] as string;
            if (role != "Admin")
            {
                return Results.Forbid();
            }

            try
            {
                var http = cf.CreateClient("worker");

                using var reader = new StreamReader(ctx.Request.Body);
                var jsonContent = await reader.ReadToEndAsync();

                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                var response = await http.PostAsync($"/guilds/{guildId}/roles/ogur", content);

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (!response.IsSuccessStatusCode)
                {
                    return Results.Json(result, statusCode: (int)response.StatusCode);
                }

                return Results.Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}" }, statusCode: 503);
            }
        });

        app.MapGet("/ore/state", async (IHttpClientFactory cf) =>
        {
            try
            {
                var http = cf.CreateClient("worker");
                var response = await http.GetAsync("/ore/state");
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return Results.Json(new { error = $"Worker error: {ex.Message}" }, statusCode: 503);
            }
        });

        app.MapPost("/ore/mark", async (HttpContext ctx, IHttpClientFactory cf, IDataProtectionProvider dp) =>
        {
            var cookie = ctx.Request.Cookies["ore_discord_identity"];
            if (string.IsNullOrEmpty(cookie))
                return Results.Json(new { error = "Not logged in" }, statusCode: 401);

            string discordId, username;
            try
            {
                var protector = dp.CreateProtector("OreDiscordIdentity");
                var json = protector.Unprotect(cookie);
                var doc = JsonDocument.Parse(json);
                discordId = doc.RootElement.GetProperty("id").GetString()!;
                username = doc.RootElement.GetProperty("username").GetString()!;
            }
            catch
            {
                return Results.Json(new { error = "Invalid session" }, statusCode: 401);
            }

            JsonElement body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
            }
            catch
            {
                return Results.BadRequest(new { error = "Invalid body" });
            }

            if (!body.TryGetProperty("x", out var xEl) || !body.TryGetProperty("y", out var yEl))
                return Results.BadRequest(new { error = "x, y are required" });

            var payload = new { x = xEl.GetDouble(), y = yEl.GetDouble(), user_id = discordId, username };

            var http = cf.CreateClient("worker");
            var response = await http.PostAsJsonAsync("/ore/mark", payload);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            return response.IsSuccessStatusCode
                ? Results.Ok(result)
                : Results.Json(result, statusCode: (int)response.StatusCode);
        });

        app.MapPost("/ore/reset", async (HttpContext ctx, IDataProtectionProvider dp, IHttpClientFactory cf) =>
        {
            var cookie = ctx.Request.Cookies["ore_discord_identity"];
            if (string.IsNullOrEmpty(cookie))
                return Results.Json(new { error = "Not logged in" }, statusCode: 401);

            string discordId;
            try
            {
                var protector = dp.CreateProtector("OreDiscordIdentity");
                var json = protector.Unprotect(cookie);
                var doc = JsonDocument.Parse(json);
                discordId = doc.RootElement.GetProperty("id").GetString()!;
            }
            catch
            {
                return Results.Json(new { error = "Invalid session" }, statusCode: 401);
            }

            if (discordId != OreAdminDiscordId)
                return Results.Forbid();

            var http = cf.CreateClient("worker");
            var res = await http.PostAsync("/ore/reset", null);
            res.EnsureSuccessStatusCode();
            var result = await res.Content.ReadFromJsonAsync<JsonElement>();
            return Results.Ok(result);
        });

        app.MapGet("/ore/whoami", (HttpContext ctx, IDataProtectionProvider dp) =>
        {
            var cookie = ctx.Request.Cookies["ore_discord_identity"];
            if (string.IsNullOrEmpty(cookie))
                return Results.Ok(new { logged_in = false, is_ore_admin = false });

            try
            {
                var protector = dp.CreateProtector("OreDiscordIdentity");
                var json = protector.Unprotect(cookie);
                var doc = JsonDocument.Parse(json);
                var discordId = doc.RootElement.GetProperty("id").GetString();
                return Results.Ok(new { logged_in = true, is_ore_admin = discordId == OreAdminDiscordId });
            }
            catch
            {
                return Results.Ok(new { logged_in = false, is_ore_admin = false });
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