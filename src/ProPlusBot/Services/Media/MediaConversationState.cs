namespace ProPlusBot.Services.Media;

public enum MediaConversationState
{
    Idle,
    AwaitingYouTubeQuery,
    AwaitingPinterestQuery
}

public sealed record MediaSearchResultItem(string Title, string Url, string? ThumbnailUrl);
