using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class SubscriptionAdminService(AppDbContext db)
{
    public async Task<List<PlanLimitDto>> GetLimitsAsync(CancellationToken ct = default) =>
        await db.PlanPlatformLimits.AsNoTracking()
            .OrderBy(l => l.Plan).ThenBy(l => l.Platform).ThenBy(l => l.Period).ThenBy(l => l.LimitKind)
            .Select(l => new PlanLimitDto(l.Id, l.Plan, l.Platform, l.Period, l.LimitKind, l.LimitValue))
            .ToListAsync(ct);

    public async Task UpdateLimitAsync(int id, long value, CancellationToken ct = default)
    {
        var limit = await db.PlanPlatformLimits.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new InvalidOperationException("محدودیت یافت نشد.");

        limit.LimitValue = Math.Max(0, value);
        limit.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateLimitFromMegabytesAsync(int id, decimal megabytes, CancellationToken ct = default) =>
        await UpdateLimitAsync(id, ByteUnits.FromMegabytes(megabytes), ct);

    public async Task<List<PlanLimitMatrixRow>> GetLimitMatrixAsync(CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(ct);
        var rows = limits
            .GroupBy(l => (l.Platform, l.Period, l.LimitKind))
            .Select(g =>
            {
                var cells = g.ToDictionary(
                    x => x.Plan,
                    x => new PlanLimitCell(x.Id, PlanLimitDisplay.ToDisplayValue(x.LimitKind, x.LimitValue)));

                foreach (var plan in Enum.GetValues<SubscriptionPlan>())
                {
                    if (!cells.ContainsKey(plan))
                        cells[plan] = new PlanLimitCell(0, 0);
                }

                var first = g.First();
                return new PlanLimitMatrixRow(first.Platform, first.Period, first.LimitKind, cells);
            })
            .OrderBy(r => r.Platform)
            .ThenBy(r => r.LimitKind)
            .ThenBy(r => r.Period)
            .ToList();

        return rows;
    }

    public async Task UpdateLimitRowAsync(
        MediaPlatformKind platform,
        UsagePeriod period,
        QuotaLimitKind limitKind,
        IReadOnlyDictionary<int, decimal> valuesByPlan,
        CancellationToken ct = default)
    {
        var limits = await db.PlanPlatformLimits
            .Where(l => l.Platform == platform && l.Period == period && l.LimitKind == limitKind)
            .ToListAsync(ct);

        foreach (var limit in limits)
        {
            if (!valuesByPlan.TryGetValue((int)limit.Plan, out var displayValue))
                continue;

            limit.LimitValue = ByteUnits.IsByteLimitKind(limitKind)
                ? ByteUnits.FromMegabytes(displayValue)
                : Math.Max(0, (long)displayValue);
            limit.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PlanPricingDto>> GetPricingAsync(CancellationToken ct = default) =>
        await db.PlanPricings.AsNoTracking()
            .OrderBy(p => p.Plan)
            .Select(p => new PlanPricingDto(p.Plan, p.MonthlyPriceToman))
            .ToListAsync(ct);

    public async Task UpdatePricingAsync(SubscriptionPlan plan, long monthlyPriceToman, CancellationToken ct = default)
    {
        var row = await db.PlanPricings.FirstOrDefaultAsync(p => p.Plan == plan, ct)
            ?? throw new InvalidOperationException("قیمت پلن یافت نشد.");

        row.MonthlyPriceToman = Math.Max(0, monthlyPriceToman);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ExtraQuotaPackSettings> GetExtraPackAsync(CancellationToken ct = default) =>
        await db.ExtraQuotaPackSettings.AsNoTracking().FirstAsync(ct);

    public async Task UpdateExtraPackAsync(long priceToman, int count, long bytes, CancellationToken ct = default)
    {
        var pack = await db.ExtraQuotaPackSettings.FirstAsync(ct);
        pack.PriceToman = Math.Max(0, priceToman);
        pack.ExtraDownloadCount = Math.Max(0, count);
        pack.ExtraDownloadBytes = Math.Max(0, bytes);
        pack.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PaymentRecordDto>> ListPaymentsAsync(int take = 200, CancellationToken ct = default) =>
        await db.PaymentRecords.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .Select(p => new PaymentRecordDto(
                p.Id,
                p.TelegramUserId,
                p.User.PhoneNumber,
                p.Type,
                p.Status,
                p.AmountToman,
                p.FromPlan,
                p.ToPlan,
                p.Platform,
                p.CreatedAt,
                p.CompletedAt,
                p.Note))
            .ToListAsync(ct);

    public async Task<List<BotUserAdminDto>> ListUsersAsync(string? search, int take = 200, CancellationToken ct = default)
    {
        var query = db.BotUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            if (long.TryParse(search, out var id))
                query = query.Where(u => u.TelegramUserId == id);
            else
                query = query.Where(u =>
                    (u.Username != null && u.Username.Contains(search))
                    || (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Take(take)
            .Select(u => new BotUserAdminDto(
                u.TelegramUserId,
                u.Username,
                u.PhoneNumber,
                u.Plan,
                u.PlanExpiresAt,
                u.IsBanned,
                u.CreatedAt))
            .ToListAsync(ct);
    }
}
