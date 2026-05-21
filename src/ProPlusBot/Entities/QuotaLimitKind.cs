namespace ProPlusBot.Entities;

public enum QuotaLimitKind
{
    DownloadCount = 0,
    DownloadBytes = 1,
    /// <summary>Max size per file; stored with <see cref="UsagePeriod.Daily"/> (period ignored).</summary>
    MaxFileBytes = 2
}
