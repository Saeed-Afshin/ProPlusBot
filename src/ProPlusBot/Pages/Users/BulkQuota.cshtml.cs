using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Pages.Messages;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Pages.Users;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class BulkQuotaModel(AdminUserBulkQuotaService bulkQuota) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SelectedUserIds { get; set; }

    [BindProperty(SupportsGet = true)]
    public AdminUserTargetKind Target { get; set; } = AdminUserTargetKind.Single;

    [BindProperty(SupportsGet = true)]
    public long? SingleUserId { get; set; }

    [BindProperty]
    public AdminBulkQuotaAction Action { get; set; }

    [BindProperty]
    public decimal Value { get; set; }

    public int SelectedUserCount => ParseUserIds(SelectedUserIds).Count;
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

    public async Task<IActionResult> OnPostApplyAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var ids = await bulkQuota.ResolveUserIdsAsync(Target, SingleUserId, SelectedUserIds, ct);
        if (ids.Count == 0)
        {
            ErrorMessage = "هیچ کاربری انتخاب نشده است.";
            return Page();
        }

        if (BulkQuotaUi.RequiresValue(Action) && Value <= 0)
        {
            ErrorMessage = "مقدار باید بزرگ‌تر از صفر باشد.";
            return Page();
        }

        var result = await bulkQuota.ApplyAsync(Action, ids, Value, ct);
        SuccessMessage = $"{result.Succeeded} کاربر به‌روزرسانی شد.";
        if (result.Failed > 0)
            ErrorMessage = $"{result.Failed} ناموفق. " + string.Join(" ", result.Errors.Take(10));

        return Page();
    }

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
