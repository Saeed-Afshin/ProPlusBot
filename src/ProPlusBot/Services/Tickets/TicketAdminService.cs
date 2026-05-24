using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Tickets;

public class TicketAdminService(AppDbContext db)
{
    public async Task<List<SupportTicketListItemDto>> ListAsync(
        SupportTicketStatus? statusFilter,
        TicketListSort sort = TicketListSort.Priority,
        CancellationToken ct = default)
    {
        var query = db.SupportTickets.AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .AsQueryable();

        if (statusFilter is not null)
            query = query.Where(t => t.Status == statusFilter);

        var tickets = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Take(500)
            .ToListAsync(ct);
        tickets = ApplySort(tickets, sort);

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var lastAdminByTicket = await db.SupportTicketMessages.AsNoTracking()
            .Where(m => ticketIds.Contains(m.TicketId) && m.Sender == SupportTicketSender.Admin)
            .Include(m => m.AdminUser)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        var lastAdminLookup = lastAdminByTicket
            .GroupBy(m => m.TicketId)
            .ToDictionary(g => g.Key, g => g.First());

        return tickets.Select(t =>
        {
            var last = t.Messages.FirstOrDefault();
            var preview = last?.Body ?? "—";
            if (preview.Length > 80)
                preview = preview[..80] + "…";

            lastAdminLookup.TryGetValue(t.Id, out var lastAdmin);
            var lastAdminName = lastAdmin is null
                ? null
                : lastAdmin.AdminUser?.DisplayName ?? lastAdmin.AdminDisplayName;

            return new SupportTicketListItemDto(
                t.Id,
                t.TelegramUserId,
                t.User.Username,
                t.User.PhoneNumber,
                t.Status,
                t.CreatedAt,
                t.UpdatedAt,
                t.LastAdminReplyAt,
                lastAdminName,
                preview);
        }).ToList();
    }

    public async Task<SupportTicketDetailDto?> GetDetailAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets.AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Messages.OrderBy(m => m.CreatedAt))
            .ThenInclude(m => m.AdminUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket is null)
            return null;

        var messages = ticket.Messages.Select(m => new SupportTicketMessageDto(
            m.Id,
            m.Sender,
            m.Body,
            ResolveAdminDisplayName(m),
            m.CreatedAt)).ToList();

        var lastAdminName = messages
            .Where(m => m.Sender == SupportTicketSender.Admin)
            .Select(m => m.AdminDisplayName)
            .LastOrDefault(n => !string.IsNullOrWhiteSpace(n));

        return new SupportTicketDetailDto(
            ticket.Id,
            ticket.TelegramUserId,
            ticket.User.Username,
            ticket.User.PhoneNumber,
            ticket.Status,
            ticket.CreatedAt,
            ticket.ClosedAt,
            ticket.LastAdminReplyAt,
            lastAdminName,
            messages);
    }

    private static string? ResolveAdminDisplayName(SupportTicketMessage m) =>
        m.Sender == SupportTicketSender.Admin
            ? m.AdminUser?.DisplayName ?? m.AdminDisplayName
            : null;

    private static List<SupportTicket> ApplySort(List<SupportTicket> tickets, TicketListSort sort) =>
        sort switch
        {
            TicketListSort.UpdatedAsc => tickets.OrderBy(t => t.UpdatedAt).ToList(),
            TicketListSort.UpdatedDesc => tickets.OrderByDescending(t => t.UpdatedAt).ToList(),
            TicketListSort.CreatedAsc => tickets.OrderBy(t => t.CreatedAt).ToList(),
            TicketListSort.CreatedDesc => tickets.OrderByDescending(t => t.CreatedAt).ToList(),
            _ => tickets
                .OrderBy(t => TicketStatusRules.ListSortRank(t.Status))
                .ThenBy(t => t.Status != SupportTicketStatus.Closed ? t.UpdatedAt : DateTime.MaxValue)
                .ThenByDescending(t => t.Status == SupportTicketStatus.Closed ? t.UpdatedAt : DateTime.MinValue)
                .ToList()
        };
}
