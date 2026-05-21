using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Services;

namespace ProPlusBot.Pages.Settings;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(BotSettingsService settingsService) : PageModel
{
    [BindProperty]
    public BotMode Mode { get; set; }

    [BindProperty]
    public BotUpdateMode UpdateMode { get; set; }

    [BindProperty]
    public bool IsActive { get; set; }

    [BindProperty]
    public bool YouTubeEnabled { get; set; }

    [BindProperty]
    public bool PinterestEnabled { get; set; }

    [BindProperty]
    public string? WebhookUrl { get; set; }

    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var s = await settingsService.GetAsync(ct);
        Mode = s.Mode;
        UpdateMode = s.UpdateMode;
        IsActive = s.IsActive;
        YouTubeEnabled = s.YouTubeEnabled;
        PinterestEnabled = s.PinterestEnabled;
        WebhookUrl = s.WebhookUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await settingsService.UpdateAsync(
            Mode, UpdateMode, IsActive, WebhookUrl, YouTubeEnabled, PinterestEnabled, User.GetAdminId(), ct);
        SuccessMessage = "تنظیمات ذخیره شد. برای تغییر حالت دریافت پیام، برنامه را مجدداً راه‌اندازی کنید.";
        return Page();
    }
}
