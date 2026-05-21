using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Pages.Users;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(
    SubscriptionAdminService adminService,
    SubscriptionService subscriptionService) : PageModel
{
    public List<Models.BotUserAdminDto> Users { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        Users = await adminService.ListUsersAsync(Search, ct: ct);
        return Page();
    }

    public async Task<IActionResult> OnPostBanAsync(long userId, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await subscriptionService.SetBanAsync(userId, true, ct);
        SuccessMessage = "کاربر مسدود شد.";
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostUnbanAsync(long userId, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await subscriptionService.SetBanAsync(userId, false, ct);
        SuccessMessage = "مسدودیت کاربر برداشته شد.";
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Users = await adminService.ListUsersAsync(Search, ct: ct);
        return Page();
    }
}
