namespace ProPlusBot.Services;

public sealed class AdminSettingsCacheBootstrap(AdminSettingsCache cache) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        cache.RefreshAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
