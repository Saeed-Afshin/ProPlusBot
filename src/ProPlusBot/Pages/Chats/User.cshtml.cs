using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Pages.Chats;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class UserModel(ChatLogAdminService chatLogs) : PageModel
{
    public ChatUserHeaderDto? ChatUser { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public long UserId { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; } = 50;
    public int TotalMessages { get; set; }
    public int TotalPages { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        if (UserId <= 0)
            return RedirectToPage("/Chats/Index");

        ChatUser = await chatLogs.GetUserHeaderAsync(UserId, ct);
        if (ChatUser is null)
            return NotFound();

        TotalMessages = await chatLogs.CountMessagesAsync(UserId, ct);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalMessages / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        Messages = await chatLogs.ListMessagesAsync(UserId, PageNumber, PageSize, ct);
        return Page();
    }
}
