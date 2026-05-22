using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Entities;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ProPlusBot.Services;

public class BotHostedService(
    IServiceScopeFactory scopeFactory,
    BaleBotClientFactory clientFactory,
    IOptions<BotOptions> botOptions,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration,
    ILogger<BotHostedService> logger) : BackgroundService
{
    private static readonly UpdateType[] PaymentUpdateTypes =
    [
        UpdateType.Message,
        UpdateType.CallbackQuery,
        UpdateType.PreCheckoutQuery
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        logger.LogInformation(
            "Bale bot service starting ({Environment}, db {Database}, token {BotToken})",
            hostEnvironment.EnvironmentName,
            ConfigurationValidation.DescribeConnection(configuration.GetConnectionString("DefaultConnection")),
            ConfigurationValidation.MaskBotToken(botOptions.Value.Token));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<BotSettingsService>();
                var settings = await settingsService.GetAsync(stoppingToken);

                if (!settings.IsActive)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                if (settings.UpdateMode == BotUpdateMode.Webhook)
                {
                    await ConfigureWebhookAsync(settings, stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                await RunLongPollingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Bot hosted service error");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task RunLongPollingAsync(CancellationToken ct)
    {
        var bot = clientFactory.CreateClient();
        await bot.DeleteWebhook(cancellationToken: ct);
        logger.LogInformation("Webhook cleared; starting Bale bot long polling");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = PaymentUpdateTypes
        };

        bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
        try
        {
            await handler.HandleUpdateAsync(update, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Bale bot polling error");
        return Task.CompletedTask;
    }

    private async Task ConfigureWebhookAsync(BotSetting settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.WebhookUrl))
            return;

        var bot = clientFactory.CreateClient();
        var secret = botOptions.Value.WebhookSecret;
        await bot.SetWebhook(
            settings.WebhookUrl,
            secretToken: secret,
            allowedUpdates: PaymentUpdateTypes,
            cancellationToken: ct);
        logger.LogInformation("Webhook configured to {Url}", settings.WebhookUrl);
    }
}
