using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services.Tickets;

public class TicketService(
    AppDbContext db,
    QuotaService quotaService,
    UserAccessService userAccess)
{
    public async Task<SupportTicket?> GetOpenTicketAsync(long telegramUserId, CancellationToken ct = default) =>
        await db.SupportTickets
            .FirstOrDefaultAsync(t =>
                t.TelegramUserId == telegramUserId && t.Status != SupportTicketStatus.Closed, ct);

    public async Task<int> GetMonthlyTicketLimitAsync(long telegramUserId, CancellationToken ct = default)
    {
        if (await userAccess.IsPrivilegedUserAsync(telegramUserId, ct))
            return 0;

        var plan = await quotaService.GetEffectivePlanAsync(telegramUserId, ct);
        var row = await db.PlanPricings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Plan == plan, ct);

        return row?.MonthlyTicketLimit ?? 0;
    }

    public async Task<int> CountTicketsOpenedSinceAsync(
        long telegramUserId,
        DateTime sinceUtc,
        CancellationToken ct = default) =>
        await db.SupportTickets.AsNoTracking()
            .Where(t => t.TelegramUserId == telegramUserId && t.CreatedAt >= sinceUtc)
            .CountAsync(ct);

    public async Task<(bool Allowed, string? Message)> CanOpenNewTicketAsync(
        long telegramUserId,
        CancellationToken ct = default)
    {
        if (await userAccess.IsPrivilegedUserAsync(telegramUserId, ct))
            return (true, null);

        var open = await GetOpenTicketAsync(telegramUserId, ct);
        if (open is not null)
            return (true, null);

        var limit = await GetMonthlyTicketLimitAsync(telegramUserId, ct);
        if (limit <= 0)
            return (true, null);

        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        var since = user is null ? DateTime.UtcNow : QuotaPeriodHelper.GetPeriodStartUtc(user);
        var used = await CountTicketsOpenedSinceAsync(telegramUserId, since, ct);

        if (used >= limit)
        {
            return (false,
                $"سقف تعداد تیکت پشتیبانی در این ماه ({limit}) تمام شده است. پس از پایان دوره سهمیه می‌توانید تیکت جدید باز کنید.");
        }

        return (true, null);
    }

    public async Task<SupportTicket> CreateTicketWithMessageAsync(
        long telegramUserId,
        string body,
        long? incomingChatMessageId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            TelegramUserId = telegramUserId,
            Status = SupportTicketStatus.Created,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.SupportTickets.Add(ticket);
        await AddMessageCoreAsync(ticket, SupportTicketSender.User, body, null, null, incomingChatMessageId, ct);
        return ticket;
    }

    public async Task<SupportTicketMessage> AddUserMessageAsync(
        Guid ticketId,
        long telegramUserId,
        string body,
        long? incomingChatMessageId,
        CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("تیکت یافت نشد.");

        if (!TicketStatusRules.IsOpen(ticket.Status))
            throw new InvalidOperationException("این تیکت بسته شده است.");

        return await AddMessageCoreAsync(ticket, SupportTicketSender.User, body, null, null, incomingChatMessageId, ct);
    }

    public async Task<SupportTicketMessage> AddAdminReplyAsync(
        Guid ticketId,
        Guid? adminUserId,
        string? adminDisplayName,
        string body,
        CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException("تیکت یافت نشد.");

        if (!TicketStatusRules.IsOpen(ticket.Status))
            throw new InvalidOperationException("تیکت بسته است.");

        ticket.LastAdminReplyAt = DateTime.UtcNow;

        return await AddMessageCoreAsync(
            ticket,
            SupportTicketSender.Admin,
            body,
            adminUserId,
            adminDisplayName,
            null,
            ct);
    }

    public async Task CloseTicketAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException("تیکت یافت نشد.");

        if (ticket.Status == SupportTicketStatus.Closed)
            return;

        var now = DateTime.UtcNow;
        ticket.Status = SupportTicketStatus.Closed;
        ticket.ClosedAt = now;
        ticket.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<long>> AutoCloseExpiredTicketsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-TicketConstants.AutoCloseDaysAfterAdminReply);
        var expired = await db.SupportTickets
            .Where(t => t.Status == SupportTicketStatus.WaitingForUser
                && t.LastAdminReplyAt != null
                && t.LastAdminReplyAt < cutoff)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return [];

        var now = DateTime.UtcNow;
        foreach (var ticket in expired)
        {
            ticket.Status = SupportTicketStatus.Closed;
            ticket.ClosedAt = now;
            ticket.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return expired.Select(t => t.TelegramUserId).Distinct().ToList();
    }

    private async Task<SupportTicketMessage> AddMessageCoreAsync(
        SupportTicket ticket,
        SupportTicketSender sender,
        string body,
        Guid? adminUserId,
        string? adminDisplayName,
        long? incomingChatMessageId,
        CancellationToken ct)
    {
        var trimmed = body.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException("متن پیام خالی است.");

        var message = new SupportTicketMessage
        {
            TicketId = ticket.Id,
            Sender = sender,
            Body = trimmed.Length > 4096 ? trimmed[..4096] : trimmed,
            AdminUserId = adminUserId,
            AdminDisplayName = sender == SupportTicketSender.Admin
                ? TrimAdminDisplayName(adminDisplayName)
                : null,
            IncomingChatMessageId = incomingChatMessageId,
            CreatedAt = DateTime.UtcNow
        };

        ticket.UpdatedAt = message.CreatedAt;
        await ApplyStatusAfterMessageAsync(ticket, sender, ct);
        db.SupportTicketMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }

    private async Task ApplyStatusAfterMessageAsync(
        SupportTicket ticket,
        SupportTicketSender sender,
        CancellationToken ct)
    {
        if (ticket.Status == SupportTicketStatus.Closed)
            return;

        if (sender == SupportTicketSender.Admin)
        {
            ticket.Status = SupportTicketStatus.WaitingForUser;
            return;
        }

        var hasAdminReply = await db.SupportTicketMessages.AsNoTracking()
            .AnyAsync(m => m.TicketId == ticket.Id && m.Sender == SupportTicketSender.Admin, ct);

        ticket.Status = hasAdminReply
            ? SupportTicketStatus.WaitingForAdmin
            : SupportTicketStatus.Created;
    }

    private static string? TrimAdminDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim();
        return trimmed.Length > 128 ? trimmed[..128] : trimmed;
    }
}
