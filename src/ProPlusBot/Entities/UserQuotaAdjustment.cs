namespace ProPlusBot.Entities;

/// <summary>Purchased or admin-granted extra download quota (shared across platforms).</summary>
public class UserQuotaAdjustment
{
    public long TelegramUserId { get; set; }
    public int ExtraDownloadCount { get; set; }
    public long ExtraDownloadBytes { get; set; }
    public int ExtraSearchCount { get; set; }
    public DateTime UpdatedAt { get; set; }

    public BotUser User { get; set; } = null!;
}
