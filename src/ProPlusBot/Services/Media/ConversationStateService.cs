using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Services.Media.State;

namespace ProPlusBot.Services.Media;

public class ConversationStateService(
    ConversationStateStoreProvider storeProvider,
    IOptions<ConversationStateOptions> options)
{
    private static string StateKey(long userId) => $"media:state:{userId}";
    private static string SessionKey(long userId) => $"media:session:{userId}";
    private static string FormatSessionKey(long userId) => $"media:formats:{userId}";

    private TimeSpan Ttl => TimeSpan.FromMinutes(Math.Clamp(options.Value.SessionTtlMinutes, 5, 1440));

    private IConversationStateStore Store => storeProvider.GetStore();

    public MediaConversationState GetState(long userId) =>
        Store.Get<ConversationStateEnvelope>(StateKey(userId))?.State ?? MediaConversationState.Idle;

    public void SetState(long userId, MediaConversationState state) =>
        Store.Set(StateKey(userId), new ConversationStateEnvelope(state), Ttl);

    public void Clear(long userId)
    {
        Store.Remove(StateKey(userId));
        Store.Remove(SessionKey(userId));
        ClearFormatSession(userId);
    }

    public void ClearFormatSession(long userId) =>
        Store.Remove(FormatSessionKey(userId));

    public void SetFormatSession(long userId, YouTubeFormatSession session) =>
        Store.Set(FormatSessionKey(userId), session, Ttl);

    public YouTubeFormatSession? GetFormatSession(long userId) =>
        Store.Get<YouTubeFormatSession>(FormatSessionKey(userId));

    public void RefreshFormatSession(long userId)
    {
        var session = GetFormatSession(userId);
        if (session is not null)
            SetFormatSession(userId, session);
    }

    public void SetSearchSession(long userId, MediaSearchSession session) =>
        Store.Set(SessionKey(userId), session, Ttl);

    public MediaSearchSession? GetSearchSession(long userId) =>
        Store.Get<MediaSearchSession>(SessionKey(userId));

    public void RefreshSearchSession(long userId)
    {
        var session = GetSearchSession(userId);
        if (session is not null)
            SetSearchSession(userId, session);
    }
}
