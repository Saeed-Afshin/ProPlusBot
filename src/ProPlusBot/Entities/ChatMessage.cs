namespace ProPlusBot.Entities;

public class ChatMessage
{
    public long Id { get; set; }
    public long TelegramUserId { get; set; }
    public MessageDirection Direction { get; set; }
    public string? Text { get; set; }
    public string MessageType { get; set; } = "text";
    public int? TelegramMessageId { get; set; }
    public string? RawPayload { get; set; }
    public DateTime CreatedAt { get; set; }

    public BotUser User { get; set; } = null!;
}
