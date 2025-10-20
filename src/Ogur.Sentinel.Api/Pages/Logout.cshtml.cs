using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace Ogur.Sentinel.Api.Pages;

//public class Logout : PageModel
public class LogoutModel : PageModel

{
    //public void OnGet() {}
    public IActionResult OnPost()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Login");
    }
}