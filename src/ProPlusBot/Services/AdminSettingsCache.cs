using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services;

public sealed class AdminSettingsCache(
    IMemoryCache memoryCache,
    IServiceScopeFactory scopeFactory,
    IOptions<AdminSettingsCacheOptions> cacheOptions,
    ILogger<AdminSettingsCache> logger)
{
    private const string CacheKey = "admin-settings-v1";
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(Math.Max(1, cacheOptions.Value.TtlMinutes));

    public Task<AdminSettingsSnapshot> GetAsync(CancellationToken ct = default) =>
        memoryCache.TryGetValue(CacheKey, out AdminSettingsSnapshot? snapshot) && snapshot is not null
            ? Task.FromResult(snapshot)
            : RefreshAsync(ct);

    public async Task<AdminSettingsSnapshot> RefreshAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bot = await db.BotSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct)
            ?? CreateDefaultBotSetting();

        NormalizeBot(bot);

        var trial = await db.TrialSettings.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new TrialSettings { Id = 1, DurationDays = 7, UpdatedAt = DateTime.UtcNow };

        var planRows = await db.PlanPricings.AsNoTracking().ToListAsync(ct);
        EnsureAllPlans(planRows);

        var snapshot = AdminSettingsSnapshot.Create(bot, trial, planRows);

        memoryCache.Set(CacheKey, snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttl
        });

        logger.LogDebug(
            "Admin settings cached (TTL {TtlMinutes}m, bot active={Active}, {PlanCount} plans)",
            cacheOptions.Value.TtlMinutes,
            snapshot.Bot.IsActive,
            snapshot.Plans.Count);

        return snapshot;
    }

    private static BotSetting CreateDefaultBotSetting() => new()
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
        ConversationStateBackend = ConversationStateBackend.Memory,
        BaleDirectArvanThresholdBytes = UploadFallbackPresets.DefaultMinBytes,
        UpdatedAt = DateTime.UtcNow
    };

    private static void NormalizeBot(BotSetting settings)
    {
        var (cols, rows) = SearchGridPresets.Normalize(settings.SearchGridColumns, settings.SearchGridRows);
        settings.SearchGridColumns = cols;
        settings.SearchGridRows = rows;
        settings.SearchGridJpegQuality = SearchGridPresets.NormalizeJpegQuality(settings.SearchGridJpegQuality);
        settings.BaleDirectArvanThresholdBytes = UploadFallbackPresets.NormalizeMinBytes(settings.BaleDirectArvanThresholdBytes);
    }

    private static void EnsureAllPlans(List<PlanPricing> planRows)
    {
        foreach (var seed in SubscriptionSeedData.DefaultPlanDefinitions())
        {
            if (planRows.All(p => p.Plan != seed.Plan))
                planRows.Add(seed);
        }
    }
}
