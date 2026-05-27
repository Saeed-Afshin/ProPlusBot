namespace ProPlusBot.Entities;

public class BotSetting
{
    public int Id { get; set; } = 1;
    public BotMode Mode { get; set; } = BotMode.Live;
    public BotUpdateMode UpdateMode { get; set; } = BotUpdateMode.LongPolling;
    public bool IsActive { get; set; } = true;
    public bool YouTubeEnabled { get; set; } = true;
    public bool PinterestEnabled { get; set; } = true;
    public bool UploadFallbackEnabled { get; set; }
    /// <summary>Files at or above this size (bytes) use Arvan fallback instead of Bale when fallback is enabled.</summary>
    public long UploadFallbackMinBytes { get; set; } = 49L * 1024 * 1024;
    public int UploadFallbackExpiryHours { get; set; } = 24;
    public int SearchGridColumns { get; set; } = 3;
    public int SearchGridRows { get; set; } = 3;
    public int SearchGridJpegQuality { get; set; } = 85;
    public ConversationStateBackend ConversationStateBackend { get; set; } = ConversationStateBackend.Memory;
    public string? WebhookUrl { get; set; }
    /// <summary>Netscape-format YouTube cookies (set from admin panel).</summary>
    public string? YouTubeCookiesContent { get; set; }
    public DateTime? YouTubeCookiesUpdatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByAdminId { get; set; }
}
