namespace ProPlusBot.Entities;

public class ErrorLog
{
    public Guid Id { get; set; }
    public long? TelegramUserId { get; set; }
    public string? PhoneNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
}
