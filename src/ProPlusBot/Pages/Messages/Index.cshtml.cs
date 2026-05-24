using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Services.Messaging;

namespace ProPlusBot.Pages.Messages;

public enum AdminUserTargetKind
{
    Single,
    All,
    Selected
}

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(
    AdminMessagingService messaging,
    BaleUserProfileSyncService profileSync) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SelectedUserIds { get; set; }

    [BindProperty(SupportsGet = true)]
    public AdminUserTargetKind Target { get; set; } = AdminUserTargetKind.Single;

    public int SelectedUserCount =>
        ParseUserIds(SelectedUserIds).Count;

    [BindProperty(SupportsGet = true)]
    public long? SingleUserId { get; set; }

    [BindProperty]
    public string MessageText { get; set; } = "";

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        if (!string.IsNullOrWhiteSpace(SelectedUserIds))
            Target = AdminUserTargetKind.Selected;
        else if (SingleUserId is > 0)
            Target = AdminUserTargetKind.Single;

        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(MessageText))
        {
            ErrorMessage = "متن پیام خالی است.";
            return Page();
        }

        var ids = await ResolveTargetUserIdsAsync(ct);
        var result = await messaging.BroadcastAsync(ids, MessageText, ct);
        SuccessMessage =
            $"ارسال: {result.Sent} موفق، {result.Failed} ناموفق، {result.Skipped} رد شده.";
        if (result.Errors.Count > 0)
            ErrorMessage = string.Join(" ", result.Errors);

        return Page();
    }

    public async Task<IActionResult> OnPostSyncAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var ids = await ResolveTargetUserIdsAsync(ct);
        var result = await profileSync.SyncUsersAsync(ids, ct);
        SuccessMessage =
            $"همگام‌سازی: {result.Updated} به‌روز، {result.Failed} ناموفق، {result.Skipped} رد شده.";
        if (result.Errors.Count > 0)
            ErrorMessage = string.Join(" ", result.Errors);

        return Page();
    }

    private async Task<IReadOnlyList<long>> ResolveTargetUserIdsAsync(CancellationToken ct) =>
        Target switch
        {
            AdminUserTargetKind.Single when SingleUserId is > 0 => [SingleUserId.Value],
            AdminUserTargetKind.All => await messaging.GetAllUserIdsAsync(ct),
            AdminUserTargetKind.Selected => ParseUserIds(SelectedUserIds),
            _ => []
        };

    private static IReadOnlyList<long> ParseUserIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split([',', '\n', '\r', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : (long?)null)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }
}
