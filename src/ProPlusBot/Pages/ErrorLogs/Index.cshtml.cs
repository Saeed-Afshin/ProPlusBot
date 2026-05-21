using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Pages.ErrorLogs;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(ErrorLogAdminService errorLogAdmin) : PageModel
{
    public List<ErrorLogListItemDto> ErrorLogs { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return;

        ErrorLogs = await errorLogAdmin.ListAsync(ct: ct);
    }
}
