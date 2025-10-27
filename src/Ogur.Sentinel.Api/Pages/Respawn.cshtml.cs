using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages;

public class RespawnModel : PageModel
{
    public bool IsViewer { get; set; }
    public bool IsTimer { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    public void OnGet()
    {
        var role = HttpContext.Items["Role"] as string;
        var username = HttpContext.Items["Username"] as string;
        
        Role = role ?? "Unknown";
        Username = username ?? "Unknown";
        
        // Timer nie ma dostępu w ogóle
        IsTimer = role == "Timer";
        
        // Operator ma tylko odczyt - ustawiamy IsViewer=true
        IsViewer = role == "Operator";
    }
}