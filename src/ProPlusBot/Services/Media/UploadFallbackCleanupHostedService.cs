namespace ProPlusBot.Services.Media;

public class UploadFallbackCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<UploadFallbackCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Upload fallback cleanup failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task CleanupExpiredAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var fallbackUploads = scope.ServiceProvider.GetRequiredService<FallbackUploadService>();
        var storage = scope.ServiceProvider.GetRequiredService<ArvanCloudStorageService>();

        if (!storage.IsConfigured)
            return;

        const int batchSize = 50;
        var removed = 0;

        while (true)
        {
            var expired = await fallbackUploads.ListExpiredAsync(batchSize, ct);
            if (expired.Count == 0)
                break;

            foreach (var upload in expired)
            {
                await storage.DeleteAsync(upload.StorageObjectKey, ct);
                await fallbackUploads.RemoveAsync(upload.Id, ct);
                removed++;
            }

            if (expired.Count < batchSize)
                break;
        }

        if (removed > 0)
            logger.LogInformation("Removed {Count} expired fallback upload(s)", removed);
    }
}
