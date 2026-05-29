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
                ConversationStateBackend = ConversationStateBackend.Memory,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await trialSettings.GetAsync(ct);
        await EnsurePlanDefinitionsAsync(ct);
        await AssignTrialExpiryToUsersWithoutAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsurePlanDefinitionsAsync(CancellationToken ct)
    {
        foreach (var seed in SubscriptionSeedData.DefaultPlanDefinitions())
        {
            var row = await db.PlanPricings.FirstOrDefaultAsync(p => p.Plan == seed.Plan, ct);
            if (row is null)
            {
                db.PlanPricings.Add(seed);
                continue;
            }

            if (row.MonthlyDownloadCount == 0 && row.MonthlyDownloadBytes == 0 && row.MonthlySearchCount == 0)
            {
                row.MonthlyDownloadCount = seed.MonthlyDownloadCount;
                row.MonthlyDownloadBytes = seed.MonthlyDownloadBytes;
                row.MonthlySearchCount = seed.MonthlySearchCount;
                row.MonthlyTicketLimit = seed.MonthlyTicketLimit;
                row.MaxFileBytes = seed.MaxFileBytes;
                row.ExtraDownloadCountPriceToman = seed.ExtraDownloadCountPriceToman;
                row.ExtraDownloadCountPack = seed.ExtraDownloadCountPack;
                row.ExtraDownloadBytesPriceToman = seed.ExtraDownloadBytesPriceToman;
                row.ExtraDownloadBytesPack = seed.ExtraDownloadBytesPack;
                row.UpdatedAt = DateTime.UtcNow;
            }
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
