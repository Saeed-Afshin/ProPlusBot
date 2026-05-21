namespace ProPlusBot.Entities;

/// <summary>Singleton (Id=1) — price and pack size for extra quota purchases.</summary>
public class ExtraQuotaPackSettings
{
    public int Id { get; set; } = 1;
    /// <summary>Pack price in Toman.</summary>
    public long PriceToman { get; set; }
    public int ExtraDownloadCount { get; set; }
    public long ExtraDownloadBytes { get; set; }
    public DateTime UpdatedAt { get; set; }
}
