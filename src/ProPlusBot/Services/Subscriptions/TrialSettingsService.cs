using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public class TrialSettingsService(AppDbContext db, AdminSettingsCache adminSettingsCache)
{
    public async Task<TrialSettings> GetAsync(CancellationToken ct = default)
    {
        var snapshot = await adminSettingsCache.GetAsync(ct);
        return snapshot.Trial;
    }

    public async Task UpdateDurationDaysAsync(int durationDays, CancellationToken ct = default)
    {
        var settings = await db.TrialSettings.FirstOrDefaultAsync(ct)
            ?? new TrialSettings { Id = 1 };

        if (settings.Id == 0)
            db.TrialSettings.Add(settings);

        settings.DurationDays = Math.Clamp(durationDays, 1, 365);
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await adminSettingsCache.RefreshAsync(ct);
    }
}
