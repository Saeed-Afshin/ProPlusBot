using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Configuration;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Pages.Plans;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(
    PlanDefinitionService planDefinitions,
    BotSettingsService botSettings,
    TrialSettingsService trialSettings,
    IConfiguration configuration) : PageModel
{
    private static readonly SubscriptionPlan[] PlanColumnOrder =
    [
        SubscriptionPlan.Free,
        SubscriptionPlan.Bronze,
        SubscriptionPlan.Silver,
        SubscriptionPlan.Golden
    ];

    public IReadOnlyList<SubscriptionPlan> PlanColumns { get; private set; } = PlanColumnOrder;
    public List<PlanDefinitionDto> Plans { get; set; } = [];
    public int TrialDurationDays { get; set; } = 7;
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public int PostedTrialDurationDays { get; set; } = 7;

    [BindProperty]
    public List<PlanDefinitionDto> PostedPlans { get; set; } = [];

    [BindProperty]
    public int BaleDirectArvanMegabytes { get; set; } = UploadFallbackPresets.BytesToMegabytes(UploadFallbackPresets.DefaultMinBytes);

    public bool ArvanCloudConfigured { get; private set; }

    public IReadOnlyList<int> FallbackExpiryHourOptions { get; private set; } =
        UploadFallbackPresets.AllowedExpiryHours;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        await LoadAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostSavePlansAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        foreach (var plan in PostedPlans)
            await planDefinitions.UpdateDefinitionAsync(plan, ct);

        await botSettings.UpdateBaleDirectArvanThresholdAsync(
            UploadFallbackPresets.MegabytesToBytes(BaleDirectArvanMegabytes),
            User.GetAdminId(),
            ct);

        SuccessMessage = "تنظیمات بسته‌ها و ArvanCloud ذخیره شد.";
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

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var all = await planDefinitions.GetAllDefinitionsAsync(ct);
        Plans = PlanColumnOrder
            .Select(plan => all.First(p => p.Plan == plan))
            .ToList();
        PostedPlans = Plans.ToList();

        var bot = await botSettings.GetAsync(ct);
        BaleDirectArvanMegabytes = UploadFallbackPresets.BytesToMegabytes(bot.BaleDirectArvanThresholdBytes);

        ArvanCloudConfigured = configuration.GetSection(ArvanCloudStorageOptions.SectionName)
            .Get<ArvanCloudStorageOptions>()?.IsConfigured == true;

        TrialDurationDays = (await trialSettings.GetAsync(ct)).DurationDays;
        PostedTrialDurationDays = TrialDurationDays;
    }
}
