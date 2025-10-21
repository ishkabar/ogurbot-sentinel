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
        var adminUser = _config["Auth:AdminUser"] ?? _config["Auth:RespawnUser"] ?? "admin";
        var adminPass = _config["Auth:AdminPassword"] ?? _config["Auth:RespawnPassword"] ?? "changeme";
        var viewerUser = _config["Auth:ViewerUser"];
        var viewerPass = _config["Auth:ViewerPassword"];

        if (username == adminUser && password == adminPass)
        {
            HttpContext.Session.SetString("IsAuthenticated", "true");
            HttpContext.Session.SetString("Role", "Admin");
            HttpContext.Session.SetString("Username", username);
            await HttpContext.Session.CommitAsync();
            return RedirectToPage("/Respawn");
        }
        else if (!string.IsNullOrEmpty(viewerUser) && username == viewerUser && password == viewerPass)
        {
            HttpContext.Session.SetString("IsAuthenticated", "true");
            HttpContext.Session.SetString("Role", "Viewer");
            HttpContext.Session.SetString("Username", username);
            await HttpContext.Session.CommitAsync();
            return RedirectToPage("/Respawn");
        }

        ErrorMessage = "Invalid username or password";
        return Page();
    }
}