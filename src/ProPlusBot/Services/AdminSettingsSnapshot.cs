using ProPlusBot.Entities;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Services;

public sealed class AdminSettingsSnapshot
{
    public required BotSetting Bot { get; init; }

    public required TrialSettings Trial { get; init; }

    public required IReadOnlyDictionary<SubscriptionPlan, PlanPricing> Plans { get; init; }

    public PlanPricing GetPlan(SubscriptionPlan plan) =>
        Plans.TryGetValue(plan, out var row) ? row : throw new KeyNotFoundException($"Plan {plan} not in cache.");

    public UploadFallbackConfigSnapshot ToUploadFallbackConfig()
    {
        var policies = Plans.ToDictionary(p => p.Key, p => PlanFallbackPolicy.From(p.Value));
        return new UploadFallbackConfigSnapshot
        {
            BaleDirectArvanThresholdBytes = Bot.BaleDirectArvanThresholdBytes,
            Plans = policies
        };
    }

    public static AdminSettingsSnapshot Create(BotSetting bot, TrialSettings trial, IEnumerable<PlanPricing> plans) =>
        new()
        {
            Bot = CloneBot(bot),
            Trial = CloneTrial(trial),
            Plans = plans.ToDictionary(p => p.Plan, ClonePlan)
        };

    private static BotSetting CloneBot(BotSetting s) => new()
    {
        Id = s.Id,
        Mode = s.Mode,
        UpdateMode = s.UpdateMode,
        IsActive = s.IsActive,
        YouTubeEnabled = s.YouTubeEnabled,
        PinterestEnabled = s.PinterestEnabled,
        BaleDirectArvanThresholdBytes = s.BaleDirectArvanThresholdBytes,
        SearchGridColumns = s.SearchGridColumns,
        SearchGridRows = s.SearchGridRows,
        SearchGridJpegQuality = s.SearchGridJpegQuality,
        ConversationStateBackend = s.ConversationStateBackend,
        WebhookUrl = s.WebhookUrl,
        YouTubeCookiesContent = s.YouTubeCookiesContent,
        YouTubeCookiesUpdatedAt = s.YouTubeCookiesUpdatedAt,
        UpdatedAt = s.UpdatedAt,
        UpdatedByAdminId = s.UpdatedByAdminId
    };

    private static TrialSettings CloneTrial(TrialSettings s) => new()
    {
        Id = s.Id,
        DurationDays = s.DurationDays,
        UpdatedAt = s.UpdatedAt
    };

    private static PlanPricing ClonePlan(PlanPricing p) => new()
    {
        Plan = p.Plan,
        MonthlyPriceToman = p.MonthlyPriceToman,
        MonthlyDownloadCount = p.MonthlyDownloadCount,
        MonthlyDownloadBytes = p.MonthlyDownloadBytes,
        MonthlySearchCount = p.MonthlySearchCount,
        MonthlyTicketLimit = p.MonthlyTicketLimit,
        MaxFileBytes = p.MaxFileBytes,
        ExtraDownloadCountPriceToman = p.ExtraDownloadCountPriceToman,
        ExtraDownloadCountPack = p.ExtraDownloadCountPack,
        ExtraDownloadBytesPriceToman = p.ExtraDownloadBytesPriceToman,
        ExtraDownloadBytesPack = p.ExtraDownloadBytesPack,
        FallbackOnSizeExceed = p.FallbackOnSizeExceed,
        FallbackOnBaleFailure = p.FallbackOnBaleFailure,
        FallbackLinkExpiryHours = p.FallbackLinkExpiryHours,
        UpdatedAt = p.UpdatedAt
    };
}
