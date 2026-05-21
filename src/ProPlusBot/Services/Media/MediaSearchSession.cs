namespace ProPlusBot.Services.Media;

public sealed record MediaSearchSession(
    string Query,
    int Page,
    IReadOnlyList<MediaSearchResultItem> Results,
    bool HasMoreResults,
    string CallbackPrefix,
    DetectedMediaPlatform Platform,
    string? PinterestBookmark,
    SearchGridLayout GridLayout);
