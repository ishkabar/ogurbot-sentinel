using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Ogur.Sentinel.Api.Http;

public static class DiscordAuthEndpoints
{
    public static void MapDiscordAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/discord/login", (HttpContext ctx, IConfiguration cfg) =>
        {
            var clientId = cfg["Discord:ClientId"];
            var redirectUri = cfg["Discord:RedirectUri"];

            var returnTo = ctx.Request.Query["returnTo"].ToString();
            if (string.IsNullOrEmpty(returnTo) || !returnTo.StartsWith("/"))
                returnTo = "/baerim/ore/chunjo";

            var state = Uri.EscapeDataString(returnTo);
            var url = "https://discord.com/api/oauth2/authorize" +
                      $"?client_id={clientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri!)}" +
                      "&response_type=code&scope=identify" +
                      $"&state={state}";

            return Results.Redirect(url);
        });

        app.MapGet("/auth/discord/callback", async (
            HttpContext ctx, IConfiguration cfg, IHttpClientFactory cf,
            IDataProtectionProvider dp, ILogger<Program> logger) =>
        {
            var code = ctx.Request.Query["code"].ToString();
            var state = ctx.Request.Query["state"].ToString();

            if (string.IsNullOrEmpty(code))
                return Results.Redirect("/baerim/ore/chunjo?error=oauth");

            var clientId = cfg["Discord:ClientId"];
            var clientSecret = cfg["Discord:ClientSecret"];
            var redirectUri = cfg["Discord:RedirectUri"];

            var http = cf.CreateClient();

            var tokenResp = await http.PostAsync("https://discord.com/api/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId!,
                    ["client_secret"] = clientSecret!,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri!
                }));

            if (!tokenResp.IsSuccessStatusCode)
            {
                logger.LogWarning("[ORE-OAUTH] Token exchange failed: {Status}", tokenResp.StatusCode);
                return Results.Redirect("/baerim/ore/chunjo?error=oauth");
            }

            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = tokenJson.GetProperty("access_token").GetString();

            var userReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
            userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var userResp = await http.SendAsync(userReq);

            if (!userResp.IsSuccessStatusCode)
            {
                logger.LogWarning("[ORE-OAUTH] /users/@me failed: {Status}", userResp.StatusCode);
                return Results.Redirect("/baerim/ore/chunjo?error=oauth");
            }

            var userJson = await userResp.Content.ReadFromJsonAsync<JsonElement>();
            var discordId = userJson.GetProperty("id").GetString()!;
            var username = userJson.TryGetProperty("global_name", out var gn) && gn.ValueKind == JsonValueKind.String
                ? gn.GetString()!
                : userJson.GetProperty("username").GetString()!;

            var protector = dp.CreateProtector("OreDiscordIdentity");
            var payload = JsonSerializer.Serialize(new { id = discordId, username });
            var protectedPayload = protector.Protect(payload);

            ctx.Response.Cookies.Append("ore_discord_identity", protectedPayload, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            logger.LogInformation("[ORE-OAUTH] Logged in: {Username} ({Id})", username, discordId);

            var returnTo = string.IsNullOrEmpty(state) ? "/baerim/ore/chunjo" : Uri.UnescapeDataString(state);
            if (!returnTo.StartsWith("/")) returnTo = "/baerim/ore/chunjo";
            return Results.Redirect(returnTo);
        });

        app.MapGet("/auth/discord/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete("ore_discord_identity");
            return Results.Redirect("/baerim/ore/chunjo");
        });
    }
}