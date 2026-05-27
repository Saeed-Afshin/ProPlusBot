using ProPlusBot.Services.Media;

namespace ProPlusBot.Entities;

public class FallbackUpload
{
    public Guid Id { get; set; }
    public string ContentKey { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DetectedMediaPlatform Platform { get; set; }
    public string? YouTubeFormatId { get; set; }
    public string StorageObjectKey { get; set; } = "";
    public string PublicUrl { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
}
