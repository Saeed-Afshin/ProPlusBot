namespace ProPlusBot.Entities;

public class BotUser
{
    public long TelegramUserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public AdminRole? ResolvedRole { get; set; }
    public bool HasSharedPhone { get; set; }
    public bool HasJoinedChannel { get; set; }
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public DateTime? PlanExpiresAt { get; set; }
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = [];
    public ICollection<DownloadUsageLog> DownloadUsages { get; set; } = [];
    public ICollection<SearchUsageLog> SearchUsages { get; set; } = [];
    public ICollection<PaymentRecord> Payments { get; set; } = [];
    public ICollection<UserQuotaAdjustment> QuotaAdjustments { get; set; } = [];
    public ICollection<UserReservedPlan> ReservedPlans { get; set; } = [];
    public ICollection<UserPlanPlatformLimit> PlanPlatformLimits { get; set; } = [];
}
