using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Tickets;

public class TicketNotificationService(
    BaleBotClientFactory clientFactory,
    ChatStorageService chatStorage,
    BotFeatureService botFeatures)
{
    public async Task NotifyUserAsync(long telegramUserId, string body, CancellationToken ct = default)
    {
        var bot = clientFactory.CreateClient();
        var text = $"{TicketConstants.SupportHeader}\n{body.Trim()}";
        var markup = await botFeatures.BuildMainMenuKeyboardAsync(telegramUserId, ct);
        var sent = await bot.SendMessage(telegramUserId, text, replyMarkup: markup, cancellationToken: ct);
        await chatStorage.SaveOutgoingAsync(telegramUserId, text, sent.MessageId, ct);
    }

    public async Task NotifyTicketClosedAsync(long telegramUserId, CancellationToken ct = default) =>
        await NotifyUserAsync(
            telegramUserId,
            "تیکت پشتیبانی شما بسته شد. برای ارسال درخواست جدید دوباره «🎫 پشتیبانی» را بزنید.",
            ct);
}
