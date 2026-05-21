namespace ProPlusBot.Configuration;

public class PaymentOptions
{
    public const string SectionName = "Payment";

    public string TestProviderToken { get; set; } = "WALLET-TEST-1111111111111111";
    public string LiveProviderToken { get; set; } = "WALLET-qSjR9JHWY8OFxjsn";
    public string Currency { get; set; } = "IRR";

    public int PlanDurationDays { get; set; } = 30;

    /// <summary>Pending invoices older than this are auto-cancelled.</summary>
    public int PendingPaymentExpiryHours { get; set; } = 24;
}
