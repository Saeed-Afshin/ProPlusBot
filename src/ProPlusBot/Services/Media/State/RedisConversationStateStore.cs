using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using StackExchange.Redis;

namespace ProPlusBot.Services.Media.State;

public sealed class RedisConversationStateStore : IConversationStateStore, IDisposable
{
    private readonly string _keyPrefix;
    private readonly ILogger<RedisConversationStateStore> _logger;
    private readonly object _connectLock = new();
    private IConnectionMultiplexer? _multiplexer;

    public RedisConversationStateStore(
        IOptions<RedisOptions> redisOptions,
        ILogger<RedisConversationStateStore> logger)
    {
        _logger = logger;
        _keyPrefix = string.IsNullOrWhiteSpace(redisOptions.Value.InstanceName)
            ? "ProPlusBot:"
            : redisOptions.Value.InstanceName;
        ConnectionString = redisOptions.Value.ConnectionString?.Trim() ?? "";
    }

    public string ConnectionString { get; }

    public bool IsAvailable
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                return false;

            try
            {
                return GetDatabase() is not null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis is not available for conversation state");
                return false;
            }
        }
    }

    public T? Get<T>(string key) where T : class
    {
        var db = GetDatabase();
        if (db is null)
            return null;

        var payload = db.StringGet(Prefixed(key));
        if (payload.IsNullOrEmpty)
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(
                payload.ToString(),
                ConversationStateJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize conversation state key {Key}", key);
            db.KeyDelete(Prefixed(key));
            return null;
        }
    }

    public void Set<T>(string key, T value, TimeSpan ttl) where T : class
    {
        var db = GetDatabase();
        if (db is null)
            throw new InvalidOperationException("Redis is not configured or not reachable.");

        var json = System.Text.Json.JsonSerializer.Serialize(value, ConversationStateJson.Options);
        if (!db.StringSet(Prefixed(key), json, ttl))
            throw new InvalidOperationException($"Failed to write conversation state key {key} to Redis.");
    }

    public void Remove(string key)
    {
        var db = GetDatabase();
        db?.KeyDelete(Prefixed(key));
    }

    private IDatabase? GetDatabase()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return null;

        if (_multiplexer is { IsConnected: true })
            return _multiplexer.GetDatabase();

        lock (_connectLock)
        {
            if (_multiplexer is { IsConnected: true })
                return _multiplexer.GetDatabase();

            _multiplexer?.Dispose();
            _multiplexer = ConnectionMultiplexer.Connect(ConnectionString);
            return _multiplexer.GetDatabase();
        }
    }

    private string Prefixed(string key) => $"{_keyPrefix}{key}";

    public void Dispose() => _multiplexer?.Dispose();
}
