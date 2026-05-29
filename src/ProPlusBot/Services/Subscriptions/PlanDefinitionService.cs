using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Services.Subscriptions;

public class PlanDefinitionService(
    AppDbContext db,
    AdminSettingsCache adminSettingsCache)
{
    public async Task<PlanPricing> GetPlanAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        var snapshot = await adminSettingsCache.GetAsync(ct);
        return snapshot.GetPlan(plan);
    }

    public async Task<List<PlanDefinitionDto>> GetAllDefinitionsAsync(CancellationToken ct = default)
    {
        var snapshot = await adminSettingsCache.GetAsync(ct);
        return snapshot.Plans.Values
            .OrderBy(p => p.Plan)
            .Select(ToDto)
            .ToList();
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
        row.MaxFileBytes = maxFileBytes;
        row.ExtraDownloadCountPriceToman = packPrice;
        row.ExtraDownloadBytesPriceToman = packPrice;
        row.ExtraDownloadCountPack = Math.Max(0, dto.ExtraDownloadCountPack);
        row.ExtraDownloadBytesPack = packBytes;
        row.FallbackOnSizeExceed = dto.FallbackOnSizeExceed;
        row.FallbackOnBaleFailure = dto.FallbackOnBaleFailure;
        row.FallbackLinkExpiryHours = UploadFallbackPresets.NormalizeExpiryHours(dto.FallbackLinkExpiryHours);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<long> GetMaxFileBytesAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        var planRow = await GetPlanAsync(plan, ct);
        return planRow.MaxFileBytes;
    }

    public static PlanDefinitionDto ToDto(PlanPricing p) =>
        new(
            p.Plan,
            p.MonthlyPriceToman,
            p.MonthlyDownloadCount,
            ByteUnits.ToMegabytes(p.MonthlyDownloadBytes),
            p.MonthlySearchCount,
            p.MonthlyTicketLimit,
            ByteUnits.ToMegabytes(p.MaxFileBytes),
            PlanExtraPackHelper.GetPackPriceToman(p),
            p.ExtraDownloadCountPack,
            ByteUnits.ToMegabytes(p.ExtraDownloadBytesPack),
            p.FallbackOnSizeExceed,
            p.FallbackOnBaleFailure,
            p.FallbackLinkExpiryHours);
}
