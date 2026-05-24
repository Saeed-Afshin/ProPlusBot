namespace ProPlusBot.Services.Tickets;

public class TicketAutoCloseHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TicketAutoCloseHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ticket auto-close job failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
        var notifications = scope.ServiceProvider.GetRequiredService<TicketNotificationService>();

        var userIds = await ticketService.AutoCloseExpiredTicketsAsync(ct);
        if (userIds.Count == 0)
            return;

        logger.LogInformation("Auto-closed support tickets for {Count} users", userIds.Count);

        foreach (var userId in userIds)
        {
            try
            {
                await notifications.NotifyTicketClosedAsync(userId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify user {UserId} about ticket auto-close", userId);
            }
        }
    }
}
