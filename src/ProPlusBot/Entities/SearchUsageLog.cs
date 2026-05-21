namespace ProPlusBot.Entities;

public class SearchUsageLog
{
    public long Id { get; set; }
    public long TelegramUserId { get; set; }
    public MediaPlatformKind Platform { get; set; }
    public DateTime CreatedAt { get; set; }

    public BotUser User { get; set; } = null!;
}
