using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class AdminUserBulkQuotaService(
    AppDbContext db,
    QuotaService quotaService,
    TrialSettingsService trialSettings,
    IOptions<PaymentOptions> paymentOptions)
{
    private readonly PaymentOptions _paymentOptions = paymentOptions.Value;

    public async Task<IReadOnlyList<long>> ResolveUserIdsAsync(
        Pages.Messages.AdminUserTargetKind target,
        long? singleUserId,
        string? selectedUserIds,
        CancellationToken ct = default) =>
        target switch
        {
            Pages.Messages.AdminUserTargetKind.Single when singleUserId is > 0 => [singleUserId.Value],
            Pages.Messages.AdminUserTargetKind.All => await db.BotUsers.AsNoTracking()
                .Select(u => u.TelegramUserId)
                .ToListAsync(ct),
            Pages.Messages.AdminUserTargetKind.Selected => ParseUserIds(selectedUserIds),
            _ => []
        };

    public async Task<AdminBulkApplyResult> ApplyAsync(
        AdminBulkQuotaAction action,
        IReadOnlyList<long> userIds,
        decimal value,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new AdminBulkApplyResult(0, 0, ["هیچ کاربری انتخاب نشده است."]);

        var errors = new List<string>();
        var succeeded = 0;

        foreach (var userId in userIds)
        {
            try
            {
                await ApplyToUserAsync(userId, action, value, ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                errors.Add($"{userId}: {ex.Message}");
            }
        }

        if (succeeded > 0)
            await db.SaveChangesAsync(ct);

        return new AdminBulkApplyResult(succeeded, userIds.Count - succeeded, errors);
    }

    private async Task ApplyToUserAsync(
        long telegramUserId,
        AdminBulkQuotaAction action,
        decimal value,
        CancellationToken ct)
    {
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("کاربر یافت نشد.");

        switch (action)
        {
            case AdminBulkQuotaAction.ExtendPlanDays:
                ApplyExtendPlanDays(user, (int)value);
                break;
            case AdminBulkQuotaAction.ResetPlanExpiry:
                await ApplyResetPlanExpiryAsync(user, ct);
                break;
            case AdminBulkQuotaAction.ExtendDownloadCount:
                await ApplyExtendDownloadCountAsync(telegramUserId, (int)value, ct);
                break;
            case AdminBulkQuotaAction.ResetDownloadCount:
                await ApplyResetDownloadCountAsync(telegramUserId, ct);
                break;
            case AdminBulkQuotaAction.ExtendDownloadMegabytes:
                await ApplyExtendDownloadBytesAsync(telegramUserId, ByteUnits.FromMegabytes(value), ct);
                break;
            case AdminBulkQuotaAction.ResetDownloadVolume:
                await ApplyResetDownloadBytesAsync(telegramUserId, ct);
                break;
            case AdminBulkQuotaAction.ExtendSearchCount:
                await ApplyExtendSearchCountAsync(telegramUserId, (int)value, ct);
                break;
            case AdminBulkQuotaAction.ResetSearchCount:
                await ApplyResetSearchCountAsync(telegramUserId, ct);
                break;
            default:
                throw new InvalidOperationException("عملیات نامعتبر است.");
        }

        user.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyExtendPlanDays(BotUser user, int days)
    {
        if (days <= 0)
            throw new InvalidOperationException("تعداد روز باید بزرگ‌تر از صفر باشد.");

        var baseDate = user.PlanExpiresAt is null || user.PlanExpiresAt < DateTime.UtcNow
            ? DateTime.UtcNow
            : user.PlanExpiresAt.Value;

        user.PlanExpiresAt = baseDate.AddDays(days);
    }

    private async Task ApplyResetPlanExpiryAsync(BotUser user, CancellationToken ct)
    {
        if (user.Plan == SubscriptionPlan.Free || !PlanLifecycleService.IsPaidPlanActive(user))
        {
            var trialDays = (await trialSettings.GetAsync(ct)).DurationDays;
            user.PlanExpiresAt = DateTime.UtcNow.AddDays(trialDays);
        }
        else
        {
            user.PlanExpiresAt = DateTime.UtcNow.AddDays(_paymentOptions.PlanDurationDays);
        }
    }

    private async Task ApplyExtendDownloadCountAsync(long telegramUserId, int add, CancellationToken ct)
    {
        if (add <= 0)
            throw new InvalidOperationException("مقدار باید بزرگ‌تر از صفر باشد.");

        var adj = await GetOrCreateAdjustmentAsync(telegramUserId, ct);
        adj.ExtraDownloadCount = Math.Max(0, adj.ExtraDownloadCount + add);
        adj.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets remaining download count to the plan allowance (used + plan limit total cap).
    /// </summary>
    private async Task ApplyResetDownloadCountAsync(long telegramUserId, CancellationToken ct)
    {
        var (used, _, _) = await quotaService.GetPeriodUsageAsync(telegramUserId, ct);
        var adj = await GetOrCreateAdjustmentAsync(telegramUserId, ct);
        adj.ExtraDownloadCount = (int)Math.Min(used, int.MaxValue);
        adj.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ApplyExtendDownloadBytesAsync(long telegramUserId, long addBytes, CancellationToken ct)
    {
        if (addBytes <= 0)
            throw new InvalidOperationException("مقدار باید بزرگ‌تر از صفر باشد.");

        var adj = await GetOrCreateAdjustmentAsync(telegramUserId, ct);
        adj.ExtraDownloadBytes = Math.Max(0, adj.ExtraDownloadBytes + addBytes);
        adj.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ApplyResetDownloadBytesAsync(long telegramUserId, CancellationToken ct)
    {
        var (_, usedBytes, _) = await quotaService.GetPeriodUsageAsync(telegramUserId, ct);
        var adj = await GetOrCreateAdjustmentAsync(telegramUserId, ct);
        adj.ExtraDownloadBytes = usedBytes;
        adj.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ApplyExtendSearchCountAsync(long telegramUserId, int add, CancellationToken ct)
    {
        if (add <= 0)
            throw new InvalidOperationException("مقدار باید بزرگ‌تر از صفر باشد.");

        var adj = await GetOrCreateAdjustmentAsync(telegramUserId, ct);
        adj.ExtraSearchCount = Math.Max(0, adj.ExtraSearchCount + add);
        adj.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ApplyResetSearchCountAsync(long telegramUserId, CancellationToken ct)
    {
        var (_, _, searchUsed) = await quotaService.GetPeriodUsageAsync(telegramUserId, ct);
        var adj = await GetOrCreateAdjustmentAsync(telegramUserId, ct);
        adj.ExtraSearchCount = (int)Math.Min(searchUsed, int.MaxValue);
        adj.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<UserQuotaAdjustment> GetOrCreateAdjustmentAsync(long telegramUserId, CancellationToken ct)
    {
        var adj = await db.UserQuotaAdjustments
            .FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId, ct);

        if (adj is not null)
            return adj;

        adj = new UserQuotaAdjustment { TelegramUserId = telegramUserId };
        db.UserQuotaAdjustments.Add(adj);
        return adj;
    }

    private static IReadOnlyList<long> ParseUserIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split([',', '\n', '\r', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : (long?)null)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }
}
