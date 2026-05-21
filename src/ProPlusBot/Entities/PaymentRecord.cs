namespace ProPlusBot.Entities;

public class PaymentRecord
{
    public Guid Id { get; set; }
    public long TelegramUserId { get; set; }
    public PaymentType Type { get; set; }
    public PaymentStatus Status { get; set; }
    /// <summary>Amount in Toman (shown to users).</summary>
    public long AmountToman { get; set; }

    /// <summary>Amount in Rials sent to Bale (Toman × 10).</summary>
    public long AmountRials { get; set; }
    public string Currency { get; set; } = "IRR";
    public string Payload { get; set; } = string.Empty;
    public SubscriptionPlan? FromPlan { get; set; }
    public SubscriptionPlan? ToPlan { get; set; }
    public MediaPlatformKind? Platform { get; set; }
    public string? ProviderPaymentChargeId { get; set; }
    public string? ProviderTelegramPaymentChargeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }

    public BotUser User { get; set; } = null!;
}
