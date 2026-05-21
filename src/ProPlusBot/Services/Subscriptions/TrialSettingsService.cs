using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public class TrialSettingsService(AppDbContext db)
{
    public async Task<TrialSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.TrialSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not null)
            return settings;

        settings = new TrialSettings
        {
            Id = 1,
            DurationDays = 7,
            UpdatedAt = DateTime.UtcNow
        };
        db.TrialSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
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
    }
}
