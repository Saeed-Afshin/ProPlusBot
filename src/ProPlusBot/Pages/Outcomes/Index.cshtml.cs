using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Pages.Outcomes;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(UserInteractionLogService interactionLog) : PageModel
{
    public List<UserInteractionListItemDto> Outcomes { get; set; } = [];

    public UserInteractionStatus? StatusFilter { get; set; }
    public long? UserIdFilter { get; set; }

    public async Task OnGetAsync(UserInteractionStatus? status, long? userId, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return;

        StatusFilter = status;
        UserIdFilter = userId;
        Outcomes = await interactionLog.ListAsync(userId, status, ct: ct);
    }
}
