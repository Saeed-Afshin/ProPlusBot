namespace ProPlusBot.Configuration;

public class ConversationStateOptions
{
    public const string SectionName = "ConversationState";

    public int SessionTtlMinutes { get; set; } = 30;
}
