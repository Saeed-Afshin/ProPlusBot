using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services;

public class BotUpdateHandler(
    BaleBotClientFactory clientFactory,
    ChatStorageService chatStorage,
    UserAccessService userAccess,
    BotSettingsService settingsService,
    ConversationStateService conversationState,
    MediaBotHandler mediaHandler,
    SubscriptionBotHandler subscriptionHandler,
    BalePaymentService paymentService,
    ErrorLogService errorLog,
    AppDbContext db,
    IOptions<BotOptions> botOptions,
    ILogger<BotUpdateHandler> logger)
{
    private readonly BotOptions _botOptions = botOptions.Value;

    public async Task HandleUpdateAsync(Update update, CancellationToken ct = default)
    {
        if (update.PreCheckoutQuery is not null)
        {
            var preCheckoutSettings = await settingsService.GetAsync(ct);
            if (!preCheckoutSettings.IsActive)
                return;

            if (!await userAccess.CanReceiveBotResponseAsync(update.PreCheckoutQuery.From.Id, ct))
                return;

            await paymentService.HandlePreCheckoutQueryAsync(update.PreCheckoutQuery, ct);
            return;
        }

        if (update.CallbackQuery is not null)
        {
            await HandleCallbackQueryAsync(update.CallbackQuery, ct);
            return;
        }

        if (update.Message is null || update.Message.From is null)
            return;

        var message = update.Message;

        if (message.SuccessfulPayment is not null)
        {
            await chatStorage.EnsureUserAsync(message.From, ct);
            var paymentSettings = await settingsService.GetAsync(ct);
            if (!paymentSettings.IsActive)
                return;

            if (!await userAccess.CanReceiveBotResponseAsync(message.From.Id, ct))
                return;

            await paymentService.HandleSuccessfulPaymentAsync(message, ct);
            return;
        }
        var userId = message.From.Id;
        var bot = clientFactory.CreateClient();

        await chatStorage.EnsureUserAsync(message.From, ct);
        await chatStorage.SaveIncomingAsync(message, ct);

        var settings = await settingsService.GetAsync(ct);
        if (!settings.IsActive)
            return;

        if (!await userAccess.CanReceiveBotResponseAsync(userId, ct))
            return;

        if (message.Contact is not null && message.Contact.UserId == userId)
        {
            await userAccess.UpdatePhoneAsync(userId, message.Contact.PhoneNumber, ct);
            var botUser = await db.BotUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.TelegramUserId == userId, ct);

            var roleNote = botUser?.ResolvedRole switch
            {
                AdminRole.SuperAdmin => " شما به عنوان سوپر ادمین شناسایی شدید.",
                AdminRole.Admin => " شما به عنوان ادمین شناسایی شدید.",
                AdminRole.Tester => " شما به عنوان تستر شناسایی شدید.",
                _ => string.Empty
            };

            await SendAndStoreAsync(bot, userId,
                $"شماره تماس ثبت شد.{roleNote} لطفاً عضویت خود را در کانال بررسی کنید.", ct);
            await VerifyOnboardingAsync(bot, userId, ct);
            return;
        }

        if (!await HasSharedPhoneAsync(userId, ct) && PhoneInputValidator.LooksLikePhoneNumber(message.Text))
        {
            await PromptPhoneKeyboardAsync(bot, userId,
                "ارسال شماره به صورت متن پذیرفته نیست. لطفاً فقط از دکمه «اشتراک شماره تماس» استفاده کنید.", ct);
            return;
        }

        if (UserAccessService.IsStartCommand(message.Text))
        {
            conversationState.Clear(userId);
            await VerifyOnboardingAsync(bot, userId, ct);
            return;
        }

        var privileged = await userAccess.IsPrivilegedUserAsync(userId, ct);
        if (!privileged && !await userAccess.HasCompletedOnboardingAsync(userId, ct))
        {
            if (await HasSharedPhoneAsync(userId, ct))
            {
                await SendAndStoreAsync(bot, userId,
                    $"لطفاً در کانال {_botOptions.RequiredChannelUsername} عضو شوید و «{UserAccessService.RestartButtonText}» را بزنید.", ct);
            }
            else
            {
                await PromptPhoneKeyboardAsync(bot, userId, userAccess.OnboardingMessage(), ct);
            }

            return;
        }

        if (!privileged && await IsBannedAsync(userId, ct))
        {
            await SendAndStoreAsync(bot, userId, "حساب شما مسدود شده است.", ct);
            return;
        }

        if (await subscriptionHandler.TryHandleMessageAsync(bot, message, ct))
            return;

        if (await mediaHandler.TryHandleMessageAsync(bot, message, ct))
            return;

        conversationState.Clear(userId);
        await SendAndStoreAsync(bot, userId, BotConversationHelper.StateResetMessage, ct);
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.From is null)
            return;

        var userId = callback.From.Id;
        var bot = clientFactory.CreateClient();

        try
        {
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AnswerCallbackQuery failed for user {UserId}", userId);
            await errorLog.LogExceptionAsync(
                userId,
                "خطا در پاسخ به دکمه",
                ex,
                nameof(BotUpdateHandler),
                ct);
        }

        await chatStorage.EnsureUserAsync(callback.From, ct);

        var settings = await settingsService.GetAsync(ct);
        if (!settings.IsActive || !await userAccess.CanReceiveBotResponseAsync(userId, ct))
            return;

        var privileged = await userAccess.IsPrivilegedUserAsync(userId, ct);
        if (!privileged && !await userAccess.HasCompletedOnboardingAsync(userId, ct))
            return;

        if (!privileged && await IsBannedAsync(userId, ct))
            return;

        if (IsMediaSearchCallback(callback.Data)
            && await mediaHandler.HandleCallbackQueryAsync(callback, ct))
            return;

        if (await subscriptionHandler.HandleCallbackQueryAsync(callback, ct))
            return;

        if (await mediaHandler.HandleCallbackQueryAsync(callback, ct))
            return;

        conversationState.Clear(userId);
        await SendAndStoreAsync(bot, userId, BotConversationHelper.StateResetMessage, ct);
    }

    private async Task VerifyOnboardingAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        if (settings.Mode == BotMode.Test && await userAccess.IsStaffUserAsync(userId, ct))
        {
            await SendAndStoreAsync(bot, userId,
                "حالت تست: پیام‌های شما (سوپر ادمین / ادمین / تستر) پردازش می‌شوند.",
                ct,
                replyMarkup: SubscriptionBotHandler.SubscriptionMainMenuKeyboard(),
                useAutoMarkup: false);
            return;
        }

        var botUser = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == userId, ct);

        if (botUser is not { HasSharedPhone: true })
        {
            await PromptPhoneKeyboardAsync(bot, userId, userAccess.OnboardingMessage(), ct);
            return;
        }

        var joined = await userAccess.CheckChannelMembershipAsync(bot, userId, ct);
        if (!joined)
        {
            await SendAndStoreAsync(bot, userId,
                $"هنوز در کانال عضو نشده‌اید. لطفاً در {_botOptions.RequiredChannelUsername} عضو شوید و «{UserAccessService.RestartButtonText}» را بزنید.",
                ct);
            return;
        }

        await SendAndStoreAsync(bot, userId,
            "همه شرایط تکمیل است. لینک یوتیوب یا پینترست بفرستید، یا از دکمه‌های جستجو استفاده کنید.",
            ct,
            replyMarkup: SubscriptionBotHandler.SubscriptionMainMenuKeyboard(),
            useAutoMarkup: false);
    }

    private async Task PromptPhoneKeyboardAsync(
        ITelegramBotClient bot,
        long userId,
        string text,
        CancellationToken ct)
    {
        await SendAndStoreAsync(bot, userId, text, ct, replyMarkup: null, useAutoMarkup: false);
        await bot.SendMessage(
            userId,
            "از دکمه زیر برای اشتراک شماره تماس استفاده کنید:",
            replyMarkup: UserAccessService.PhoneRequestKeyboard(),
            cancellationToken: ct);
    }

    private async Task<bool> HasSharedPhoneAsync(long userId, CancellationToken ct) =>
        await db.BotUsers.AsNoTracking()
            .AnyAsync(u => u.TelegramUserId == userId && u.HasSharedPhone, ct);

    private async Task<bool> IsBannedAsync(long userId, CancellationToken ct) =>
        await db.BotUsers.AsNoTracking()
            .AnyAsync(u => u.TelegramUserId == userId && u.IsBanned, ct);

    private static bool IsMediaSearchCallback(string? data) =>
        data is not null
        && (data.StartsWith(MediaConstants.CallbackYouTubePrefix, StringComparison.Ordinal)
            || data.StartsWith(MediaConstants.CallbackPinterestPrefix, StringComparison.Ordinal));

    private async Task<ReplyMarkup?> ResolveReplyMarkupAsync(long userId, CancellationToken ct)
    {
        if (!await HasSharedPhoneAsync(userId, ct))
            return null;

        return SubscriptionBotHandler.SubscriptionMainMenuKeyboard();
    }

    private async Task SendAndStoreAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken ct,
        ReplyMarkup? replyMarkup = null,
        bool useAutoMarkup = true)
    {
        try
        {
            if (useAutoMarkup && replyMarkup is null)
                replyMarkup = await ResolveReplyMarkupAsync(chatId, ct);

            var sent = await bot.SendMessage(chatId, text, replyMarkup: replyMarkup, cancellationToken: ct);
            await chatStorage.SaveOutgoingAsync(chatId, text, sent.MessageId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message to {ChatId}", chatId);
            await errorLog.LogExceptionAsync(
                chatId,
                "خطا در ارسال پیام به کاربر",
                ex,
                nameof(BotUpdateHandler),
                ct);
        }
    }
}
