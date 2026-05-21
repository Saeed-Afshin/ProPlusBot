using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services;

public class DatabaseInitializer(AppDbContext db)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.BotSettings.AnyAsync(ct))
        {
            db.BotSettings.Add(new BotSetting
            {
                Id = 1,
                Mode = BotMode.Live,
                UpdateMode = BotUpdateMode.LongPolling,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (!await db.PlanPlatformLimits.AnyAsync(ct))
            db.PlanPlatformLimits.AddRange(SubscriptionSeedData.DefaultLimits());
        else
            await EnsureMaxFileLimitsAsync(ct);

        if (!await db.PlanPricings.AnyAsync(ct))
            db.PlanPricings.AddRange(SubscriptionSeedData.DefaultPricing());

        if (!await db.ExtraQuotaPackSettings.AnyAsync(ct))
        {
            db.ExtraQuotaPackSettings.Add(new ExtraQuotaPackSettings
            {
                Id = 1,
                PriceToman = 29_000,
                ExtraDownloadCount = 5,
                ExtraDownloadBytes = 200 * 1024 * 1024,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureMaxFileLimitsAsync(CancellationToken ct)
    {
        foreach (var seed in SubscriptionSeedData.DefaultMaxFileLimits())
        {
            var exists = await db.PlanPlatformLimits.AnyAsync(l =>
                l.Plan == seed.Plan
                && l.Platform == seed.Platform
                && l.LimitKind == QuotaLimitKind.MaxFileBytes, ct);

            if (!exists)
                db.PlanPlatformLimits.Add(seed);
        }
    }
}
