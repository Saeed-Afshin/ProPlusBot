namespace ProPlusBot.Configuration;

public class BotOptions
{
    public const string SectionName = "Bot";

    public string Token { get; set; } = string.Empty;
    public string BaleApiBaseUrl { get; set; } = "https://tapi.bale.ai";
    public string RequiredChannelUsername { get; set; } = "@AfshinPlus";
    public long RequiredChannelId { get; set; } = 5824531861;
    public string? WebhookSecret { get; set; }
}
