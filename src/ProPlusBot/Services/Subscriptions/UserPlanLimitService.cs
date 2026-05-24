using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class UserPlanLimitService(AppDbContext db, PlanDefinitionService planDefinitions)
{
    public async Task<IReadOnlyList<UserLimitEditRow>> GetUserLimitRowsAsync(
        long telegramUserId,
        SubscriptionPlan effectivePlan,
        CancellationToken ct = default)
    {
        var plan = await planDefinitions.GetPlanAsync(effectivePlan, ct);
        var planMb = ByteUnits.ToMegabytes(PlanExtraPackHelper.GetUnifiedMaxFileBytes(plan));

        var overrides = await db.UserPlanPlatformLimits.AsNoTracking()
            .Where(l => l.TelegramUserId == telegramUserId && l.LimitKind == QuotaLimitKind.MaxFileBytes)
            .ToListAsync(ct);

        var hasCustom = overrides.Count > 0;
        var value = hasCustom
            ? ByteUnits.ToMegabytes(overrides.Max(l => l.LimitValue))
            : planMb;

        return [new UserLimitEditRow(value, hasCustom)];
    }

    public async Task SaveUserMaxFileAsync(
        long telegramUserId,
        decimal megabytes,
        CancellationToken ct = default)
    {
        var limitValue = ByteUnits.FromMegabytes(megabytes);

        foreach (var platform in Enum.GetValues<MediaPlatformKind>())
        {
            var row = await db.UserPlanPlatformLimits
                .FirstOrDefaultAsync(l =>
                    l.TelegramUserId == telegramUserId
                    && l.Platform == platform
                    && l.LimitKind == QuotaLimitKind.MaxFileBytes, ct);

            if (row is null)
            {
                row = new UserPlanPlatformLimit
                {
                    TelegramUserId = telegramUserId,
                    Platform = platform,
                    Period = UsagePeriod.Daily,
                    LimitKind = QuotaLimitKind.MaxFileBytes
                };
                db.UserPlanPlatformLimits.Add(row);
            }

            row.LimitValue = limitValue;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ClearUserLimitsAsync(long telegramUserId, CancellationToken ct = default)
    {
        var rows = await db.UserPlanPlatformLimits
            .Where(l => l.TelegramUserId == telegramUserId)
            .ToListAsync(ct);

        db.UserPlanPlatformLimits.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
