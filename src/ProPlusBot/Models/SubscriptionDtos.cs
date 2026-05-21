using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record QuotaUsageDto(
    MediaPlatformKind Platform,
    long MonthlyDownloadCountUsed,
    long MonthlyDownloadCountLimit,
    long MonthlyBytesUsed,
    long MonthlyBytesLimit,
    long MonthlySearchUsed,
    long MonthlySearchLimit,
    long MaxFileBytesLimit,
    int ExtraCountRemaining,
    long ExtraBytesRemaining);

public record ReservedPlanDto(Guid Id, SubscriptionPlan Plan, int DurationDays, DateTime CreatedAt);

public record UserAccountSummaryDto(
    long TelegramUserId,
    SubscriptionPlan EffectivePlan,
    SubscriptionPlan StoredPlan,
    DateTime? PlanExpiresAt,
    bool IsBanned,
    bool HasSubscriptionAccess,
    bool IsTrialActive,
    IReadOnlyList<QuotaUsageDto> Quotas,
    IReadOnlyList<ReservedPlanDto> ReservedPlans);

public record PaymentRecordDto(
    Guid Id,
    long TelegramUserId,
    string? PhoneNumber,
    PaymentType Type,
    PaymentStatus Status,
    long AmountToman,
    SubscriptionPlan? FromPlan,
    SubscriptionPlan? ToPlan,
    MediaPlatformKind? Platform,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? Note);

public record PlanLimitDto(
    int Id,
    SubscriptionPlan Plan,
    MediaPlatformKind Platform,
    UsagePeriod Period,
    QuotaLimitKind LimitKind,
    long LimitValue);

public record PlanPricingDto(SubscriptionPlan Plan, long MonthlyPriceToman);

public record BotUserAdminDto(
    long TelegramUserId,
    string? Username,
    string? PhoneNumber,
    SubscriptionPlan Plan,
    DateTime? PlanExpiresAt,
    bool IsBanned,
    DateTime CreatedAt);

public record TrialSettingsDto(int DurationDays);
