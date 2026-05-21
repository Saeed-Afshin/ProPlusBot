namespace ProPlusBot.Entities;

public class UserReservedPlan
{
    public Guid Id { get; set; }
    public long TelegramUserId { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public int DurationDays { get; set; }
    public DateTime CreatedAt { get; set; }

    public BotUser User { get; set; } = null!;
}
