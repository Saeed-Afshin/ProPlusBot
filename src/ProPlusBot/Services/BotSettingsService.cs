using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Services;

public class BotSettingsService(AppDbContext db, YouTubeCookiesProvider cookiesProvider)
{
    public async Task<BotSetting> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.BotSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not null)
            return Normalize(settings);

        settings = new BotSetting
        {
            Id = 1,
            Mode = BotMode.Live,
            UpdateMode = BotUpdateMode.LongPolling,
            IsActive = true,
            YouTubeEnabled = true,
            PinterestEnabled = true,
            SearchGridColumns = 3,
            SearchGridRows = 3,
            SearchGridJpegQuality = SearchGridPresets.DefaultJpegQuality,
            UpdatedAt = DateTime.UtcNow
        };
        db.BotSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
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
        Guid? updatedByAdminId,
        CancellationToken ct = default)
    {
        var settings = await db.BotSettings.FirstOrDefaultAsync(ct)
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

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = updatedByAdminId;
        await db.SaveChangesAsync(ct);
        return Normalize(settings);
    }

    public async Task<(bool Success, string? ErrorMessage, string? WarningMessage)> UpdateYouTubeCookiesAsync(
        string? paste,
        bool clear,
        Guid? updatedByAdminId,
        CancellationToken ct = default)
    {
        var settings = await db.BotSettings.FirstOrDefaultAsync(ct)
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

            return (true, null, warning);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = updatedByAdminId;
        await db.SaveChangesAsync(ct);
        return (true, null, null);
    }

    public async Task<(bool Configured, DateTime? UpdatedAt)> GetYouTubeCookiesStatusAsync(CancellationToken ct = default)
    {
        var row = await db.BotSettings
            .AsNoTracking()
            .Select(s => new { s.YouTubeCookiesContent, s.YouTubeCookiesUpdatedAt })
            .FirstOrDefaultAsync(ct);

        return (
            !string.IsNullOrWhiteSpace(row?.YouTubeCookiesContent),
            row?.YouTubeCookiesUpdatedAt);
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

    private static BotSetting Normalize(BotSetting settings)
    {
        var (cols, rows) = SearchGridPresets.Normalize(settings.SearchGridColumns, settings.SearchGridRows);
        settings.SearchGridColumns = cols;
        settings.SearchGridRows = rows;
        settings.SearchGridJpegQuality = SearchGridPresets.NormalizeJpegQuality(settings.SearchGridJpegQuality);
        return settings;
    }
}
