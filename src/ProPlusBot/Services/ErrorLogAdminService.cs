using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Models;

namespace ProPlusBot.Services;

public class ErrorLogAdminService(AppDbContext db)
{
    public async Task<List<ErrorLogListItemDto>> ListAsync(int take = 200, CancellationToken ct = default) =>
        await (
            from e in db.ErrorLogs.AsNoTracking()
            join u in db.BotUsers.AsNoTracking() on e.TelegramUserId equals u.TelegramUserId into userJoin
            from u in userJoin.DefaultIfEmpty()
            orderby e.CreatedAt descending
            select new ErrorLogListItemDto(
                e.Id,
                e.TelegramUserId,
                u != null ? u.Username : null,
                u != null
                    ? (string.IsNullOrWhiteSpace(u.FirstName)
                        ? null
                        : (u.FirstName + (u.LastName != null ? " " + u.LastName : "")).Trim())
                    : null,
                e.PhoneNumber ?? (u != null ? u.PhoneNumber : null),
                e.Title,
                e.Service,
                e.Source,
                e.CreatedAt))
            .Take(take)
            .ToListAsync(ct);

    public async Task<ErrorLogDetailDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.ErrorLogs.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new ErrorLogDetailDto(
                e.Id,
                e.TelegramUserId,
                e.PhoneNumber,
                e.Title,
                e.Detail,
                e.Service,
                e.Source,
                e.CreatedAt))
            .FirstOrDefaultAsync(ct);
}
