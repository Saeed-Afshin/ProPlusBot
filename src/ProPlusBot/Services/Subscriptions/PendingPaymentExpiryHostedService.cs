using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public class PendingPaymentExpiryHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PaymentOptions> paymentOptions,
    ILogger<PendingPaymentExpiryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpirePendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pending payment expiry job failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task ExpirePendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hours = paymentOptions.Value.PendingPaymentExpiryHours;
        var cutoff = DateTime.UtcNow.AddHours(-hours);

        var expired = await db.PaymentRecords
            .Where(p => p.Status == PaymentStatus.Pending && p.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return;

        foreach (var payment in expired)
        {
            payment.Status = PaymentStatus.Cancelled;
            payment.Note = $"منقضی شده پس از {hours} ساعت";
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Cancelled {Count} expired pending payments", expired.Count);
    }
}
