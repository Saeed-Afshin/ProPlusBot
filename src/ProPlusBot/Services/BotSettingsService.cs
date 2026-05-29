using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Services;

public class BotSettingsService(
    AppDbContext db,
    YouTubeCookiesProvider cookiesProvider,
    Media.State.ConversationStateBackendHolder conversationStateBackendHolder,
    AdminSettingsCache adminSettingsCache)
{
    public async Task<BotSetting> GetAsync(CancellationToken ct = default)
    {
        var snapshot = await adminSettingsCache.GetAsync(ct);
        conversationStateBackendHolder.Set(snapshot.Bot.ConversationStateBackend);
        return snapshot.Bot;
    }

    public async Task<BotSetting> UpdateAsync(
        BotMode? mode,
        BotUpdateMode? updateMode,
        bool? isActive,
        string? webhookUrl,
        bool? youtubeEnabled,
        bool? pinterestEnabled,
        int? searchGridColumns,
        int? searchGridRows,
        int? searchGridJpegQuality,
        ConversationStateBackend? conversationStateBackend,
        long? baleDirectArvanThresholdBytes,
        Guid? updatedByAdminId,
        CancellationToken ct = default)
    {
        var settings = await db.BotSettings.FirstOrDefaultAsync(s => s.Id == 1, ct)
            ?? new BotSetting { Id = 1 };

        if (settings.Id == 0)
            db.BotSettings.Add(settings);

        if (mode.HasValue)
            settings.Mode = mode.Value;
        if (updateMode.HasValue)
            settings.UpdateMode = updateMode.Value;
        if (isActive.HasValue)
            settings.IsActive = isActive.Value;
        if (webhookUrl is not null)
            settings.WebhookUrl = string.IsNullOrWhiteSpace(webhookUrl) ? null : webhookUrl.Trim();
        if (youtubeEnabled.HasValue)
            settings.YouTubeEnabled = youtubeEnabled.Value;
        if (pinterestEnabled.HasValue)
            settings.PinterestEnabled = pinterestEnabled.Value;

        if (searchGridColumns.HasValue || searchGridRows.HasValue)
        {
            var (cols, rows) = SearchGridPresets.Normalize(
                searchGridColumns ?? settings.SearchGridColumns,
                searchGridRows ?? settings.SearchGridRows);
            settings.SearchGridColumns = cols;
            settings.SearchGridRows = rows;
        }

        if (searchGridJpegQuality.HasValue)
            settings.SearchGridJpegQuality = SearchGridPresets.NormalizeJpegQuality(searchGridJpegQuality.Value);

        if (conversationStateBackend.HasValue)
            settings.ConversationStateBackend = conversationStateBackend.Value;

        if (baleDirectArvanThresholdBytes.HasValue)
            settings.BaleDirectArvanThresholdBytes = UploadFallbackPresets.NormalizeMinBytes(baleDirectArvanThresholdBytes.Value);

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = updatedByAdminId;
        await db.SaveChangesAsync(ct);

        var snapshot = await adminSettingsCache.RefreshAsync(ct);
        conversationStateBackendHolder.Set(snapshot.Bot.ConversationStateBackend);
        return snapshot.Bot;
    }

    public async Task UpdateBaleDirectArvanThresholdAsync(long bytes, Guid? updatedByAdminId, CancellationToken ct = default)
    {
        var settings = await db.BotSettings.FirstOrDefaultAsync(s => s.Id == 1, ct)
            ?? new BotSetting { Id = 1 };

        if (settings.Id == 0)
            db.BotSettings.Add(settings);

        settings.BaleDirectArvanThresholdBytes = UploadFallbackPresets.NormalizeMinBytes(bytes);
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = updatedByAdminId;
        await db.SaveChangesAsync(ct);
        await adminSettingsCache.RefreshAsync(ct);
    }

    public async Task<(bool Success, string? ErrorMessage, string? WarningMessage)> UpdateYouTubeCookiesAsync(
        string? paste,
        bool clear,
        Guid? updatedByAdminId,
        CancellationToken ct = default)
    {
        var settings = await db.BotSettings.FirstOrDefaultAsync(s => s.Id == 1, ct)
            ?? new BotSetting { Id = 1 };

        if (settings.Id == 0)
            db.BotSettings.Add(settings);

        if (clear)
        {
            settings.YouTubeCookiesContent = null;
            settings.YouTubeCookiesUpdatedAt = null;
            TryDeleteMaterializedCookieFile();
        }
        else if (!string.IsNullOrWhiteSpace(paste))
        {
            if (!YouTubeCookiesContentParser.TryNormalize(paste, out var normalized, out var error))
                return (false, error, null);

            settings.YouTubeCookiesContent = normalized;
            settings.YouTubeCookiesUpdatedAt = DateTime.UtcNow;
            var warning = YouTubeCookiesContentParser.GetWeakSessionWarning(normalized);
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedByAdminId = updatedByAdminId;
            await db.SaveChangesAsync(ct);

            var (matOk, _, matError) = await cookiesProvider.TryMaterializeAdminCookiesAsync(ct);
            if (!matOk)
            {
                return (false,
                    $"کوکی در پایگاه داده ذخیره شد اما نوشتن فایل روی سرور ناموفق بود: {matError}",
                    warning);
            }

            await adminSettingsCache.RefreshAsync(ct);
            return (true, null, warning);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = updatedByAdminId;
        await db.SaveChangesAsync(ct);
        await adminSettingsCache.RefreshAsync(ct);
        return (true, null, null);
    }

    public async Task<(bool Configured, DateTime? UpdatedAt)> GetYouTubeCookiesStatusAsync(CancellationToken ct = default)
    {
        var snapshot = await adminSettingsCache.GetAsync(ct);
        return (
            !string.IsNullOrWhiteSpace(snapshot.Bot.YouTubeCookiesContent),
            snapshot.Bot.YouTubeCookiesUpdatedAt);
    }

    private static void TryDeleteMaterializedCookieFile()
    {
        try
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "ProPlusBot",
                "youtube-cookies",
                YouTubeCookiesProvider.AdminCookiesFileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
