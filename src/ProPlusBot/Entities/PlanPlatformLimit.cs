namespace ProPlusBot.Entities;

public class PlanPlatformLimit
{
    public int Id { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public MediaPlatformKind Platform { get; set; }
    public UsagePeriod Period { get; set; }
    public QuotaLimitKind LimitKind { get; set; }
    public long LimitValue { get; set; }
    public DateTime UpdatedAt { get; set; }
}
