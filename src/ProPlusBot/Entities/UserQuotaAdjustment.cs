namespace ProPlusBot.Entities;

/// <summary>Manual admin adjustments to extra quota balances (not consumed by plan limits).</summary>
public class UserQuotaAdjustment
{
    public long TelegramUserId { get; set; }
    public MediaPlatformKind Platform { get; set; }
    public int ExtraDownloadCount { get; set; }
    public long ExtraDownloadBytes { get; set; }
    public DateTime UpdatedAt { get; set; }

    public BotUser User { get; set; } = null!;
}
