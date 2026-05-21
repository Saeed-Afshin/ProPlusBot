using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services;

public class BotSettingsService(AppDbContext db)
{
    public async Task<BotSetting> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.BotSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not null)
            return settings;

        settings = new BotSetting
        {
            Id = 1,
            Mode = BotMode.Live,
            UpdateMode = BotUpdateMode.LongPolling,
            IsActive = true,
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

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = updatedByAdminId;
        await db.SaveChangesAsync(ct);
        return settings;
    }
}
