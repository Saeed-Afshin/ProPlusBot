namespace ProPlusBot.Configuration;

public class AppDataProtectionOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>Must stay the same across instances and redeploys.</summary>
    public string ApplicationName { get; set; } = "ProPlusBot";

    /// <summary>Persist antiforgery / TempData keys (e.g. /app/data/dataprotection-keys in Docker).</summary>
    public string? KeysPath { get; set; }
}
