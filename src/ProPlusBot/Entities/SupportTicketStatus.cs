namespace ProPlusBot.Entities;

/// <summary>Lifecycle of a support ticket (replaces legacy Open/Closed).</summary>
public enum SupportTicketStatus
{
    /// <summary>Open; no admin reply yet.</summary>
    Created = 0,

    /// <summary>Open; user sent the latest message (needs admin).</summary>
    WaitingForAdmin = 1,

    /// <summary>Open; admin sent the latest message.</summary>
    WaitingForUser = 2,

    Closed = 3
}
