using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages.Baerim.Events;

public class IndexModel : PageModel
{
    public string Lang { get; private set; } = "pl";
    public bool IsLoggedIn { get; private set; }
    public string? DiscordUsername { get; private set; }

    public void OnGet(string? lang)
    {
        if (lang is "pl" or "en")
            Lang = lang;
    }
}