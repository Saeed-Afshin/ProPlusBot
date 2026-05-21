using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

internal static class SubscriptionSeedData
{
    public static IReadOnlyList<PlanPlatformLimit> DefaultLimits() =>
    [
        ..DefaultDownloadLimits(),
        ..DefaultMaxFileLimits(),
    ];

    private static IEnumerable<PlanPlatformLimit> DefaultDownloadLimits() =>
    [
        // Free
        Limit(SubscriptionPlan.Free, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 3),
        Limit(SubscriptionPlan.Free, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 50 * 1024 * 1024),
        Limit(SubscriptionPlan.Free, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 30),
        Limit(SubscriptionPlan.Free, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 500 * 1024 * 1024),
        Limit(SubscriptionPlan.Free, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 3),
        Limit(SubscriptionPlan.Free, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 50 * 1024 * 1024),
        Limit(SubscriptionPlan.Free, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 30),
        Limit(SubscriptionPlan.Free, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 500 * 1024 * 1024),

        // Bronze
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 10),
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 200 * 1024 * 1024),
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 150),
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 2L * 1024 * 1024 * 1024),
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 10),
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 200 * 1024 * 1024),
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 150),
        Limit(SubscriptionPlan.Bronze, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 2L * 1024 * 1024 * 1024),

        // Silver
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 25),
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 500 * 1024 * 1024),
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 400),
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 5L * 1024 * 1024 * 1024),
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 25),
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 500 * 1024 * 1024),
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 400),
        Limit(SubscriptionPlan.Silver, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 5L * 1024 * 1024 * 1024),

        // Golden
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 50),
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.YouTube, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 1024L * 1024 * 1024),
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 1000),
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.YouTube, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 15L * 1024 * 1024 * 1024),
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadCount, 50),
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.Pinterest, UsagePeriod.Daily, QuotaLimitKind.DownloadBytes, 1024L * 1024 * 1024),
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount, 1000),
        Limit(SubscriptionPlan.Golden, MediaPlatformKind.Pinterest, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes, 15L * 1024 * 1024 * 1024),
    ];

    public static IEnumerable<PlanPlatformLimit> DefaultMaxFileLimits() =>
    [
        MaxFile(SubscriptionPlan.Free, MediaPlatformKind.YouTube, 25),
        MaxFile(SubscriptionPlan.Free, MediaPlatformKind.Pinterest, 15),
        MaxFile(SubscriptionPlan.Bronze, MediaPlatformKind.YouTube, 40),
        MaxFile(SubscriptionPlan.Bronze, MediaPlatformKind.Pinterest, 25),
        MaxFile(SubscriptionPlan.Silver, MediaPlatformKind.YouTube, 49),
        MaxFile(SubscriptionPlan.Silver, MediaPlatformKind.Pinterest, 40),
        MaxFile(SubscriptionPlan.Golden, MediaPlatformKind.YouTube, 49),
        MaxFile(SubscriptionPlan.Golden, MediaPlatformKind.Pinterest, 49),
    ];

    public static IReadOnlyList<PlanPricing> DefaultPricing() =>
    [
        new() { Plan = SubscriptionPlan.Free, MonthlyPriceToman = 0, UpdatedAt = DateTime.UtcNow },
        new() { Plan = SubscriptionPlan.Bronze, MonthlyPriceToman = 99_000, UpdatedAt = DateTime.UtcNow },
        new() { Plan = SubscriptionPlan.Silver, MonthlyPriceToman = 199_000, UpdatedAt = DateTime.UtcNow },
        new() { Plan = SubscriptionPlan.Golden, MonthlyPriceToman = 399_000, UpdatedAt = DateTime.UtcNow },
    ];

    private static PlanPlatformLimit Limit(
        SubscriptionPlan plan,
        MediaPlatformKind platform,
        UsagePeriod period,
        QuotaLimitKind kind,
        long value) =>
        new()
        {
            Plan = plan,
            Platform = platform,
            Period = period,
            LimitKind = kind,
            LimitValue = value,
            UpdatedAt = DateTime.UtcNow
        };

    private static PlanPlatformLimit MaxFile(SubscriptionPlan plan, MediaPlatformKind platform, int megabytes) =>
        Limit(plan, platform, UsagePeriod.Daily, QuotaLimitKind.MaxFileBytes, megabytes * 1024L * 1024);
}
