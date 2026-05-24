using ProPlusBot.Services;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ProPlusBot.Services.Tickets;

public class TicketBotHandler(
    TicketService ticketService,
    ConversationStateService conversationState)
{
    public static bool IsSupportButton(string? text) =>
        string.Equals(text?.Trim(), TicketConstants.SupportButtonText, StringComparison.Ordinal);

    public async Task<bool> TryHandleSupportButtonAsync(
        ITelegramBotClient bot,
        Message message,
        Func<ITelegramBotClient, long, string, CancellationToken, Task> sendAsync,
        CancellationToken ct)
    {
        if (!IsSupportButton(message.Text))
            return false;

        var userId = message.From!.Id;
        conversationState.Clear(userId);

        var open = await ticketService.GetOpenTicketAsync(userId, ct);
        if (open is not null)
        {
            conversationState.SetState(userId, MediaConversationState.AwaitingTicketMessage);
            await sendAsync(bot, userId,
                $"{TicketConstants.SupportHeader}\nتیکت باز دارید. پیام خود را بنویسید تا برای پشتیبانی ارسال شود.",
                ct);
            return true;
        }

        var (allowed, quotaMessage) = await ticketService.CanOpenNewTicketAsync(userId, ct);
        if (!allowed)
        {
            await sendAsync(bot, userId, quotaMessage!, ct);
            return true;
        }

        conversationState.SetState(userId, MediaConversationState.AwaitingTicketMessage);
        await sendAsync(bot, userId,
            $"{TicketConstants.SupportHeader}\nپیام خود را بنویسید.",
            ct);
        return true;
    }

    public async Task<bool> TryHandleMessageAsync(
        ITelegramBotClient bot,
        Message message,
        long? incomingChatMessageId,
        Func<ITelegramBotClient, long, string, CancellationToken, Task> sendAsync,
        CancellationToken ct)
    {
        if (IsMenuButton(message.Text))
            return false;

        var userId = message.From!.Id;
        if (conversationState.GetState(userId) != MediaConversationState.AwaitingTicketMessage)
            return false;

        var open = await ticketService.GetOpenTicketAsync(userId, ct);
        var body = ExtractMessageBody(message);
        if (string.IsNullOrWhiteSpace(body))
        {
            await sendAsync(bot, userId,
                $"{TicketConstants.SupportHeader}\nلطفاً پیام خود را به صورت متن ارسال کنید.",
                ct);
            return true;
        }

        if (open is null)
        {
            var (allowed, quotaMessage) = await ticketService.CanOpenNewTicketAsync(userId, ct);
            if (!allowed)
            {
                conversationState.SetState(userId, MediaConversationState.Idle);
                await sendAsync(bot, userId, quotaMessage!, ct);
                return true;
            }

            await ticketService.CreateTicketWithMessageAsync(userId, body, incomingChatMessageId, ct);
            conversationState.SetState(userId, MediaConversationState.Idle);
            await sendAsync(bot, userId,
                $"{TicketConstants.SupportHeader}\nپیام شما ثبت شد. به‌زودی پاسخ می‌دهیم.",
                ct);
            return true;
        }

        await ticketService.AddUserMessageAsync(open.Id, userId, body, incomingChatMessageId, ct);
        conversationState.SetState(userId, MediaConversationState.Idle);
        await sendAsync(bot, userId,
            $"{TicketConstants.SupportHeader}\nپیام شما به تیکت باز اضافه شد.",
            ct);
        return true;
    }

    private static string? ExtractMessageBody(Message message) =>
        message.Text?.Trim()
        ?? message.Caption?.Trim();

    private static bool IsMenuButton(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var t = text.Trim();
        return IsSupportButton(t)
            || t == BotFeatureService.HelpButtonText
            || t == SubscriptionBotHandler.AccountButtonText
            || t == SubscriptionBotHandler.PlansButtonText
            || t == MediaConstants.YouTubeSearchButtonText
            || t == MediaConstants.PinterestSearchButtonText
            || t == UserAccessService.RestartButtonText
            || UserAccessService.IsStartCommand(t);
    }
}
