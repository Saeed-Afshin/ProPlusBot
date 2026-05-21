namespace ProPlusBot.Services.Media;

/// <summary>Current search page state (up to 9 items). Next page re-queries the platform API.</summary>
public sealed record MediaSearchSession(
    string Query,
    int Page,
    IReadOnlyList<MediaSearchResultItem> Results,
    bool HasMoreResults,
    string CallbackPrefix,
    DetectedMediaPlatform Platform,
    string? PinterestBookmark);
