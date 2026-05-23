using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class QuotaService(
    AppDbContext db,
    PlanLifecycleService planLifecycle,
    UserPlanLimitService userPlanLimitService,
    IOptions<MediaDownloadOptions> mediaOptions)
{
    private readonly long _maxFileBytes = mediaOptions.Value.MaxUploadBytes;

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
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);

        var adjustments = await GetAdjustmentsAsync(telegramUserId, platform, ct);
        var monthly = await GetDownloadUsageAsync(telegramUserId, platform, monthlyStart, ct);
        var limits = await userPlanLimitService.ResolveLimitsAsync(telegramUserId, plan, platform, ct);

        var maxFileBytes = ResolveMaxFileBytes(limits);
        var countLimit = GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount);
        var bytesLimit = GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes);
        var extraCount = adjustments.ExtraCount;
        var extraBytes = adjustments.ExtraBytes;

        if (countLimit > 0 && monthly.Count >= countLimit + extraCount)
        {
            return (false,
                $"سقف تعداد دانلود ماهانه {MediaPlatformMapper.ToDisplayName(platform)} تمام شده است.");
        }

        if (bytesLimit > 0 && monthly.Bytes + maxFileBytes > bytesLimit + extraBytes)
        {
            return (false,
                $"سقف حجم دانلود ماهانه {MediaPlatformMapper.ToDisplayName(platform)} تمام شده است.");
        }

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
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var searchUsed = await GetSearchUsageAsync(telegramUserId, platform, monthlyStart, ct);
        var limits = await userPlanLimitService.ResolveLimitsAsync(telegramUserId, plan, platform, ct);
        var searchLimit = GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.SearchCount);

        if (searchLimit > 0 && searchUsed >= searchLimit)
        {
            return (false,
                $"سقف تعداد جستجوی ماهانه {MediaPlatformMapper.ToDisplayName(platform)} تمام شده است.");
        }

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

    public async Task<long> GetMaxFileBytesAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        CancellationToken ct = default)
    {
        var plan = await GetEffectivePlanAsync(telegramUserId, ct);
        var limits = await userPlanLimitService.ResolveLimitsAsync(telegramUserId, plan, platform, ct);
        return ResolveMaxFileBytes(limits);
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

        var plan = await GetEffectivePlanAsync(telegramUserId, ct);
        var limits = await userPlanLimitService.ResolveLimitsAsync(telegramUserId, plan, platform, ct);

        var maxFileBytes = ResolveMaxFileBytes(limits);
        if (fileSizeBytes <= maxFileBytes)
            return (true, null);

        return (false,
            $"حداکثر حجم هر فایل {MediaPlatformMapper.ToDisplayName(platform)} برای بسته شما {ByteUnits.FormatMegabytes(maxFileBytes)} است.");
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

        await ConsumeExtraQuotaIfNeededAsync(telegramUserId, platform, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task ConsumeExtraQuotaIfNeededAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        CancellationToken ct)
    {
        var plan = await GetEffectivePlanAsync(telegramUserId, ct);
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var monthly = await GetDownloadUsageAsync(telegramUserId, platform, monthlyStart, ct);
        var limits = await userPlanLimitService.ResolveLimitsAsync(telegramUserId, plan, platform, ct);

        var adjustment = await db.UserQuotaAdjustments
            .FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId && a.Platform == platform, ct);

        if (adjustment is null)
            return;

        var countLimit = GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount);
        var bytesLimit = GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes);

        if (countLimit > 0 && monthly.Count > countLimit && adjustment.ExtraDownloadCount > 0)
            adjustment.ExtraDownloadCount--;

        if (bytesLimit > 0 && monthly.Bytes > bytesLimit && adjustment.ExtraDownloadBytes > 0)
        {
            var over = monthly.Bytes - bytesLimit;
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
        var quotas = new List<QuotaUsageDto>();

        foreach (var platform in Enum.GetValues<MediaPlatformKind>())
            quotas.Add(await BuildQuotaDtoAsync(telegramUserId, effectivePlan, platform, ct));

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
            quotas,
            reservedDtos);
    }

    private async Task<QuotaUsageDto> BuildQuotaDtoAsync(
        long telegramUserId,
        SubscriptionPlan plan,
        MediaPlatformKind platform,
        CancellationToken ct)
    {
        var monthlyStart = await GetQuotaPeriodStartUtcAsync(telegramUserId, ct);
        var adjustments = await GetAdjustmentsAsync(telegramUserId, platform, ct);
        var monthly = await GetDownloadUsageAsync(telegramUserId, platform, monthlyStart, ct);
        var searchUsed = await GetSearchUsageAsync(telegramUserId, platform, monthlyStart, ct);
        var limits = await userPlanLimitService.ResolveLimitsAsync(telegramUserId, plan, platform, ct);

        return new QuotaUsageDto(
            platform,
            monthly.Count,
            GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount),
            monthly.Bytes,
            GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes),
            searchUsed,
            GetLimit(limits, UsagePeriod.Monthly, QuotaLimitKind.SearchCount),
            ResolveMaxFileBytes(limits),
            adjustments.ExtraCount,
            adjustments.ExtraBytes);
    }

    private async Task<(int ExtraCount, long ExtraBytes)> GetAdjustmentsAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        CancellationToken ct)
    {
        var row = await db.UserQuotaAdjustments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId && a.Platform == platform, ct);

        return row is null ? (0, 0) : (row.ExtraDownloadCount, row.ExtraDownloadBytes);
    }

    private async Task<DateTime> GetQuotaPeriodStartUtcAsync(long telegramUserId, CancellationToken ct)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        return user is null ? DateTime.UtcNow : QuotaPeriodHelper.GetPeriodStartUtc(user);
    }

    private async Task<(long Count, long Bytes)> GetDownloadUsageAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        DateTime since,
        CancellationToken ct)
    {
        var query = db.DownloadUsageLogs.AsNoTracking()
            .Where(l => l.TelegramUserId == telegramUserId
                && l.Platform == platform
                && l.CreatedAt >= since);

        var count = await query.CountAsync(ct);
        var bytes = await query.SumAsync(l => (long?)l.FileSizeBytes, ct) ?? 0;
        return (count, bytes);
    }

    private async Task<long> GetSearchUsageAsync(
        long telegramUserId,
        MediaPlatformKind platform,
        DateTime since,
        CancellationToken ct) =>
        await db.SearchUsageLogs.AsNoTracking()
            .Where(l => l.TelegramUserId == telegramUserId
                && l.Platform == platform
                && l.CreatedAt >= since)
            .LongCountAsync(ct);

    private static long GetLimit(
        IReadOnlyList<PlanPlatformLimit> limits,
        UsagePeriod period,
        QuotaLimitKind kind) =>
        limits.FirstOrDefault(l => l.Period == period && l.LimitKind == kind)?.LimitValue ?? 0;

    private long ResolveMaxFileBytes(IReadOnlyList<PlanPlatformLimit> limits)
    {
        var planMax = limits.FirstOrDefault(l => l.LimitKind == QuotaLimitKind.MaxFileBytes)?.LimitValue ?? 0;
        if (planMax <= 0)
            return _maxFileBytes;

        return Math.Min(planMax, _maxFileBytes);
    }
}
