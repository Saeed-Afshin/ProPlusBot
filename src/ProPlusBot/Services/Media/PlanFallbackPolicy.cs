using ProPlusBot.Entities;

namespace ProPlusBot.Services.Media;

public sealed record PlanFallbackPolicy(
    bool OnSizeExceed,
    bool OnBaleFailure,
    int ExpiryHours)
{
    public static PlanFallbackPolicy From(PlanPricing plan) => new(
        plan.FallbackOnSizeExceed,
        plan.FallbackOnBaleFailure,
        UploadFallbackPresets.NormalizeExpiryHours(plan.FallbackLinkExpiryHours));

    public static PlanFallbackPolicy Disabled { get; } = new(false, false, UploadFallbackPresets.DefaultExpiryHours);

    public bool Allows(UploadFallbackTrigger trigger) =>
        trigger switch
        {
            UploadFallbackTrigger.SizeExceed => OnSizeExceed,
            UploadFallbackTrigger.BaleFailure => OnBaleFailure,
            _ => false
        };

    public bool AllowsAny() => OnSizeExceed || OnBaleFailure;
}
