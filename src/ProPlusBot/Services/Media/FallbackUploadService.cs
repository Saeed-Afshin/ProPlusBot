using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services.Media;

public class FallbackUploadService(AppDbContext db)
{
    public async Task<FallbackUpload?> FindActiveByContentKeyAsync(string contentKey, CancellationToken ct = default) =>
        await db.FallbackUploads
            .AsNoTracking()
            .Where(u => u.ContentKey == contentKey && u.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(u => u.LastAccessedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<FallbackUpload> RegisterAsync(
        string contentKey,
        string sourceUrl,
        DetectedMediaPlatform platform,
        string? youTubeFormatId,
        string storageObjectKey,
        string publicUrl,
        long fileSizeBytes,
        string contentType,
        int expiryHours,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var entity = new FallbackUpload
        {
            Id = Guid.NewGuid(),
            ContentKey = contentKey,
            SourceUrl = sourceUrl,
            Platform = platform,
            YouTubeFormatId = youTubeFormatId,
            StorageObjectKey = storageObjectKey,
            PublicUrl = publicUrl,
            FileSizeBytes = fileSizeBytes,
            ContentType = contentType,
            CreatedAt = now,
            LastAccessedAt = now,
            ExpiresAt = now.AddHours(UploadFallbackPresets.NormalizeExpiryHours(expiryHours))
        };

        db.FallbackUploads.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task RenewExpiryAsync(Guid id, int expiryHours, CancellationToken ct = default)
    {
        var entity = await db.FallbackUploads.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (entity is null)
            return;

        var now = DateTime.UtcNow;
        entity.LastAccessedAt = now;
        entity.ExpiresAt = now.AddHours(UploadFallbackPresets.NormalizeExpiryHours(expiryHours));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FallbackUpload>> ListExpiredAsync(int take, CancellationToken ct = default) =>
        await db.FallbackUploads
            .Where(u => u.ExpiresAt <= DateTime.UtcNow)
            .OrderBy(u => u.ExpiresAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.FallbackUploads.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (entity is null)
            return;

        db.FallbackUploads.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
