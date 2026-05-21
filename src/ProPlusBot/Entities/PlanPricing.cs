namespace ProPlusBot.Entities;

public class PlanPricing
{
    public SubscriptionPlan Plan { get; set; }
    /// <summary>Monthly price in Toman.</summary>
    public long MonthlyPriceToman { get; set; }
    public DateTime UpdatedAt { get; set; }
}
