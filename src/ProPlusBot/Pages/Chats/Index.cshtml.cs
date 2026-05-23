using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Pages.Chats;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(ChatLogAdminService chatLogs) : PageModel
{
    public List<ChatUserSummaryDto> Conversations { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        Conversations = await chatLogs.ListConversationSummariesAsync(Search, ct: ct);
        return Page();
    }
}
