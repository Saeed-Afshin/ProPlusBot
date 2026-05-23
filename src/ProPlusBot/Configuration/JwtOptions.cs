namespace ProPlusBot.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Symmetric signing key (min 32 characters). Set via env Jwt__Secret so tokens survive redeploy.</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ProPlusBot";

    public string Audience { get; set; } = "ProPlusBot.Admin";

    public int ExpirationDays { get; set; } = 30;
}
