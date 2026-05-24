namespace ProPlusBot.Services.Media;

public enum MediaConversationState
{
    Idle,
    AwaitingYouTubeQuery,
    AwaitingPinterestQuery,
    AwaitingTicketMessage
}

public sealed record MediaSearchResultItem(string Title, string Url, string? ThumbnailUrl);
