namespace ProPlusBot.Services.Media;

public sealed record PinterestSearchPage(
    IReadOnlyList<MediaSearchResultItem> Items,
    string? NextBookmark);
