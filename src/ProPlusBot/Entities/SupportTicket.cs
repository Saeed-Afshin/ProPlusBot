namespace ProPlusBot.Entities;

public class SupportTicket
{
    public Guid Id { get; set; }
    public long TelegramUserId { get; set; }
    public SupportTicketStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? LastAdminReplyAt { get; set; }

    public BotUser User { get; set; } = null!;
    public ICollection<SupportTicketMessage> Messages { get; set; } = [];
}
