namespace ProPlusBot.Entities;

public class UserInteractionLog
{
    public long Id { get; set; }
    public long TelegramUserId { get; set; }
    public long? IncomingChatMessageId { get; set; }
    public Guid? MediaDownloadJobId { get; set; }
    public UserInteractionKind Kind { get; set; }
    public UserInteractionStatus Status { get; set; }
    public string InputSummary { get; set; } = "";
    public string? ResultSummary { get; set; }
    public DateTime CreatedAt { get; set; }

    public BotUser User { get; set; } = null!;
    public ChatMessage? IncomingChatMessage { get; set; }
    public MediaDownloadJobEntity? MediaDownloadJob { get; set; }
}
