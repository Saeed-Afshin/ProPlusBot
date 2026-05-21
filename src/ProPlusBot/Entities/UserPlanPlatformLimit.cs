namespace ProPlusBot.Entities;

public class UserPlanPlatformLimit
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public MediaPlatformKind Platform { get; set; }
    public UsagePeriod Period { get; set; }
    public QuotaLimitKind LimitKind { get; set; }
    public long LimitValue { get; set; }
    public DateTime UpdatedAt { get; set; }

    public BotUser User { get; set; } = null!;
}
