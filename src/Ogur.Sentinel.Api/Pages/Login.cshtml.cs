using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ogur.Sentinel.Api.Pages;

public class LoginModel : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}