using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly ILogger<LoginModel> _logger;

    public string? ErrorMessage { get; set; }

    public LoginModel(IConfiguration config, ILogger<LoginModel> logger)
    {
        _config = config;
        _logger = logger;
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
        _logger.LogInformation("🔍 Login attempt - username: '{Username}', password length: {Length}",
            username, password?.Length ?? 0);

        // Sprawdź skąd są brane wartości
        var adminUser = _config["Auth:AdminUser"];
        var adminUserSource = adminUser != null
            ? "Auth:AdminUser"
            : (_config["Auth:RespawnUser"] != null ? "Auth:RespawnUser" : "default");
        adminUser = adminUser ?? _config["Auth:RespawnUser"] ?? "admin";

        var adminPass = _config["Auth:AdminPassword"];
        var adminPassSource = adminPass != null
            ? "Auth:AdminPassword"
            : (_config["Auth:RespawnPassword"] != null ? "Auth:RespawnPassword" : "default");
        adminPass = adminPass ?? _config["Auth:RespawnPassword"] ?? "changeme";

        var viewerUser = _config["Auth:ViewerUser"];
        var viewerPass = _config["Auth:ViewerPassword"];

        _logger.LogInformation(
            "🔍 Config source - adminUser from: {AdminUserSource}, adminPass from: {AdminPassSource}",
            adminUserSource, adminPassSource);
        _logger.LogInformation("🔍 Expected - adminUser: '{AdminUser}', adminPass length: {AdminPassLength}",
            adminUser, adminPass?.Length ?? 0);
        _logger.LogInformation("🔍 Expected - viewerUser: '{ViewerUser}', viewerPass length: {ViewerPassLength}",
            viewerUser ?? "null", viewerPass?.Length ?? 0);
        _logger.LogInformation("🔍 Match admin? {MatchAdmin}, Match viewer? {MatchViewer}",
            username == adminUser && password == adminPass,
            !string.IsNullOrEmpty(viewerUser) && username == viewerUser && password == viewerPass);

        if (username == adminUser && password == adminPass)
        {
            _logger.LogInformation("✅ Admin login SUCCESS for user: {Username}", username);
            HttpContext.Session.SetString("IsAuthenticated", "true");
            HttpContext.Session.SetString("Role", "Admin");
            HttpContext.Session.SetString("Username", username);
            await HttpContext.Session.CommitAsync();
            return RedirectToPage("/Respawn");
        }
        else if (!string.IsNullOrEmpty(viewerUser) && username == viewerUser && password == viewerPass)
        {
            _logger.LogInformation("✅ Viewer login SUCCESS for user: {Username}", username);
            HttpContext.Session.SetString("IsAuthenticated", "true");
            HttpContext.Session.SetString("Role", "Viewer");
            HttpContext.Session.SetString("Username", username);
            await HttpContext.Session.CommitAsync();
            return RedirectToPage("/Respawn");
        }

        _logger.LogWarning("❌ Login FAILED for username: {Username}", username);
        ErrorMessage = "Invalid username or password";
        return Page();
    }
}