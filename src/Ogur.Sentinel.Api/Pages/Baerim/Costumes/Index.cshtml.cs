using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages.Baerim.Costumes;

public class IndexModel : PageModel
{
    // Dostosuj do tego, jak reszta podstron Baerim czyta jezyk i stan logowania.
    // Jesli masz wspolna klase bazowa (jak w Ore), dziedzicz po niej zamiast tego.
    public string Lang { get; private set; } = "pl";
    public bool IsLoggedIn { get; private set; }
    public string? DiscordUsername { get; private set; }

    public void OnGet(string? lang)
    {
        if (!string.IsNullOrEmpty(lang) && (lang == "pl" || lang == "en"))
            Lang = lang;

        // TODO: podepnij realny stan sesji Discord, tak jak w Ore/Chunjo.
        // IsLoggedIn = ...;
        // DiscordUsername = ...;
    }
}