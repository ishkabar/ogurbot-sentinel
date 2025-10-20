using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    
    public string? ErrorMessage { get; set; }

    public LoginModel(IConfiguration config)
    {
        _config = config;
    }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("IsAuthenticated") == "true")
        {
            return RedirectToPage("/Respawn");
        }
        
        return Page();
    }

    public async Task<IActionResult> OnPost(string username, string password)
    {
        var validUser = _config["Auth:RespawnUser"] ?? "admin";
        var validPass = _config["Auth:RespawnPassword"] ?? "changeme";

        if (username == validUser && password == validPass)
        {
            HttpContext.Session.SetString("IsAuthenticated", "true");
            await HttpContext.Session.CommitAsync(); // ✅ Wymuszaj zapis
            return RedirectToPage("/Respawn");
        }

        ErrorMessage = "Invalid username or password";
        return Page();
    }
}