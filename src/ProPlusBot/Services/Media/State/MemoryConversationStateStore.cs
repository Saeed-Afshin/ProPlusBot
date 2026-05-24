using Microsoft.Extensions.Caching.Memory;

namespace ProPlusBot.Services.Media.State;

public sealed class MemoryConversationStateStore(IMemoryCache cache) : IConversationStateStore
{
    public bool IsAvailable => true;

    public T? Get<T>(string key) where T : class =>
        cache.TryGetValue(key, out T? value) ? value : null;

    public void Set<T>(string key, T value, TimeSpan ttl) where T : class =>
        cache.Set(key, value, ttl);

    public void Remove(string key) => cache.Remove(key);
}
