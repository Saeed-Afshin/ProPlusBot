namespace ProPlusBot.Configuration;

public class BotOptions
{
    public const string SectionName = "Bot";

    public string Token { get; set; } = string.Empty;
    public string BaleApiBaseUrl { get; set; } = "https://tapi.bale.ai";
    public string RequiredChannelUsername { get; set; } = "@AfshinPlus";
    public long RequiredChannelId { get; set; } = 5824531861;
    /// <summary>Bale one-click join link (e.g. ble.ir/join/…).</summary>
    public string RequiredChannelJoinUrl { get; set; } = "https://ble.ir/join/82e7aBdU76";
    public string? WebhookSecret { get; set; }
}
