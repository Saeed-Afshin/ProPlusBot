using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;

namespace ProPlusBot.Pages;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class LogoutModel : PageModel
{
    public IActionResult OnPost()
    {
        AdminAuthHelper.ClearAuthCookie(Response);
        return RedirectToPage("/Login");
    }
}
