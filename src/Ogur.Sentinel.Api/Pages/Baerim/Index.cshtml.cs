using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ogur.Sentinel.Api.Services;
using System.Text.Json;

namespace Ogur.Sentinel.Api.Pages.Baerim;

public class IndexModel : PageModel
{
    private readonly IDataProtectionProvider _dp;

    public IndexModel(IDataProtectionProvider dp)
    {
        _dp = dp;
    }

    public bool IsLoggedIn { get; private set; }
    public string DiscordUsername { get; private set; } = string.Empty;
    public string Lang { get; private set; } = "pl";

    public void OnGet(string? lang)
    {
        Lang = BaerimLang.Resolve(Request, Response, lang);

        var cookie = Request.Cookies["ore_discord_identity"];
        if (string.IsNullOrEmpty(cookie))
        {
            return;
        }

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
}