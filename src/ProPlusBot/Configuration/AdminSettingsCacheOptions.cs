namespace ProPlusBot.Configuration;

public class AdminSettingsCacheOptions
{
    public const string SectionName = "AdminSettingsCache";

    /// <summary>How long cached admin settings stay valid before reload from DB.</summary>
    public int TtlMinutes { get; set; } = 10;
}
