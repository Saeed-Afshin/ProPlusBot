using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProPlusBot.Auth;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Pages.Users;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class ManageModel(
    SubscriptionService subscriptionService,
    QuotaService quotaService,
    UserPlanLimitService userPlanLimitService,
    AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long? UserId { get; set; }

    [BindProperty]
    public long EditUserId { get; set; }

    [BindProperty]
    public SubscriptionPlan EditPlan { get; set; }

    [BindProperty]
    public string? EditExpiresDateShamsi { get; set; }

    [BindProperty]
    public string EditExpiresTime { get; set; } = "00:00:00";

    [BindProperty]
    public int ExtendDays { get; set; } = 30;

    [BindProperty]
    public MediaPlatformKind QuotaPlatform { get; set; }

    [BindProperty]
    public int QuotaCountDelta { get; set; }

    [BindProperty]
    public decimal QuotaMegabytesDelta { get; set; }

    public UserAccountSummaryDto? AccountSummary { get; set; }
    public IReadOnlyList<UserLimitEditRow> UserLimitRows { get; set; } = [];
    public IReadOnlyList<ReservedPlanDto> ReservedPlans { get; set; } = [];

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var id = UserId ?? (EditUserId > 0 ? EditUserId : (long?)null);
        if (id is > 0)
            await LoadUserAsync(id.Value, ct);

        return Page();
    }

    public async Task<IActionResult> OnPostSetPlanAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        try
        {
            var expiresAt = PersianDateTimeHelper.CombineShamsiDateAndTimeToUtc(
                EditExpiresDateShamsi,
                EditExpiresTime);

            await subscriptionService.ApplyAdminPlanChangeAsync(EditUserId, EditPlan, expiresAt, ct);
            SuccessMessage = "پلن کاربر به‌روزرسانی شد.";
            await LoadUserAsync(EditUserId, ct);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostExtendAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        try
        {
            await subscriptionService.ExtendPlanAsync(EditUserId, ExtendDays, ct);
            SuccessMessage = $"اشتراک {ExtendDays} روز تمدید شد.";
            await LoadUserAsync(EditUserId, ct);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAdjustQuotaAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var bytesDelta = ByteUnits.FromMegabytes(QuotaMegabytesDelta);
        await subscriptionService.AdjustQuotaAsync(EditUserId, QuotaPlatform, QuotaCountDelta, bytesDelta, ct);
        SuccessMessage = "سهمیه اضافه کاربر تنظیم شد.";
        await LoadUserAsync(EditUserId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveUserLimitRowAsync(
        MediaPlatformKind platform,
        UsagePeriod period,
        QuotaLimitKind limitKind,
        decimal value,
        CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await userPlanLimitService.SaveUserLimitRowAsync(EditUserId, platform, period, limitKind, value, ct);
        SuccessMessage = "محدودیت کاربر ذخیره شد.";
        await LoadUserAsync(EditUserId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostClearUserLimitsAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await userPlanLimitService.ClearUserLimitsAsync(EditUserId, ct);
        SuccessMessage = "محدودیت‌های سفارشی کاربر حذف شد (پیش‌فرض پلن اعمال می‌شود).";
        await LoadUserAsync(EditUserId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteReservedAsync(Guid reservationId, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var row = await db.UserReservedPlans
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.TelegramUserId == EditUserId, ct);

        if (row is not null)
        {
            db.UserReservedPlans.Remove(row);
            await db.SaveChangesAsync(ct);
            SuccessMessage = "پلن رزرو حذف شد.";
        }

        await LoadUserAsync(EditUserId, ct);
        return Page();
    }

    private async Task LoadUserAsync(long telegramUserId, CancellationToken ct)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user is null)
        {
            AccountSummary = null;
            UserLimitRows = [];
            ReservedPlans = [];
            return;
        }

        EditUserId = user.TelegramUserId;
        EditPlan = user.Plan;

        string date;
        string time;
        if (user.Plan == SubscriptionPlan.Free)
            PersianDateTimeHelper.SetShamsiDateAndTimeNow(out date, out time);
        else if (user.PlanExpiresAt.HasValue)
            PersianDateTimeHelper.SetShamsiDateAndTimeFromUtc(user.PlanExpiresAt.Value, out date, out time);
        else
            PersianDateTimeHelper.SetShamsiDateAndTimeNow(out date, out time);

        EditExpiresDateShamsi = date;
        EditExpiresTime = time;

        try
        {
            AccountSummary = await quotaService.GetAccountSummaryAsync(telegramUserId, ct);
            ReservedPlans = AccountSummary.ReservedPlans;

            var effectivePlan = AccountSummary.EffectivePlan;
            UserLimitRows = await userPlanLimitService.GetUserLimitRowsAsync(telegramUserId, effectivePlan, ct);
        }
        catch
        {
            AccountSummary = null;
            UserLimitRows = [];
            ReservedPlans = [];
        }
    }
}
