using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record PlanLimitCell(int LimitId, decimal DisplayValue);

public record PlanLimitMatrixRow(
    MediaPlatformKind Platform,
    UsagePeriod Period,
    QuotaLimitKind LimitKind,
    IReadOnlyDictionary<SubscriptionPlan, PlanLimitCell> Cells);
