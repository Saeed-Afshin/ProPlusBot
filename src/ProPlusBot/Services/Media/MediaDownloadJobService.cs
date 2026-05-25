using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Media;

public class MediaDownloadJobService(AppDbContext db)
{
    public async Task<Guid> CreatePendingAsync(MediaDownloadJob job, long? incomingChatMessageId, CancellationToken ct = default)
    {
        var entity = new MediaDownloadJobEntity
        {
            Id = Guid.NewGuid(),
            TelegramUserId = job.ChatId,
            SourceUrl = job.SourceUrl,
            Platform = job.Platform,
            Source = job.Source,
            YouTubeFormatId = job.YouTubeFormatId,
            Status = MediaDownloadJobStatus.Pending,
            IncomingChatMessageId = incomingChatMessageId,
            CreatedAt = DateTime.UtcNow
        };

        db.MediaDownloadJobs.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<MediaDownloadWorkItem?> TryMarkProcessingAsync(Guid jobId, CancellationToken ct = default)
    {
        var entity = await db.MediaDownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (entity is null || entity.Status is not (MediaDownloadJobStatus.Pending or MediaDownloadJobStatus.Processing))
            return null;

        if (entity.Status == MediaDownloadJobStatus.Pending)
        {
            entity.Status = MediaDownloadJobStatus.Processing;
            entity.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return ToWorkItem(entity);
    }

    public async Task MarkCompletedAsync(
        Guid jobId,
        long? fileSizeBytes,
        string resultSummary,
        CancellationToken ct = default)
    {
        var entity = await db.MediaDownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (entity is null)
            return;

        entity.Status = MediaDownloadJobStatus.Completed;
        entity.ResultSummary = resultSummary;
        entity.FileSizeBytes = fileSizeBytes;
        entity.CompletedAt = DateTime.UtcNow;
        entity.ErrorDetail = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(
        Guid jobId,
        string resultSummary,
        string? errorDetail = null,
        CancellationToken ct = default)
    {
        var entity = await db.MediaDownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (entity is null)
            return;

        entity.Status = MediaDownloadJobStatus.Failed;
        entity.ResultSummary = resultSummary;
        entity.ErrorDetail = errorDetail;
        entity.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> RecoverPendingJobIdsAsync(CancellationToken ct = default)
    {
        var stuck = await db.MediaDownloadJobs
            .Where(j => j.Status == MediaDownloadJobStatus.Processing)
            .ToListAsync(ct);

        foreach (var job in stuck)
        {
            job.Status = MediaDownloadJobStatus.Pending;
            job.StartedAt = null;
        }

        if (stuck.Count > 0)
            await db.SaveChangesAsync(ct);

        return await db.MediaDownloadJobs
            .AsNoTracking()
            .Where(j => j.Status == MediaDownloadJobStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.Id)
            .ToListAsync(ct);
    }

    public async Task<List<MediaDownloadJobListItemDto>> ListAsync(
        MediaDownloadJobStatus? status,
        long? telegramUserId,
        int take = 200,
        CancellationToken ct = default)
    {
        var query = db.MediaDownloadJobs.AsNoTracking();
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);
        if (telegramUserId.HasValue)
            query = query.Where(j => j.TelegramUserId == telegramUserId.Value);

        return await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(take)
            .Select(j => new MediaDownloadJobListItemDto(
                j.Id,
                j.TelegramUserId,
                j.User.Username,
                string.IsNullOrWhiteSpace(j.User.FirstName)
                    ? null
                    : (j.User.FirstName + (j.User.LastName != null ? " " + j.User.LastName : "")).Trim(),
                j.User.PhoneNumber,
                j.Platform,
                j.Source,
                j.SourceUrl,
                j.Status,
                j.ResultSummary,
                j.CreatedAt,
                j.CompletedAt))
            .ToListAsync(ct);
    }

    public async Task<MediaDownloadJobDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default) =>
        await db.MediaDownloadJobs.AsNoTracking()
            .Where(j => j.Id == id)
            .Select(j => new MediaDownloadJobDetailDto(
                j.Id,
                j.TelegramUserId,
                j.User.Username,
                string.IsNullOrWhiteSpace(j.User.FirstName)
                    ? null
                    : (j.User.FirstName + (j.User.LastName != null ? " " + j.User.LastName : "")).Trim(),
                j.User.PhoneNumber,
                j.Platform,
                j.Source,
                j.SourceUrl,
                j.YouTubeFormatId,
                j.Status,
                j.ResultSummary,
                j.ErrorDetail,
                j.FileSizeBytes,
                j.IncomingChatMessageId,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt))
            .FirstOrDefaultAsync(ct);

    private static MediaDownloadWorkItem ToWorkItem(MediaDownloadJobEntity entity) =>
        new(
            entity.Id,
            entity.TelegramUserId,
            entity.SourceUrl,
            entity.Platform,
            entity.Source,
            entity.YouTubeFormatId,
            entity.IncomingChatMessageId);
}
