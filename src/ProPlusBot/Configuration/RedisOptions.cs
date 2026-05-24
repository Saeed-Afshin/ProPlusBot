namespace ProPlusBot.Configuration;

public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string, e.g. localhost:6379 or host:6379,password=...</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Optional key prefix for all conversation-state keys.</summary>
    public string InstanceName { get; set; } = "ProPlusBot:";
}
