using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages;

public class RespawnModel : PageModel
{
    public bool IsViewer { get; set; }
    
    public void OnGet()
    {
        IsViewer = HttpContext.Session.GetString("Role") == "Viewer";
    }
}