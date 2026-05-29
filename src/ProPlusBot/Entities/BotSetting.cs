namespace ProPlusBot.Entities;

public class BotSetting
{
    public int Id { get; set; } = 1;
    public BotMode Mode { get; set; } = BotMode.Live;
    public BotUpdateMode UpdateMode { get; set; } = BotUpdateMode.LongPolling;
    public bool IsActive { get; set; } = true;
    public bool YouTubeEnabled { get; set; } = true;
    public bool PinterestEnabled { get; set; } = true;

    /// <summary>Files at or above this size (bytes) go directly to ArvanCloud when the plan allows size-exceed fallback.</summary>
    public long BaleDirectArvanThresholdBytes { get; set; } = 49L * 1024 * 1024;

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
