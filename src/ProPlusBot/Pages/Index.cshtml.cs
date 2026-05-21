using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProPlusBot.Auth;
using ProPlusBot.Data;
using ProPlusBot.Services;

namespace ProPlusBot.Pages;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(AppDbContext db, BotSettingsService settingsService) : PageModel
{
    public int TotalUsers { get; set; }
    public int TotalMessages { get; set; }
    public string BotMode { get; set; } = string.Empty;
    public bool BotActive { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        TotalUsers = await db.BotUsers.CountAsync(ct);
        TotalMessages = await db.ChatMessages.CountAsync(ct);
        var settings = await settingsService.GetAsync(ct);
        BotMode = settings.Mode.ToString();
        BotActive = settings.IsActive;
        return Page();
    }
}
