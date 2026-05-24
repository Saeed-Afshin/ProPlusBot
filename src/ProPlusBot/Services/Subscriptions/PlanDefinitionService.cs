using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class PlanDefinitionService(AppDbContext db)
{
    public async Task<PlanPricing> GetPlanAsync(SubscriptionPlan plan, CancellationToken ct = default) =>
        await db.PlanPricings.AsNoTracking().FirstAsync(p => p.Plan == plan, ct);

    public async Task<List<PlanDefinitionDto>> GetAllDefinitionsAsync(CancellationToken ct = default)
    {
        var rows = await db.PlanPricings.AsNoTracking().OrderBy(p => p.Plan).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task UpdateDefinitionAsync(PlanDefinitionDto dto, CancellationToken ct = default)
    {
        var row = await db.PlanPricings.FirstAsync(p => p.Plan == dto.Plan, ct);
        var maxFileBytes = ByteUnits.FromMegabytes(dto.MaxFileMegabytes);
        var packPrice = Math.Max(0, dto.ExtraDownloadPackPriceToman);
        var packBytes = ByteUnits.FromMegabytes(dto.ExtraDownloadPackMegabytes);

        row.MonthlyPriceToman = Math.Max(0, dto.MonthlyPriceToman);
        row.MonthlyDownloadCount = Math.Max(0, dto.MonthlyDownloadCount);
        row.MonthlyDownloadBytes = ByteUnits.FromMegabytes(dto.MonthlyDownloadMegabytes);
        row.MonthlySearchCount = Math.Max(0, dto.MonthlySearchCount);
        row.MonthlyTicketLimit = Math.Max(0, dto.MonthlyTicketLimit);
        row.MaxFileBytesYouTube = maxFileBytes;
        row.MaxFileBytesPinterest = maxFileBytes;
        row.ExtraDownloadCountPriceToman = packPrice;
        row.ExtraDownloadBytesPriceToman = packPrice;
        row.ExtraDownloadCountPack = Math.Max(0, dto.ExtraDownloadCountPack);
        row.ExtraDownloadBytesPack = packBytes;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<long> GetMaxFileBytesAsync(
        long telegramUserId,
        SubscriptionPlan plan,
        MediaPlatformKind platform,
        CancellationToken ct = default)
    {
        var planRow = await GetPlanAsync(plan, ct);
        var planMax = PlanExtraPackHelper.GetUnifiedMaxFileBytes(planRow);

        var userOverride = await db.UserPlanPlatformLimits.AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.TelegramUserId == telegramUserId
                && l.Platform == platform
                && l.LimitKind == QuotaLimitKind.MaxFileBytes, ct);

        return userOverride?.LimitValue ?? planMax;
    }

    public static PlanDefinitionDto ToDto(PlanPricing p) =>
        new(
            p.Plan,
            p.MonthlyPriceToman,
            p.MonthlyDownloadCount,
            ByteUnits.ToMegabytes(p.MonthlyDownloadBytes),
            p.MonthlySearchCount,
            p.MonthlyTicketLimit,
            ByteUnits.ToMegabytes(PlanExtraPackHelper.GetUnifiedMaxFileBytes(p)),
            PlanExtraPackHelper.GetPackPriceToman(p),
            p.ExtraDownloadCountPack,
            ByteUnits.ToMegabytes(p.ExtraDownloadBytesPack));
}
