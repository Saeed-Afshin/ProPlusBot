using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class QuotaService(
    AppDbContext db,
    PlanLifecycleService planLifecycle,
    PlanDefinitionService planDefinitions)
{
    public async Task<SubscriptionPlan> GetEffectivePlanAsync(long telegramUserId, CancellationToken ct = default)
    {
        await planLifecycle.EnsurePlanStateCurrentAsync(telegramUserId, ct);

        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user is null)
            return SubscriptionPlan.Free;

        if (PlanLifecycleService.IsPaidPlanActive(user))
            return user.Plan;

        return SubscriptionPlan.Free;
    }

    public async Task<(bool Allowed, string? Message)> CanDownloadAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        CancellationToken ct = default)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user?.IsBanned == true)
            return (false, "حساب شما مسدود شده است. با پشتیبانی تماس بگیرید.");

        if (user is not null && !PlanLifecycleService.HasSubscriptionAccess(user))
            return (false, SubscriptionMessages.TrialExpired);

        var plan = await GetEffectivePlanAsync(telegramUserId, ct);
        var planRow = await planDefinitions.GetPlanAsync(plan, ct);
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var adjustments = await GetAdjustmentsAsync(telegramUserId, ct);
        var monthly = await GetTotalDownloadUsageAsync(telegramUserId, monthlyStart, ct);

        var maxFileBytes = await GetMaxFileBytesAsync(telegramUserId, ct);
        var countLimit = planRow.MonthlyDownloadCount;
        var bytesLimit = planRow.MonthlyDownloadBytes;
        var extraCount = adjustments.ExtraCount;
        var extraBytes = adjustments.ExtraBytes;

        if (countLimit > 0 && monthly.Count >= countLimit + extraCount)
            return (false, "سقف تعداد دانلود ماهانه تمام شده است.");

        if (bytesLimit > 0 && maxFileBytes < long.MaxValue
            && monthly.Bytes + maxFileBytes > bytesLimit + extraBytes)
            return (false, "سقف حجم دانلود ماهانه تمام شده است.");

        return (true, null);
    }

    public async Task<(bool Allowed, string? Message)> CanSearchAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        CancellationToken ct = default)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user?.IsBanned == true)
            return (false, "حساب شما مسدود شده است. با پشتیبانی تماس بگیرید.");

        if (user is not null && !PlanLifecycleService.HasSubscriptionAccess(user))
            return (false, SubscriptionMessages.TrialExpired);

        var plan = await GetEffectivePlanAsync(telegramUserId, ct);
        var planRow = await planDefinitions.GetPlanAsync(plan, ct);
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var searchUsed = await GetTotalSearchUsageAsync(telegramUserId, monthlyStart, ct);

        var adjustments = await GetAdjustmentsAsync(telegramUserId, ct);
        var searchCap = planRow.MonthlySearchCount + adjustments.ExtraSearch;
        if (searchCap > 0 && searchUsed >= searchCap)
            return (false, "سقف تعداد جستجوی ماهانه تمام شده است.");

        return (true, null);
    }

    public async Task RecordSearchAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        CancellationToken ct = default)
    {
        db.SearchUsageLogs.Add(new SearchUsageLog
        {
            TelegramUserId = telegramUserId,
            Platform = platform,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<long> GetMaxFileBytesAsync(long telegramUserId, CancellationToken ct = default)
    {
        var plan = await GetEffectivePlanAsync(telegramUserId, ct);
        var bytes = await planDefinitions.GetMaxFileBytesAsync(plan, ct);
        return bytes > 0 ? bytes : long.MaxValue;
    }

    public async Task<(bool Allowed, string? Message)> ValidateFileSizeAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user?.IsBanned == true)
            return (false, "حساب شما مسدود شده است. با پشتیبانی تماس بگیرید.");

        if (user is not null && !PlanLifecycleService.HasSubscriptionAccess(user))
            return (false, SubscriptionMessages.TrialExpired);

        var maxFileBytes = await GetMaxFileBytesAsync(telegramUserId, ct);
        if (maxFileBytes >= long.MaxValue || fileSizeBytes <= maxFileBytes)
            return (true, null);

        return (false,
            $"حداکثر حجم هر فایل برای بسته شما {ByteUnits.FormatVolume(maxFileBytes)} است.");
    }

    public async Task RecordDownloadAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        db.DownloadUsageLogs.Add(new DownloadUsageLog
        {
            TelegramUserId = telegramUserId,
            Platform = platform,
            FileSizeBytes = fileSizeBytes,
            CreatedAt = DateTime.UtcNow
        });

        await ConsumeExtraQuotaIfNeededAsync(telegramUserId, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task ConsumeExtraQuotaIfNeededAsync(long telegramUserId, CancellationToken ct)
    {
        var plan = await GetEffectivePlanAsync(telegramUserId, ct);
        var planRow = await planDefinitions.GetPlanAsync(plan, ct);
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var monthly = await GetTotalDownloadUsageAsync(telegramUserId, monthlyStart, ct);

        var adjustment = await db.UserQuotaAdjustments
            .FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId, ct);

        if (adjustment is null)
            return;

        if (planRow.MonthlyDownloadCount > 0 && monthly.Count > planRow.MonthlyDownloadCount && adjustment.ExtraDownloadCount > 0)
            adjustment.ExtraDownloadCount--;

        if (planRow.MonthlyDownloadBytes > 0 && monthly.Bytes > planRow.MonthlyDownloadBytes && adjustment.ExtraDownloadBytes > 0)
        {
            var over = monthly.Bytes - planRow.MonthlyDownloadBytes;
            adjustment.ExtraDownloadBytes = Math.Max(0, adjustment.ExtraDownloadBytes - over);
        }

        adjustment.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<UserAccountSummaryDto> GetAccountSummaryAsync(long telegramUserId, CancellationToken ct = default)
    {
        await planLifecycle.EnsurePlanStateCurrentAsync(telegramUserId, ct);

        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var effectivePlan = PlanLifecycleService.IsPaidPlanActive(user) ? user.Plan : SubscriptionPlan.Free;
        var shared = await BuildSharedQuotaAsync(telegramUserId, effectivePlan, ct);
        var maxFileBytes = await GetMaxFileBytesAsync(telegramUserId, ct);

        var reserved = await planLifecycle.GetReservedPlansAsync(telegramUserId, ct);
        var reservedDtos = reserved
            .Select(r => new ReservedPlanDto(r.Id, r.Plan, r.DurationDays, r.CreatedAt))
            .ToList();

        return new UserAccountSummaryDto(
            telegramUserId,
            effectivePlan,
            user.Plan,
            user.PlanExpiresAt,
            QuotaPeriodHelper.GetPeriodStartUtc(user),
            user.IsBanned,
            PlanLifecycleService.HasSubscriptionAccess(user),
            PlanLifecycleService.IsTrialActive(user),
            shared,
            maxFileBytes >= long.MaxValue ? 0 : maxFileBytes,
            reservedDtos);
    }

    private async Task<SharedQuotaUsageDto> BuildSharedQuotaAsync(
        long telegramUserId,
        SubscriptionPlan plan,
        CancellationToken ct)
    {
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var adjustments = await GetAdjustmentsAsync(telegramUserId, ct);
        var monthly = await GetTotalDownloadUsageAsync(telegramUserId, monthlyStart, ct);
        var searchUsed = await GetTotalSearchUsageAsync(telegramUserId, monthlyStart, ct);
        var planRow = await planDefinitions.GetPlanAsync(plan, ct);

        return new SharedQuotaUsageDto(
            monthly.Count,
            planRow.MonthlyDownloadCount + adjustments.ExtraCount,
            monthly.Bytes,
            planRow.MonthlyDownloadBytes + adjustments.ExtraBytes,
            searchUsed,
            planRow.MonthlySearchCount + adjustments.ExtraSearch,
            adjustments.ExtraCount,
            adjustments.ExtraBytes,
            adjustments.ExtraSearch);
    }

    private async Task<(int ExtraCount, long ExtraBytes, int ExtraSearch)> GetAdjustmentsAsync(
        long telegramUserId,
        CancellationToken ct)
    {
        var row = await db.UserQuotaAdjustments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId, ct);

        return row is null ? (0, 0, 0) : (row.ExtraDownloadCount, row.ExtraDownloadBytes, row.ExtraSearchCount);
    }

    public async Task<(long DownloadCount, long DownloadBytes, long SearchCount)> GetPeriodUsageAsync(
        long telegramUserId,
        CancellationToken ct = default)
    {
        var since = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var monthly = await GetTotalDownloadUsageAsync(telegramUserId, since, ct);
        var search = await GetTotalSearchUsageAsync(telegramUserId, since, ct);
        return (monthly.Count, monthly.Bytes, search);
    }

    private async Task<DateTime> GetQuotaPeriodStartUtcAsync(long telegramUserId, CancellationToken ct)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        return user is null ? DateTime.UtcNow : QuotaPeriodHelper.GetPeriodStartUtc(user);
    }

    private async Task<(long Count, long Bytes)> GetTotalDownloadUsageAsync(
        long telegramUserId,
        DateTime since,
        CancellationToken ct)
    {
        var query = db.DownloadUsageLogs.AsNoTracking()
            .Where(l => l.TelegramUserId == telegramUserId && l.CreatedAt >= since);

        var count = await query.CountAsync(ct);
        var bytes = await query.SumAsync(l => (long?)l.FileSizeBytes, ct) ?? 0;
        return (count, bytes);
    }

    private async Task<long> GetTotalSearchUsageAsync(
        long telegramUserId,
        DateTime since,
        CancellationToken ct) =>
        await db.SearchUsageLogs.AsNoTracking()
            .Where(l => l.TelegramUserId == telegramUserId && l.CreatedAt >= since)
            .LongCountAsync(ct);
}
