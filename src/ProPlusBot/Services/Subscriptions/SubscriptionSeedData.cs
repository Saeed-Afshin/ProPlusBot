using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

internal static class SubscriptionSeedData
{
    public static IReadOnlyList<PlanPricing> DefaultPlanDefinitions() =>
    [
        Plan(SubscriptionPlan.Free,
            price: 0,
            downloads: 60,
            downloadMb: 1000,
            searches: 40,
            tickets: 2,
            maxFileYtMb: 25,
            maxFilePinMb: 15,
            extraCountPrice: 15_000,
            extraCountPack: 5,
            extraBytesPrice: 15_000,
            extraBytesMb: 200),
        Plan(SubscriptionPlan.Bronze,
            price: 99_000,
            downloads: 300,
            downloadMb: 4096,
            searches: 400,
            tickets: 5,
            maxFileYtMb: 40,
            maxFilePinMb: 25,
            extraCountPrice: 25_000,
            extraCountPack: 10,
            extraBytesPrice: 25_000,
            extraBytesMb: 500),
        Plan(SubscriptionPlan.Silver,
            price: 199_000,
            downloads: 800,
            downloadMb: 10240,
            searches: 1000,
            tickets: 10,
            maxFileYtMb: 49,
            maxFilePinMb: 40,
            extraCountPrice: 35_000,
            extraCountPack: 15,
            extraBytesPrice: 35_000,
            extraBytesMb: 1024),
        Plan(SubscriptionPlan.Golden,
            price: 399_000,
            downloads: 2000,
            downloadMb: 30720,
            searches: 4000,
            tickets: 20,
            maxFileYtMb: 49,
            maxFilePinMb: 49,
            extraCountPrice: 49_000,
            extraCountPack: 25,
            extraBytesPrice: 49_000,
            extraBytesMb: 2048),
    ];

    private static PlanPricing Plan(
        SubscriptionPlan plan,
        long price,
        long downloads,
        int downloadMb,
        long searches,
        int tickets,
        int maxFileYtMb,
        int maxFilePinMb,
        long extraCountPrice,
        int extraCountPack,
        long extraBytesPrice,
        int extraBytesMb) =>
        new()
        {
            Plan = plan,
            MonthlyPriceToman = price,
            MonthlyDownloadCount = downloads,
            MonthlyDownloadBytes = downloadMb * 1024L * 1024,
            MonthlySearchCount = searches,
            MonthlyTicketLimit = tickets,
            MaxFileBytesYouTube = maxFileYtMb * 1024L * 1024,
            MaxFileBytesPinterest = maxFilePinMb * 1024L * 1024,
            ExtraDownloadCountPriceToman = extraCountPrice,
            ExtraDownloadCountPack = extraCountPack,
            ExtraDownloadBytesPriceToman = extraBytesPrice,
            ExtraDownloadBytesPack = extraBytesMb * 1024L * 1024,
            UpdatedAt = DateTime.UtcNow
        };
}
