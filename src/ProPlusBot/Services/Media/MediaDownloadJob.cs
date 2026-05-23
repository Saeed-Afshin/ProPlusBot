namespace ProPlusBot.Services.Media;

public enum MediaDownloadSource
{
    Url,
    YouTubeSearch,
    PinterestSearch
}

public sealed record MediaDownloadJob(
    long ChatId,
    string SourceUrl,
    DetectedMediaPlatform Platform,
    MediaDownloadSource Source = MediaDownloadSource.Url,
    string? YouTubeFormatId = null);
