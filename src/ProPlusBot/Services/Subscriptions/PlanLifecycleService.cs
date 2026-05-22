using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public enum PlanFulfillmentResult
{
    Activated,
    Upgraded,
    Reserved
}

public class PlanLifecycleService(AppDbContext db, IOptions<PaymentOptions> paymentOptions)
{
    private readonly PaymentOptions _paymentOptions = paymentOptions.Value;

    public static bool IsPaidPlanActive(BotUser user) =>
        user.Plan != SubscriptionPlan.Free
        && user.PlanExpiresAt is not null
        && user.PlanExpiresAt > DateTime.UtcNow;

    public static bool IsTrialActive(BotUser user) =>
        user.Plan == SubscriptionPlan.Free
        && user.PlanExpiresAt is not null
        && user.PlanExpiresAt > DateTime.UtcNow;

    public static bool HasSubscriptionAccess(BotUser user) =>
        IsPaidPlanActive(user) || IsTrialActive(user);

    /// <summary>Legacy alias for paid plans only.</summary>
    public static bool IsPlanActive(BotUser user) => IsPaidPlanActive(user);

    public async Task EnsurePlanStateCurrentAsync(long telegramUserId, CancellationToken ct = default)
    {
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        if (user is null || IsPaidPlanActive(user) || user.Plan == SubscriptionPlan.Free)
            return;

        await ActivateNextReservedOrSetFreeAsync(user, ct);
    }

    public async Task<PlanFulfillmentResult> FulfillPlanPaymentAsync(
        BotUser user,
        SubscriptionPlan targetPlan,
        bool isUpgradePayment,
        CancellationToken ct = default)
    {
        if (!IsPaidPlanActive(user))
        {
            await ActivatePlanAsync(user, targetPlan, _paymentOptions.PlanDurationDays, ct);
            return PlanFulfillmentResult.Activated;
        }

        if (isUpgradePayment && CanUpgradeTo(user.Plan, targetPlan))
        {
            await ActivatePlanAsync(user, targetPlan, _paymentOptions.PlanDurationDays, ct);
            return PlanFulfillmentResult.Upgraded;
        }

        await ReservePlanAsync(user.TelegramUserId, targetPlan, _paymentOptions.PlanDurationDays, ct);
        return PlanFulfillmentResult.Reserved;
    }

    public async Task ReservePlanAsync(
        long telegramUserId,
        SubscriptionPlan plan,
        int durationDays,
        CancellationToken ct = default)
    {
        if (plan == SubscriptionPlan.Free)
            throw new InvalidOperationException("بسته آزمایشی قابل رزرو نیست.");

        db.UserReservedPlans.Add(new UserReservedPlan
        {
            Id = Guid.NewGuid(),
            TelegramUserId = telegramUserId,
            Plan = plan,
            DurationDays = durationDays,
            CreatedAt = DateTime.UtcNow
        });

        var user = await db.BotUsers.FirstAsync(u => u.TelegramUserId == telegramUserId, ct);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ActivateReservedPlanAsync(
        long telegramUserId,
        Guid reservationId,
        CancellationToken ct = default)
    {
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var reserved = await db.UserReservedPlans
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.TelegramUserId == telegramUserId, ct);

        if (reserved is null)
            return false;

        db.UserReservedPlans.Remove(reserved);
        await ActivatePlanAsync(user, reserved.Plan, reserved.DurationDays, ct);
        return true;
    }

    public async Task<List<UserReservedPlan>> GetReservedPlansAsync(long telegramUserId, CancellationToken ct = default) =>
        await db.UserReservedPlans.AsNoTracking()
            .Where(r => r.TelegramUserId == telegramUserId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public static bool CanUpgradeTo(SubscriptionPlan current, SubscriptionPlan target) =>
        (int)target > (int)current;

    private async Task ActivatePlanAsync(
        BotUser user,
        SubscriptionPlan plan,
        int durationDays,
        CancellationToken ct)
    {
        user.Plan = plan;
        user.PlanExpiresAt = DateTime.UtcNow.AddDays(durationDays);
        user.QuotaPeriodStartAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task ActivateNextReservedOrSetFreeAsync(BotUser user, CancellationToken ct)
    {
        var next = await db.UserReservedPlans
            .Where(r => r.TelegramUserId == user.TelegramUserId)
            .OrderBy(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (next is not null)
        {
            db.UserReservedPlans.Remove(next);
            await ActivatePlanAsync(user, next.Plan, next.DurationDays, ct);
            return;
        }

        user.Plan = SubscriptionPlan.Free;
        user.PlanExpiresAt = null;
        user.QuotaPeriodStartAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
