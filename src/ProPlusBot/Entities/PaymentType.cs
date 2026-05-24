namespace ProPlusBot.Entities;

public enum PaymentType
{
    PlanUpgrade = 0,
    /// <summary>Legacy combined extra quota (count + bytes).</summary>
    ExtraQuota = 1,
    PlanPurchase = 2,
    ExtraDownloadCount = 3,
    ExtraDownloadBytes = 4,
    ExtraDownloadPack = 5
}
