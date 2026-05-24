using ProPlusBot.Services.Media;

namespace ProPlusBot.Entities;

public class MediaDownloadJobEntity
{
    public Guid Id { get; set; }
    public long TelegramUserId { get; set; }
    public string SourceUrl { get; set; } = "";
    public DetectedMediaPlatform Platform { get; set; }
    public MediaDownloadSource Source { get; set; }
    public string? YouTubeFormatId { get; set; }
    public MediaDownloadJobStatus Status { get; set; }
    public string? ResultSummary { get; set; }
    public string? ErrorDetail { get; set; }
    public long? FileSizeBytes { get; set; }
    public long? IncomingChatMessageId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public BotUser User { get; set; } = null!;
    public ChatMessage? IncomingChatMessage { get; set; }
}
