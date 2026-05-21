using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;

namespace ProPlusBot.Pages;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(AuthConstants.Scheme);
        return RedirectToPage("/Login");
    }
}
