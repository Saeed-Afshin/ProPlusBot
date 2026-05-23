namespace ProPlusBot.Entities;

public class BotSetting
{
    public int Id { get; set; } = 1;
    public BotMode Mode { get; set; } = BotMode.Live;
    public BotUpdateMode UpdateMode { get; set; } = BotUpdateMode.LongPolling;
    public bool IsActive { get; set; } = true;
    public bool YouTubeEnabled { get; set; } = true;
    public bool PinterestEnabled { get; set; } = true;
    public int SearchGridColumns { get; set; } = 3;
    public int SearchGridRows { get; set; } = 3;
    public int SearchGridJpegQuality { get; set; } = 85;
    public string? WebhookUrl { get; set; }
    /// <summary>Netscape-format YouTube cookies (set from admin panel).</summary>
    public string? YouTubeCookiesContent { get; set; }
    public DateTime? YouTubeCookiesUpdatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByAdminId { get; set; }
}
