using ProPlusBot.Services;

namespace ProPlusBot.Services.Media;

public sealed class ConversationStateBackendBootstrap(
    IServiceScopeFactory scopeFactory,
    State.ConversationStateBackendHolder backendHolder) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = await scope.ServiceProvider
            .GetRequiredService<BotSettingsService>()
            .GetAsync(cancellationToken);
        backendHolder.Set(settings.ConversationStateBackend);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
