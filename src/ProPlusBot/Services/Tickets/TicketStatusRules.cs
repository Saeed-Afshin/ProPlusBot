using ProPlusBot.Entities;

namespace ProPlusBot.Services.Tickets;

public static class TicketStatusRules
{
    public static bool IsOpen(SupportTicketStatus status) =>
        status != SupportTicketStatus.Closed;

    public static bool NeedsAdminAttention(SupportTicketStatus status) =>
        status is SupportTicketStatus.Created or SupportTicketStatus.WaitingForAdmin;

    public static string ToDisplayName(SupportTicketStatus status) => status switch
    {
        SupportTicketStatus.Created => "ایجاد شده",
        SupportTicketStatus.WaitingForAdmin => "در انتظار پشتیبانی",
        SupportTicketStatus.WaitingForUser => "پاسخ داده شده",
        SupportTicketStatus.Closed => "بسته",
        _ => status.ToString()
    };

    public static string ToBadgeClass(SupportTicketStatus status) => status switch
    {
        SupportTicketStatus.Created => "bg-danger",
        SupportTicketStatus.WaitingForAdmin => "bg-warning text-dark",
        SupportTicketStatus.WaitingForUser => "bg-info text-dark",
        SupportTicketStatus.Closed => "bg-secondary",
        _ => "bg-secondary"
    };

    public static int ListSortRank(SupportTicketStatus status) => status switch
    {
        SupportTicketStatus.Created => 0,
        SupportTicketStatus.WaitingForAdmin => 0,
        SupportTicketStatus.WaitingForUser => 1,
        SupportTicketStatus.Closed => 2,
        _ => 2
    };
}

public enum TicketListSort
{
    /// <summary>Needs admin (oldest first), then waiting on user (oldest first), then closed (newest first).</summary>
    Priority = 0,
    UpdatedAsc = 1,
    UpdatedDesc = 2,
    CreatedAsc = 3,
    CreatedDesc = 4
}
