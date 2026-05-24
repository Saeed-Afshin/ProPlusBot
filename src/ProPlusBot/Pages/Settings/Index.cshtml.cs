using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Services;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Pages.Settings;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(BotSettingsService settingsService, IConfiguration configuration) : PageModel
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

    [BindProperty]
    public string SearchGridPreset { get; set; } = "3×3";

    [BindProperty]
    public int SearchGridJpegQuality { get; set; } = SearchGridPresets.DefaultJpegQuality;

    [BindProperty]
    public ConversationStateBackend ConversationStateBackend { get; set; } = ConversationStateBackend.Memory;

    public bool RedisConfigured { get; private set; }

    public IReadOnlyList<string> SearchGridPresetOptions { get; private set; } =
        SearchGridPresets.Allowed.Select(p => SearchGridPresets.Format(p.Columns, p.Rows)).ToList();

    [BindProperty]
    public string? YouTubeCookiesPaste { get; set; }

    [BindProperty]
    public bool ClearYouTubeCookies { get; set; }

    public bool YouTubeCookiesConfigured { get; private set; }

    public DateTime? YouTubeCookiesUpdatedAt { get; private set; }

    public string? ErrorMessage { get; set; }

    public string? WarningMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await LoadAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var (columns, rows) = ParseGridPreset(SearchGridPreset);

        if (ClearYouTubeCookies || !string.IsNullOrWhiteSpace(YouTubeCookiesPaste))
        {
            var (cookieOk, cookieError, cookieWarning) = await settingsService.UpdateYouTubeCookiesAsync(
                YouTubeCookiesPaste,
                ClearYouTubeCookies,
                User.GetAdminId(),
                ct);

            if (!cookieOk)
            {
                ErrorMessage = cookieError;
                await LoadAsync(ct);
                return Page();
            }

            if (!string.IsNullOrEmpty(cookieWarning))
                WarningMessage = cookieWarning;
        }

        await settingsService.UpdateAsync(
            Mode,
            UpdateMode,
            IsActive,
            WebhookUrl,
            YouTubeEnabled,
            PinterestEnabled,
            columns,
            rows,
            SearchGridJpegQuality,
            ConversationStateBackend,
            User.GetAdminId(),
            ct);

        SuccessMessage = ClearYouTubeCookies || !string.IsNullOrWhiteSpace(YouTubeCookiesPaste)
            ? "تنظیمات و کوکی یوتیوب ذخیره شد. بدون راه‌اندازی مجدد اعمال می‌شود."
            : "تنظیمات ذخیره شد. برای تغییر حالت دریافت پیام، برنامه را مجدداً راه‌اندازی کنید.";
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var s = await settingsService.GetAsync(ct);
        Mode = s.Mode;
        UpdateMode = s.UpdateMode;
        IsActive = s.IsActive;
        YouTubeEnabled = s.YouTubeEnabled;
        PinterestEnabled = s.PinterestEnabled;
        WebhookUrl = s.WebhookUrl;
        SearchGridPreset = SearchGridPresets.Format(s.SearchGridColumns, s.SearchGridRows);
        SearchGridJpegQuality = s.SearchGridJpegQuality;
        ConversationStateBackend = s.ConversationStateBackend;
        RedisConfigured = !string.IsNullOrWhiteSpace(configuration["Redis:ConnectionString"]);

        var cookieStatus = await settingsService.GetYouTubeCookiesStatusAsync(ct);
        YouTubeCookiesConfigured = cookieStatus.Configured;
        YouTubeCookiesUpdatedAt = cookieStatus.UpdatedAt;
    }

    private static (int Columns, int Rows) ParseGridPreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return (3, 3);

        var normalized = preset.Replace("x", "×", StringComparison.OrdinalIgnoreCase).Trim();
        var parts = normalized.Split('×', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var columns)
            && int.TryParse(parts[1], out var rows))
        {
            return SearchGridPresets.Normalize(columns, rows);
        }

        return (3, 3);
    }
}
