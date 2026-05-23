using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Models;

namespace ProPlusBot.Services;

public class ErrorLogAdminService(AppDbContext db)
{
    public async Task<List<ErrorLogListItemDto>> ListAsync(int take = 200, CancellationToken ct = default) =>
        await db.ErrorLogs.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new ErrorLogListItemDto(
                e.Id,
                e.TelegramUserId,
                e.PhoneNumber,
                e.Title,
                e.Service,
                e.Source,
                e.CreatedAt))
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
