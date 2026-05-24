namespace ProPlusBot.Services.Media;

public sealed record MediaDownloadWorkItem(
    Guid Id,
    long ChatId,
    string SourceUrl,
    DetectedMediaPlatform Platform,
    MediaDownloadSource Source,
    string? YouTubeFormatId,
    long? IncomingChatMessageId);
