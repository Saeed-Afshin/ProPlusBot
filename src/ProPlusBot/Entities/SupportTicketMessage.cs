namespace ProPlusBot.Entities;

public class SupportTicketMessage
{
    public long Id { get; set; }
    public Guid TicketId { get; set; }
    public SupportTicketSender Sender { get; set; }
    public string Body { get; set; } = "";
    public Guid? AdminUserId { get; set; }
    /// <summary>Snapshot when replying (e.g. config super-admin without AdminUsers row).</summary>
    public string? AdminDisplayName { get; set; }
    public long? IncomingChatMessageId { get; set; }
    public DateTime CreatedAt { get; set; }

    public SupportTicket Ticket { get; set; } = null!;
    public AdminUser? AdminUser { get; set; }
}
