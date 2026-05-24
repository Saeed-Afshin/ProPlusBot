using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;

namespace ProPlusBot.Services.Messaging;

public sealed record BaleUserSyncResult(int Updated, int Failed, int Skipped, IReadOnlyList<string> Errors);

public class BaleUserProfileSyncService(
    AppDbContext db,
    BaleBotClientFactory clientFactory,
    TrialSettingsService trialSettings,
    ILogger<BaleUserProfileSyncService> logger)
{
    public async Task<BaleUserSyncResult> SyncUsersAsync(
        IReadOnlyList<long> telegramUserIds,
        CancellationToken ct = default)
    {
        if (telegramUserIds.Count == 0)
            return new BaleUserSyncResult(0, 0, 0, ["هیچ کاربری انتخاب نشده است."]);

        var updated = 0;
        var failed = 0;
        var skipped = 0;
        var errors = new List<string>();
        var bot = clientFactory.CreateClient();

        foreach (var id in telegramUserIds.Distinct())
        {
            try
            {
                var chat = await bot.GetChat(id, ct);
                var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == id, ct);
                if (user is null)
                {
                    var trialDays = (await trialSettings.GetAsync(ct)).DurationDays;
                    user = new BotUser
                    {
                        TelegramUserId = id,
                        Plan = SubscriptionPlan.Free,
                        PlanExpiresAt = DateTime.UtcNow.AddDays(trialDays),
                        QuotaPeriodStartAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.BotUsers.Add(user);
                }

                user.Username = chat.Username;
                user.FirstName = chat.FirstName;
                user.LastName = chat.LastName;
                user.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                updated++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Bale profile sync failed for user {UserId}", id);
                failed++;
                if (errors.Count < 10)
                    errors.Add($"همگام‌سازی {id}: {ex.Message}");
            }

            await Task.Delay(35, ct);
        }

        return new BaleUserSyncResult(updated, failed, skipped, errors);
    }

    public async Task<BaleUserSyncResult> SyncAllAsync(CancellationToken ct = default)
    {
        var ids = await GetAllUserIdsAsync(ct);
        return await SyncUsersAsync(ids, ct);
    }

    public async Task<IReadOnlyList<long>> GetAllUserIdsAsync(CancellationToken ct = default) =>
        await db.BotUsers.AsNoTracking()
            .Select(u => u.TelegramUserId)
            .ToListAsync(ct);
}
