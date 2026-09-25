using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ogur.Sentinel.Api.Services;
using System.Text.Json;

namespace Ogur.Sentinel.Api.Pages.Baerim.Ore;

public class ChunjoModel : PageModel
{
    private readonly IDataProtectionProvider _dp;
    private readonly OreVisitLogger _visitLogger;

    public ChunjoModel(IDataProtectionProvider dp, OreVisitLogger visitLogger)
    {
        _dp = dp;
        _visitLogger = visitLogger;
    }

    public bool IsLoggedIn { get; private set; }
    public string DiscordUsername { get; private set; } = string.Empty;
    public string Lang { get; private set; } = "pl";

    public async Task OnGetAsync(string? lang)
    {
        Lang = BaerimLang.Resolve(Request, Response, lang);

        var cookie = Request.Cookies["ore_discord_identity"];
        if (!string.IsNullOrEmpty(cookie))
        {
            try
            {
                var protector = _dp.CreateProtector("OreDiscordIdentity");
                var json = protector.Unprotect(cookie);
                var doc = JsonDocument.Parse(json);
                DiscordUsername = doc.RootElement.GetProperty("username").GetString() ?? "";
                IsLoggedIn = !string.IsNullOrEmpty(DiscordUsername);
            }
            catch
            {
                IsLoggedIn = false;
            }
        }

        if (IsLoggedIn)
        {
            var ip = GetClientIp();
            _ = _visitLogger.LogVisitAsync(DiscordUsername, ip);
        }
    }

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}