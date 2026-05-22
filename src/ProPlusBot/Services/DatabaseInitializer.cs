using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services;

public class DatabaseInitializer(AppDbContext db, TrialSettingsService trialSettings)
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
                YouTubeEnabled = true,
                PinterestEnabled = true,
                SearchGridColumns = 3,
                SearchGridRows = 3,
                SearchGridJpegQuality = SearchGridPresets.DefaultJpegQuality,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await trialSettings.GetAsync(ct);

        await RemoveObsoleteDailyLimitsAsync(ct);

        if (!await db.PlanPlatformLimits.AnyAsync(ct))
            db.PlanPlatformLimits.AddRange(SubscriptionSeedData.DefaultLimits());
        else
        {
            await EnsureSearchLimitsAsync(ct);
            await EnsureMaxFileLimitsAsync(ct);
        }

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

        await AssignTrialExpiryToUsersWithoutAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task RemoveObsoleteDailyLimitsAsync(CancellationToken ct)
    {
        await db.PlanPlatformLimits
            .Where(l => l.Period == UsagePeriod.Daily && l.LimitKind != QuotaLimitKind.MaxFileBytes)
            .ExecuteDeleteAsync(ct);

        await db.UserPlanPlatformLimits
            .Where(l => l.Period == UsagePeriod.Daily && l.LimitKind != QuotaLimitKind.MaxFileBytes)
            .ExecuteDeleteAsync(ct);
    }

    private async Task EnsureSearchLimitsAsync(CancellationToken ct)
    {
        foreach (var seed in SubscriptionSeedData.DefaultSearchLimits())
        {
            var exists = await db.PlanPlatformLimits.AnyAsync(l =>
                l.Plan == seed.Plan
                && l.Platform == seed.Platform
                && l.Period == seed.Period
                && l.LimitKind == QuotaLimitKind.SearchCount, ct);

            if (!exists)
                db.PlanPlatformLimits.Add(seed);
        }
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

    private async Task AssignTrialExpiryToUsersWithoutAsync(CancellationToken ct)
    {
        var trialDays = (await trialSettings.GetAsync(ct)).DurationDays;
        var users = await db.BotUsers
            .Where(u => u.Plan == SubscriptionPlan.Free && u.PlanExpiresAt == null)
            .ToListAsync(ct);

        var expiresAt = DateTime.UtcNow.AddDays(trialDays);
        foreach (var user in users)
        {
            user.PlanExpiresAt = expiresAt;
            user.QuotaPeriodStartAt ??= user.CreatedAt;
            user.UpdatedAt = DateTime.UtcNow;
        }
    }
}
