using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services;

public class UserInteractionLogService(AppDbContext db)
{
    public async Task<long> LogAsync(
        long telegramUserId,
        UserInteractionKind kind,
        UserInteractionStatus status,
        string inputSummary,
        string? resultSummary = null,
        long? incomingChatMessageId = null,
        Guid? mediaDownloadJobId = null,
        CancellationToken ct = default)
    {
        var entry = new UserInteractionLog
        {
            TelegramUserId = telegramUserId,
            IncomingChatMessageId = incomingChatMessageId,
            MediaDownloadJobId = mediaDownloadJobId,
            Kind = kind,
            Status = status,
            InputSummary = Truncate(inputSummary, 512),
            ResultSummary = resultSummary is null ? null : Truncate(resultSummary, 512),
            CreatedAt = DateTime.UtcNow
        };

        db.UserInteractionLogs.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.Id;
    }

    public async Task UpdateByJobIdAsync(
        Guid mediaDownloadJobId,
        UserInteractionStatus status,
        string resultSummary,
        CancellationToken ct = default)
    {
        var rows = await db.UserInteractionLogs
            .Where(x => x.MediaDownloadJobId == mediaDownloadJobId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return;

        rows[0].Status = status;
        rows[0].ResultSummary = Truncate(resultSummary, 512);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<UserInteractionListItemDto>> ListAsync(
        long? telegramUserId,
        UserInteractionStatus? status,
        int take = 200,
        CancellationToken ct = default)
    {
        var query = db.UserInteractionLogs.AsNoTracking();
        if (telegramUserId.HasValue)
            query = query.Where(x => x.TelegramUserId == telegramUserId.Value);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new UserInteractionListItemDto(
                x.Id,
                x.TelegramUserId,
                x.User.Username,
                string.IsNullOrWhiteSpace(x.User.FirstName)
                    ? null
                    : (x.User.FirstName + (x.User.LastName != null ? " " + x.User.LastName : "")).Trim(),
                x.User.PhoneNumber,
                x.IncomingChatMessageId,
                x.MediaDownloadJobId,
                x.Kind,
                x.Status,
                x.InputSummary,
                x.ResultSummary,
                x.CreatedAt))
            .ToListAsync(ct);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
