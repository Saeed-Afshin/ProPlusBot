using Microsoft.Extensions.Caching.Memory;

namespace ProPlusBot.Services.Media;

public class ConversationStateService(IMemoryCache cache)
{
    private static string StateKey(long userId) => $"media:state:{userId}";
    private static string ResultsKey(long userId) => $"media:results:{userId}";

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public MediaConversationState GetState(long userId) =>
        cache.TryGetValue(StateKey(userId), out MediaConversationState state)
            ? state
            : MediaConversationState.Idle;

    public void SetState(long userId, MediaConversationState state) =>
        cache.Set(StateKey(userId), state, Ttl);

    public void Clear(long userId)
    {
        cache.Remove(StateKey(userId));
        cache.Remove(ResultsKey(userId));
    }

    public void SetSearchResults(long userId, IReadOnlyList<MediaSearchResultItem> results) =>
        cache.Set(ResultsKey(userId), results, Ttl);

    public IReadOnlyList<MediaSearchResultItem>? GetSearchResults(long userId) =>
        cache.TryGetValue(ResultsKey(userId), out IReadOnlyList<MediaSearchResultItem>? results)
            ? results
            : null;

    public void RefreshSearchResults(long userId)
    {
        if (cache.TryGetValue(ResultsKey(userId), out IReadOnlyList<MediaSearchResultItem>? results) && results is not null)
            cache.Set(ResultsKey(userId), results, Ttl);
    }
}
