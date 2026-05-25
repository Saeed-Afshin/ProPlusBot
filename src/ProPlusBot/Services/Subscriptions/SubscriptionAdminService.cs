using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public class SubscriptionAdminService(AppDbContext db)
{
    public async Task<List<PaymentRecordDto>> ListPaymentsAsync(int take = 200, CancellationToken ct = default) =>
        await db.PaymentRecords.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .Select(p => new PaymentRecordDto(
                p.Id,
                p.TelegramUserId,
                p.User.Username,
                string.IsNullOrWhiteSpace(p.User.FirstName)
                    ? null
                    : (p.User.FirstName + (p.User.LastName != null ? " " + p.User.LastName : "")).Trim(),
                p.User.PhoneNumber,
                p.Type,
                p.Status,
                p.AmountToman,
                p.FromPlan,
                p.ToPlan,
                p.Platform,
                p.CreatedAt,
                p.CompletedAt,
                p.Note))
            .ToListAsync(ct);

    public async Task<List<BotUserAdminDto>> ListUsersAsync(string? search, int take = 200, CancellationToken ct = default)
    {
        var query = db.BotUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            if (long.TryParse(search, out var id))
                query = query.Where(u => u.TelegramUserId == id);
            else
                query = query.Where(u =>
                    (u.Username != null && u.Username.Contains(search))
                    || (u.PhoneNumber != null && u.PhoneNumber.Contains(search))
                    || (u.FirstName != null && u.FirstName.Contains(search))
                    || (u.LastName != null && u.LastName.Contains(search)));
        }

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Take(take)
            .Select(u => new BotUserAdminDto(
                u.TelegramUserId,
                u.Username,
                string.IsNullOrWhiteSpace(u.FirstName)
                    ? null
                    : (u.FirstName + (u.LastName != null ? " " + u.LastName : "")).Trim(),
                u.PhoneNumber,
                u.Plan,
                u.PlanExpiresAt,
                u.IsBanned,
                u.CreatedAt))
            .ToListAsync(ct);
    }
}
