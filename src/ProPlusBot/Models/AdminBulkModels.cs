namespace ProPlusBot.Models;

public enum AdminBulkQuotaAction
{
    ExtendPlanDays,
    ResetPlanExpiry,
    ExtendDownloadCount,
    ResetDownloadCount,
    ExtendDownloadMegabytes,
    ResetDownloadVolume,
    ExtendSearchCount,
    ResetSearchCount
}

public record AdminBulkApplyResult(int Succeeded, int Failed, IReadOnlyList<string> Errors);
