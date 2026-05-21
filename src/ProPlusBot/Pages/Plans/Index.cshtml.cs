using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Pages.Plans;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(SubscriptionAdminService adminService, TrialSettingsService trialSettings) : PageModel
{
    private static readonly SubscriptionPlan[] PlanColumnOrder =
    [
        SubscriptionPlan.Free,
        SubscriptionPlan.Bronze,
        SubscriptionPlan.Silver,
        SubscriptionPlan.Golden
    ];

    public IReadOnlyList<SubscriptionPlan> PlanColumns { get; private set; } = PlanColumnOrder;
    public List<PlanLimitMatrixRow> LimitRows { get; set; } = [];
    public Dictionary<SubscriptionPlan, long> PlanPrices { get; set; } = new();
    public Entities.ExtraQuotaPackSettings ExtraPack { get; set; } = null!;
    public int TrialDurationDays { get; set; } = 7;
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public int PostedTrialDurationDays { get; set; } = 7;

    [BindProperty]
    public Dictionary<int, long> PostedPlanPrices { get; set; } = new();

    [BindProperty]
    public long ExtraPrice { get; set; }

    [BindProperty]
    public int ExtraCount { get; set; }

    [BindProperty]
    public decimal ExtraMegabytes { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await LoadAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostSavePlanPricesAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        foreach (var (planKey, price) in PostedPlanPrices)
        {
            if (!Enum.IsDefined(typeof(SubscriptionPlan), planKey))
                continue;

            await adminService.UpdatePricingAsync((SubscriptionPlan)planKey, price, ct);
        }

        SuccessMessage = "قیمت ماهانه پلن‌ها ذخیره شد.";
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSaveLimitRowAsync(
        MediaPlatformKind platform,
        UsagePeriod period,
        QuotaLimitKind limitKind,
        Dictionary<int, decimal> planValues,
        CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await adminService.UpdateLimitRowAsync(platform, period, limitKind, planValues, ct);
        SuccessMessage = "محدودیت‌های ردیف ذخیره شد.";
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSaveTrialAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await trialSettings.UpdateDurationDaysAsync(PostedTrialDurationDays, ct);
        SuccessMessage = "تنظیمات دوره آزمایشی ذخیره شد.";
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSaveExtraAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var extraBytes = ByteUnits.FromMegabytes(ExtraMegabytes);
        await adminService.UpdateExtraPackAsync(ExtraPrice, ExtraCount, extraBytes, ct);
        SuccessMessage = "بسته سهمیه اضافه ذخیره شد.";
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        LimitRows = await adminService.GetLimitMatrixAsync(ct);

        var plans = await adminService.GetPricingAsync(ct);
        PlanPrices = plans.ToDictionary(p => p.Plan, p => p.MonthlyPriceToman);
        foreach (var plan in PlanColumnOrder)
        {
            if (!PlanPrices.ContainsKey(plan))
                PlanPrices[plan] = 0;
        }

        TrialDurationDays = (await trialSettings.GetAsync(ct)).DurationDays;
        PostedTrialDurationDays = TrialDurationDays;

        ExtraPack = await adminService.GetExtraPackAsync(ct);
        ExtraPrice = ExtraPack.PriceToman;
        ExtraCount = ExtraPack.ExtraDownloadCount;
        ExtraMegabytes = ByteUnits.ToMegabytes(ExtraPack.ExtraDownloadBytes);
    }
}
