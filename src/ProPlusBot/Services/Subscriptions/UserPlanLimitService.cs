using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class UserPlanLimitService(AppDbContext db)
{
    public async Task<IReadOnlyList<UserLimitEditRow>> GetUserLimitRowsAsync(
        long telegramUserId,
        SubscriptionPlan effectivePlan,
        CancellationToken ct = default)
    {
        var planLimits = await db.PlanPlatformLimits.AsNoTracking()
            .Where(l => l.Plan == effectivePlan)
            .ToListAsync(ct);

        var userLimits = await db.UserPlanPlatformLimits.AsNoTracking()
            .Where(l => l.TelegramUserId == telegramUserId)
            .ToListAsync(ct);

        var userByKey = userLimits.ToDictionary(l => (l.Platform, l.Period, l.LimitKind));

        return planLimits
            .Select(l =>
            {
                var key = (l.Platform, l.Period, l.LimitKind);
                var hasCustom = userByKey.ContainsKey(key);
                var value = hasCustom ? userByKey[key].LimitValue : l.LimitValue;
                return new UserLimitEditRow(
                    l.Platform,
                    l.Period,
                    l.LimitKind,
                    PlanLimitDisplay.ToDisplayValue(l.LimitKind, value),
                    hasCustom);
            })
            .OrderBy(r => r.Platform)
            .ThenBy(r => r.LimitKind)
            .ThenBy(r => r.Period)
            .ToList();
    }

    public async Task SaveUserLimitRowAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        UsagePeriod period,
        QuotaLimitKind limitKind,
        decimal displayValue,
        CancellationToken ct = default)
    {
        var limitValue = ByteUnits.IsByteLimitKind(limitKind)
            ? ByteUnits.FromMegabytes(displayValue)
            : Math.Max(0, (long)displayValue);

        var row = await db.UserPlanPlatformLimits
            .FirstOrDefaultAsync(l =>
                l.TelegramUserId == telegramUserId
                && l.Platform == platform
                && l.Period == period
                && l.LimitKind == limitKind, ct);

        if (row is null)
        {
            row = new UserPlanPlatformLimit
            {
                TelegramUserId = telegramUserId,
                Platform = platform,
                Period = period,
                LimitKind = limitKind
            };
            db.UserPlanPlatformLimits.Add(row);
        }

        row.LimitValue = limitValue;
        row.UpdatedAt = DateTime.UtcNow;
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

    public async Task<IReadOnlyList<PlanPlatformLimit>> ResolveLimitsAsync(
        long telegramUserId,
        SubscriptionPlan plan,
        MediaPlatformKind platform,
        CancellationToken ct = default)
    {
        var planLimits = await db.PlanPlatformLimits.AsNoTracking()
            .Where(l => l.Plan == plan && l.Platform == platform)
            .ToListAsync(ct);

        var userLimits = await db.UserPlanPlatformLimits.AsNoTracking()
            .Where(l => l.TelegramUserId == telegramUserId && l.Platform == platform)
            .ToListAsync(ct);

        if (userLimits.Count == 0)
            return planLimits;

        var userByKey = userLimits.ToDictionary(l => (l.Period, l.LimitKind));
        return planLimits
            .Select(l =>
            {
                if (!userByKey.TryGetValue((l.Period, l.LimitKind), out var userLimit))
                    return l;

                return new PlanPlatformLimit
                {
                    Id = l.Id,
                    Plan = l.Plan,
                    Platform = l.Platform,
                    Period = l.Period,
                    LimitKind = l.LimitKind,
                    LimitValue = userLimit.LimitValue,
                    UpdatedAt = userLimit.UpdatedAt
                };
            })
            .ToList();
    }
}
