using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages;

public class Error403Model : PageModel
{
    public string Message { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;

    public void OnGet(string? message = null, string? role = null)
    {
        Message = message ?? "Nie masz uprawnień do tej strony.";
        Role = role ?? (HttpContext.Items["Role"] as string) ?? "Nieznana";
        
        // Dostosuj sugestię do roli
        Suggestion = Role switch
        {
            "Timer" => "Użyj aplikacji desktopowej do sprawdzania czasów respawn przez endpoint API /api/respawn/next",
            "Operator" => "Masz dostęp tylko do odczytu. Skontaktuj się z administratorem aby wprowadzać zmiany.",
            _ => "Skontaktuj się z administratorem w celu uzyskania dostępu."
        };
    }
}