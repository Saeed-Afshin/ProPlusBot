using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record QuotaUsageDto(
    MediaPlatformKind Platform,
    long DailyCountUsed,
    long DailyCountLimit,
    long MonthlyCountUsed,
    long MonthlyCountLimit,
    long DailyBytesUsed,
    long DailyBytesLimit,
    long MonthlyBytesUsed,
    long MonthlyBytesLimit,
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
