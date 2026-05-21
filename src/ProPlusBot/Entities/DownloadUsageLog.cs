namespace ProPlusBot.Entities;

public class DownloadUsageLog
{
    public long Id { get; set; }
    public long TelegramUserId { get; set; }
    public MediaPlatformKind Platform { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    public BotUser User { get; set; } = null!;
}
