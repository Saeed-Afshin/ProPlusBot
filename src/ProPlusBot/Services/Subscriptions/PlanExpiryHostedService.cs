using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public class PlanExpiryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PlanExpiryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredPlansAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plan expiry job failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessExpiredPlansAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var planLifecycle = scope.ServiceProvider.GetRequiredService<PlanLifecycleService>();

        var now = DateTime.UtcNow;
        var expiredUserIds = await db.BotUsers.AsNoTracking()
            .Where(u => u.Plan != SubscriptionPlan.Free
                && u.PlanExpiresAt != null
                && u.PlanExpiresAt < now)
            .Select(u => u.TelegramUserId)
            .ToListAsync(ct);

        foreach (var userId in expiredUserIds)
            await planLifecycle.EnsurePlanStateCurrentAsync(userId, ct);
    }
}
