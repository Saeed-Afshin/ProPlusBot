using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services;

public class ChatLogAdminService(AppDbContext db)
{
    public async Task<List<ChatUserSummaryDto>> ListConversationSummariesAsync(
        string? search,
        int take = 200,
        CancellationToken ct = default)
    {
        var userIdsQuery = db.ChatMessages.AsNoTracking().Select(m => m.TelegramUserId).Distinct();

        var usersQuery = db.BotUsers.AsNoTracking()
            .Where(u => userIdsQuery.Contains(u.TelegramUserId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            if (long.TryParse(q, out var userId))
                usersQuery = usersQuery.Where(u => u.TelegramUserId == userId);
            else
            {
                usersQuery = usersQuery.Where(u =>
                    (u.Username != null && u.Username.Contains(q))
                    || (u.PhoneNumber != null && u.PhoneNumber.Contains(q))
                    || (u.FirstName != null && u.FirstName.Contains(q))
                    || (u.LastName != null && u.LastName.Contains(q)));
            }
        }

        var matchingUserIds = await usersQuery.Select(u => u.TelegramUserId).ToListAsync(ct);
        if (matchingUserIds.Count == 0)
            return [];

        var stats = await db.ChatMessages.AsNoTracking()
            .Where(m => matchingUserIds.Contains(m.TelegramUserId))
            .GroupBy(m => m.TelegramUserId)
            .Select(g => new
            {
                TelegramUserId = g.Key,
                MessageCount = g.Count(),
                LastMessageAt = g.Max(m => m.CreatedAt)
            })
            .OrderByDescending(x => x.LastMessageAt)
            .Take(take)
            .ToListAsync(ct);

        if (stats.Count == 0)
            return [];

        var pageUserIds = stats.Select(s => s.TelegramUserId).ToList();

        var users = await db.BotUsers.AsNoTracking()
            .Where(u => pageUserIds.Contains(u.TelegramUserId))
            .ToDictionaryAsync(u => u.TelegramUserId, ct);

        var lastByUser = new Dictionary<long, (string? Text, MessageDirection Direction)>();
        foreach (var userId in pageUserIds)
        {
            var last = await db.ChatMessages.AsNoTracking()
                .Where(m => m.TelegramUserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Text, m.Direction })
                .FirstOrDefaultAsync(ct);
            if (last is not null)
                lastByUser[userId] = (last.Text, last.Direction);
        }

        return stats.Select(s =>
        {
            users.TryGetValue(s.TelegramUserId, out var user);
            lastByUser.TryGetValue(s.TelegramUserId, out var last);
            return new ChatUserSummaryDto(
                s.TelegramUserId,
                user?.Username,
                user?.FirstName,
                user?.LastName,
                user?.PhoneNumber,
                s.MessageCount,
                s.LastMessageAt,
                last.Text,
                last.Direction);
        }).ToList();
    }

    public async Task<ChatUserHeaderDto?> GetUserHeaderAsync(long telegramUserId, CancellationToken ct = default) =>
        await db.BotUsers.AsNoTracking()
            .Where(u => u.TelegramUserId == telegramUserId)
            .Select(u => new ChatUserHeaderDto(
                u.TelegramUserId,
                u.Username,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.Messages.Count()))
            .FirstOrDefaultAsync(ct);

    public async Task<int> CountMessagesAsync(long? telegramUserId, CancellationToken ct = default)
    {
        var query = db.ChatMessages.AsNoTracking();
        if (telegramUserId.HasValue)
            query = query.Where(m => m.TelegramUserId == telegramUserId.Value);

        return await query.CountAsync(ct);
    }

    public async Task<List<ChatMessageDto>> ListMessagesAsync(
        long? telegramUserId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.ChatMessages.AsNoTracking();
        if (telegramUserId.HasValue)
            query = query.Where(m => m.TelegramUserId == telegramUserId.Value);

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.TelegramUserId,
                m.Direction,
                m.Text,
                m.MessageType,
                m.CreatedAt))
            .ToListAsync(ct);
    }
}
