using ProPlusBot.Entities;

namespace ProPlusBot.Services.Media.State;

public sealed class ConversationStateStoreProvider(
    ConversationStateBackendHolder backendHolder,
    MemoryConversationStateStore memory,
    RedisConversationStateStore redis,
    ILogger<ConversationStateStoreProvider> logger)
{
    public IConversationStateStore GetStore()
    {
        if (backendHolder.Backend == ConversationStateBackend.Redis)
        {
            if (redis.IsAvailable)
                return redis;

            logger.LogWarning(
                "Conversation state backend is Redis but Redis is unavailable; falling back to memory.");
        }

        return memory;
    }
}
