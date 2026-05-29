using ProPlusBot.Entities;

namespace ProPlusBot.Services.Media;

public sealed class UploadFallbackConfigSnapshot
{
    public required long BaleDirectArvanThresholdBytes { get; init; }

    public required IReadOnlyDictionary<SubscriptionPlan, PlanFallbackPolicy> Plans { get; init; }

    public PlanFallbackPolicy GetPlan(SubscriptionPlan plan) =>
        Plans.TryGetValue(plan, out var policy) ? policy : PlanFallbackPolicy.Disabled;
}
