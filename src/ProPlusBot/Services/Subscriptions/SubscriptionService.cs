using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public class SubscriptionService(
    AppDbContext db,
    PlanLifecycleService planLifecycle,
    IOptions<PaymentOptions> paymentOptions)
{
    private readonly PaymentOptions _paymentOptions = paymentOptions.Value;

    public bool CanUserUpgradeTo(SubscriptionPlan current, SubscriptionPlan target) =>
        PlanLifecycleService.CanUpgradeTo(current, target);

    public async Task<long> GetUpgradePriceDiffAsync(
        long telegramUserId,
        SubscriptionPlan targetPlan,
        CancellationToken ct = default)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (!CanUserUpgradeTo(user.Plan, targetPlan))
            throw new InvalidOperationException("ارتقا به این بسته مجاز نیست.");

        var prices = await db.PlanPricings.AsNoTracking()
            .ToDictionaryAsync(p => p.Plan, p => p.MonthlyPriceToman, ct);
        var currentPrice = prices.GetValueOrDefault(user.Plan, 0);
        var targetPrice = prices.GetValueOrDefault(targetPlan, 0);
        return Math.Max(0, targetPrice - currentPrice);
    }

    public async Task<long> GetPlanPurchasePriceAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        var row = await db.PlanPricings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Plan == plan, ct)
            ?? throw new InvalidOperationException("قیمت بسته یافت نشد.");

        if (row.MonthlyPriceToman <= 0)
            throw new InvalidOperationException("این بسته قابل خرید نیست.");

        return row.MonthlyPriceToman;
    }

    public async Task<PaymentRecord> CreateUpgradePaymentAsync(
        long telegramUserId,
        SubscriptionPlan targetPlan,
        CancellationToken ct = default)
    {
        await ExpireStalePendingPaymentsAsync(telegramUserId, ct);

        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var amountToman = await GetUpgradePriceDiffAsync(telegramUserId, targetPlan, ct);
        if (amountToman <= 0)
            throw new InvalidOperationException("مبلغ پرداخت نامعتبر است.");

        var payment = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            TelegramUserId = telegramUserId,
            Type = PaymentType.PlanUpgrade,
            Status = PaymentStatus.Pending,
            Currency = _paymentOptions.Currency,
            Payload = $"upgrade:{targetPlan}",
            FromPlan = user.Plan,
            ToPlan = targetPlan,
            CreatedAt = DateTime.UtcNow
        };
        SetPaymentAmounts(payment, amountToman);

        db.PaymentRecords.Add(payment);
        await db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task<PaymentRecord> CreatePlanPurchasePaymentAsync(
        long telegramUserId,
        SubscriptionPlan targetPlan,
        CancellationToken ct = default)
    {
        await ExpireStalePendingPaymentsAsync(telegramUserId, ct);

        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (targetPlan == SubscriptionPlan.Free)
            throw new InvalidOperationException("بسته آزمایشی قابل خرید نیست.");

        var amountToman = await GetPlanPurchasePriceAsync(targetPlan, ct);

        var payment = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            TelegramUserId = telegramUserId,
            Type = PaymentType.PlanPurchase,
            Status = PaymentStatus.Pending,
            Currency = _paymentOptions.Currency,
            Payload = $"purchase:{targetPlan}",
            FromPlan = user.Plan,
            ToPlan = targetPlan,
            CreatedAt = DateTime.UtcNow
        };
        SetPaymentAmounts(payment, amountToman);

        db.PaymentRecords.Add(payment);
        await db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task<PaymentRecord> CreateExtraQuotaPaymentAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        CancellationToken ct = default)
    {
        await ExpireStalePendingPaymentsAsync(telegramUserId, ct);

        var pack = await db.ExtraQuotaPackSettings.AsNoTracking().FirstAsync(ct);
        if (pack.PriceToman <= 0)
            throw new InvalidOperationException("قیمت سهمیه اضافه تنظیم نشده است.");

        var payment = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            TelegramUserId = telegramUserId,
            Type = PaymentType.ExtraQuota,
            Status = PaymentStatus.Pending,
            Currency = _paymentOptions.Currency,
            Payload = $"extra:{platform}",
            Platform = platform,
            CreatedAt = DateTime.UtcNow
        };
        SetPaymentAmounts(payment, pack.PriceToman);

        db.PaymentRecords.Add(payment);
        await db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task ExpireStalePendingPaymentsAsync(long? telegramUserId = null, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-_paymentOptions.PendingPaymentExpiryHours);
        var query = db.PaymentRecords.Where(p =>
            p.Status == PaymentStatus.Pending && p.CreatedAt < cutoff);

        if (telegramUserId.HasValue)
            query = query.Where(p => p.TelegramUserId == telegramUserId.Value);

        var expired = await query.ToListAsync(ct);
        if (expired.Count == 0)
            return;

        foreach (var payment in expired)
        {
            payment.Status = PaymentStatus.Cancelled;
            payment.Note = $"منقضی شده پس از {_paymentOptions.PendingPaymentExpiryHours} ساعت";
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<PlanFulfillmentResult?> CompletePaymentAsync(
        PaymentRecord payment,
        string? providerChargeId,
        string? telegramChargeId,
        CancellationToken ct = default)
    {
        if (payment.Status == PaymentStatus.Completed)
            return null;

        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = DateTime.UtcNow;
        payment.ProviderPaymentChargeId = providerChargeId;
        payment.ProviderTelegramPaymentChargeId = telegramChargeId;

        var user = await db.BotUsers.FirstAsync(u => u.TelegramUserId == payment.TelegramUserId, ct);

        if (payment.Type is PaymentType.PlanUpgrade or PaymentType.PlanPurchase && payment.ToPlan is not null)
        {
            var isUpgrade = payment.Type == PaymentType.PlanUpgrade;
            if (isUpgrade && !CanUserUpgradeTo(user.Plan, payment.ToPlan.Value))
                throw new InvalidOperationException("ارتقای بسته برای این پرداخت معتبر نیست.");

            var result = await planLifecycle.FulfillPlanPaymentAsync(user, payment.ToPlan.Value, isUpgrade, ct);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return result;
        }

        if (payment.Type == PaymentType.ExtraQuota && payment.Platform is not null)
        {
            var pack = await db.ExtraQuotaPackSettings.AsNoTracking().FirstAsync(ct);
            var adjustment = await db.UserQuotaAdjustments
                .FirstOrDefaultAsync(a => a.TelegramUserId == payment.TelegramUserId && a.Platform == payment.Platform, ct);

            if (adjustment is null)
            {
                adjustment = new UserQuotaAdjustment
                {
                    TelegramUserId = payment.TelegramUserId,
                    Platform = payment.Platform.Value
                };
                db.UserQuotaAdjustments.Add(adjustment);
            }

            adjustment.ExtraDownloadCount += pack.ExtraDownloadCount;
            adjustment.ExtraDownloadBytes += pack.ExtraDownloadBytes;
            adjustment.UpdatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return null;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task ApplyAdminPlanChangeAsync(
        long telegramUserId,
        SubscriptionPlan plan,
        DateTime? expiresAt,
        CancellationToken ct = default)
    {
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.Plan = plan;
        user.PlanExpiresAt = plan == SubscriptionPlan.Free
            ? expiresAt
            : expiresAt ?? DateTime.UtcNow.AddDays(_paymentOptions.PlanDurationDays);

        if (user.PlanExpiresAt is not null && user.PlanExpiresAt > DateTime.UtcNow)
            user.QuotaPeriodStartAt = DateTime.UtcNow;
        else
            user.QuotaPeriodStartAt = null;

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ExtendPlanAsync(long telegramUserId, int extraDays, CancellationToken ct = default)
    {
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (user.Plan == SubscriptionPlan.Free)
            throw new InvalidOperationException("بسته آزمایشی از پنل ادمین قابل تمدید است.");

        var baseDate = user.PlanExpiresAt is null || user.PlanExpiresAt < DateTime.UtcNow
            ? DateTime.UtcNow
            : user.PlanExpiresAt.Value;

        user.PlanExpiresAt = baseDate.AddDays(extraDays);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetBanAsync(long telegramUserId, bool banned, CancellationToken ct = default)
    {
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.IsBanned = banned;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task AdjustQuotaAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        int extraCountDelta,
        long extraBytesDelta,
        CancellationToken ct = default)
    {
        var adjustment = await db.UserQuotaAdjustments
            .FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId && a.Platform == platform, ct);

        if (adjustment is null)
        {
            adjustment = new UserQuotaAdjustment
            {
                TelegramUserId = telegramUserId,
                Platform = platform
            };
            db.UserQuotaAdjustments.Add(adjustment);
        }

        adjustment.ExtraDownloadCount = Math.Max(0, adjustment.ExtraDownloadCount + extraCountDelta);
        adjustment.ExtraDownloadBytes = Math.Max(0, adjustment.ExtraDownloadBytes + extraBytesDelta);
        adjustment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static void SetPaymentAmounts(PaymentRecord payment, long toman)
    {
        payment.AmountToman = toman;
        payment.AmountRials = TomanCurrency.ToRials(toman);
    }
}
