using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using Telegram.Bot;

namespace ProPlusBot.Services;

public class BaleBotClientFactory(IOptions<BotOptions> options)
{
    private readonly BotOptions _options = options.Value;

    public ITelegramBotClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
            throw new InvalidOperationException("Bot token is not configured.");

        var clientOptions = new TelegramBotClientOptions(_options.Token, _options.BaleApiBaseUrl);
        return new TelegramBotClient(clientOptions);
    }
}
