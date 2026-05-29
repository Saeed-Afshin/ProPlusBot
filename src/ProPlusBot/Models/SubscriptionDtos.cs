using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record SharedQuotaUsageDto(
    long MonthlyDownloadCountUsed,
    long MonthlyDownloadCountLimit,
    long MonthlyBytesUsed,
    long MonthlyBytesLimit,
    long MonthlySearchUsed,
    long MonthlySearchLimit,
    int ExtraDownloadCountBonus,
    long ExtraDownloadBytesBonus,
    int ExtraSearchCountBonus);

public record ReservedPlanDto(Guid Id, SubscriptionPlan Plan, int DurationDays, DateTime CreatedAt);

public record UserAccountSummaryDto(
    long TelegramUserId,
    SubscriptionPlan EffectivePlan,
    SubscriptionPlan StoredPlan,
    DateTime? PlanExpiresAt,
    DateTime QuotaPeriodStartAt,
    bool IsBanned,
    bool HasSubscriptionAccess,
    bool IsTrialActive,
    SharedQuotaUsageDto SharedQuota,
    long MaxFileBytesLimit,
    IReadOnlyList<ReservedPlanDto> ReservedPlans);

public record PaymentRecordDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? DisplayName,
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

public record PlanDefinitionDto(
    SubscriptionPlan Plan,
    long MonthlyPriceToman,
    long MonthlyDownloadCount,
    decimal MonthlyDownloadMegabytes,
    long MonthlySearchCount,
    int MonthlyTicketLimit,
    decimal MaxFileMegabytes,
    long ExtraDownloadPackPriceToman,
    int ExtraDownloadCountPack,
    decimal ExtraDownloadPackMegabytes,
    bool FallbackOnSizeExceed,
    bool FallbackOnBaleFailure,
    int FallbackLinkExpiryHours);

public record TrialSettingsDto(int DurationDays);

public record BotUserAdminDto(
    long TelegramUserId,
    string? Username,
    string? DisplayName,
    string? PhoneNumber,
    SubscriptionPlan Plan,
    DateTime? PlanExpiresAt,
    bool IsBanned,
    DateTime CreatedAt);
