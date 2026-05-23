using Microsoft.Extensions.Caching.Memory;

namespace ProPlusBot.Services.Media;

public class ConversationStateService(IMemoryCache cache)
{
    private static string StateKey(long userId) => $"media:state:{userId}";
    private static string SessionKey(long userId) => $"media:session:{userId}";
    private static string FormatSessionKey(long userId) => $"media:formats:{userId}";

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
        cache.Remove(SessionKey(userId));
        ClearFormatSession(userId);
    }

    public void ClearFormatSession(long userId) =>
        cache.Remove(FormatSessionKey(userId));

    public void SetFormatSession(long userId, YouTubeFormatSession session) =>
        cache.Set(FormatSessionKey(userId), session, Ttl);

    public YouTubeFormatSession? GetFormatSession(long userId) =>
        cache.TryGetValue(FormatSessionKey(userId), out YouTubeFormatSession? session)
            ? session
            : null;

    public void RefreshFormatSession(long userId)
    {
        if (cache.TryGetValue(FormatSessionKey(userId), out YouTubeFormatSession? session) && session is not null)
            cache.Set(FormatSessionKey(userId), session, Ttl);
    }

    public void SetSearchSession(long userId, MediaSearchSession session) =>
        cache.Set(SessionKey(userId), session, Ttl);

    public MediaSearchSession? GetSearchSession(long userId) =>
        cache.TryGetValue(SessionKey(userId), out MediaSearchSession? session)
            ? session
            : null;

    public void RefreshSearchSession(long userId)
    {
        if (cache.TryGetValue(SessionKey(userId), out MediaSearchSession? session) && session is not null)
            cache.Set(SessionKey(userId), session, Ttl);
    }
}
