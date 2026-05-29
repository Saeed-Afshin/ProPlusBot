namespace ProPlusBot.Entities;

/// <summary>Per-plan subscription pricing, monthly quotas, max file sizes, and extra-quota pack settings.</summary>
public class PlanPricing
{
    public SubscriptionPlan Plan { get; set; }

    /// <summary>Monthly subscription price in Toman.</summary>
    public long MonthlyPriceToman { get; set; }

    /// <summary>Shared monthly download count (YouTube + Pinterest).</summary>
    public long MonthlyDownloadCount { get; set; }

    /// <summary>Shared monthly download bytes (YouTube + Pinterest).</summary>
    public long MonthlyDownloadBytes { get; set; }

    /// <summary>Shared monthly search count (YouTube + Pinterest).</summary>
    public long MonthlySearchCount { get; set; }

    /// <summary>Max new support tickets per quota month; 0 = unlimited.</summary>
    public int MonthlyTicketLimit { get; set; }

    public long MaxFileBytes { get; set; }

    public long ExtraDownloadCountPriceToman { get; set; }
    public int ExtraDownloadCountPack { get; set; }

    public long ExtraDownloadBytesPriceToman { get; set; }
    public long ExtraDownloadBytesPack { get; set; }

    public bool FallbackOnSizeExceed { get; set; }
    public bool FallbackOnBaleFailure { get; set; }
    public int FallbackLinkExpiryHours { get; set; } = 24;

    public DateTime UpdatedAt { get; set; }
}
