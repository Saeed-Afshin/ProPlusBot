using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Pages.ErrorLogs;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class DetailModel(ErrorLogAdminService errorLogAdmin) : PageModel
{
    public ErrorLogDetailDto? ErrorLog { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        ErrorLog = await errorLogAdmin.GetAsync(id, ct);
        return ErrorLog is null ? NotFound() : Page();
    }
}
