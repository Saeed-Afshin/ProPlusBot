using ProPlusBot.Services;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Media;

public class MediaBotHandler(
    ConversationStateService conversationState,
    MediaDownloadQueue downloadQueue,
    YtDlpService ytDlp,
    PinterestSearchService pinterestSearch,
    MediaFileSender fileSender,
    BaleBotClientFactory clientFactory,
    ChatStorageService chatStorage,
    QuotaService quotaService,
    UserAccessService userAccess,
    ILogger<MediaBotHandler> logger)
{
    public async Task<bool> HandleCallbackQueryAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.From is null || callback.Data is null)
            return false;

        var userId = callback.From.Id;
        var bot = clientFactory.CreateClient();

        var results = conversationState.GetSearchResults(userId);
        if (results is null || results.Count == 0)
        {
            conversationState.Clear(userId);
            await fileSender.SendTextAsync(bot, userId,
                "نتایج جستجو منقضی شده است. دوباره جستجو کنید.", ct);
            return true;
        }

        MediaSearchResultItem? selected = null;
        DetectedMediaPlatform platform;

        if (callback.Data.StartsWith(MediaConstants.CallbackYouTubePrefix, StringComparison.Ordinal))
        {
            platform = DetectedMediaPlatform.YouTube;
            if (!int.TryParse(callback.Data[MediaConstants.CallbackYouTubePrefix.Length..], out var index)
                || index < 0 || index >= results.Count)
            {
                conversationState.Clear(userId);
                await fileSender.SendTextAsync(bot, userId, BotConversationHelper.StateResetMessage, ct);
                return true;
            }

            selected = results[index];
        }
        else if (callback.Data.StartsWith(MediaConstants.CallbackPinterestPrefix, StringComparison.Ordinal))
        {
            platform = DetectedMediaPlatform.Pinterest;
            if (!int.TryParse(callback.Data[MediaConstants.CallbackPinterestPrefix.Length..], out var index)
                || index < 0 || index >= results.Count)
            {
                conversationState.Clear(userId);
                await fileSender.SendTextAsync(bot, userId, BotConversationHelper.StateResetMessage, ct);
                return true;
            }

            selected = results[index];
        }
        else
        {
            return false;
        }

        conversationState.RefreshSearchResults(userId);

        var source = platform == DetectedMediaPlatform.YouTube
            ? MediaDownloadSource.YouTubeSearch
            : MediaDownloadSource.PinterestSearch;

        await TryEnqueueDownloadAsync(bot, userId, selected.Url, platform, source, ct);
        return true;
    }

    public async Task<bool> TryHandleMessageAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text?.Trim();

        if (text == MediaConstants.YouTubeSearchButtonText)
        {
            conversationState.SetState(userId, MediaConversationState.AwaitingYouTubeQuery);
            await fileSender.SendTextAsync(bot, userId,
                "عبارت جستجو را برای یوتیوب بفرستید:", ct);
            return true;
        }

        if (text == MediaConstants.PinterestSearchButtonText)
        {
            conversationState.SetState(userId, MediaConversationState.AwaitingPinterestQuery);
            await fileSender.SendTextAsync(bot, userId,
                "عبارت جستجو را برای پینترست بفرستید:", ct);
            return true;
        }

        var detected = MediaUrlDetector.TryDetect(text);
        if (detected is not null)
        {
            conversationState.Clear(userId);
            await TryEnqueueDownloadAsync(
                bot, userId, detected.Value.Url, detected.Value.Platform, MediaDownloadSource.Url, ct);
            return true;
        }

        var state = conversationState.GetState(userId);
        if (state == MediaConversationState.Idle || string.IsNullOrWhiteSpace(text))
            return false;

        if (UserAccessService.IsStartCommand(text) || BotConversationHelper.IsMenuOrCommandText(text))
        {
            conversationState.Clear(userId);
            return false;
        }

        if (state == MediaConversationState.AwaitingYouTubeQuery)
        {
            await HandleYouTubeSearchAsync(bot, userId, text!, ct);
            return true;
        }

        if (state == MediaConversationState.AwaitingPinterestQuery)
        {
            await HandlePinterestSearchAsync(bot, userId, text!, ct);
            return true;
        }

        conversationState.Clear(userId);
        await fileSender.SendTextAsync(bot, userId, BotConversationHelper.StateResetMessage, ct);
        return true;
    }

    private async Task HandleYouTubeSearchAsync(
        ITelegramBotClient bot,
        long userId,
        string query,
        CancellationToken ct)
    {
        conversationState.SetState(userId, MediaConversationState.Idle);
        await fileSender.SendTextAsync(bot, userId, "در حال جستجو…", ct);

        var results = await ytDlp.SearchYouTubeAsync(query, ct);
        if (results.Count == 0)
        {
            await fileSender.SendTextAsync(bot, userId,
                "نتیجه‌ای یافت نشد. عبارت دیگری امتحان کنید یا دکمه «جستجوی یوتیوب» را بزنید.", ct);
            return;
        }

        conversationState.SetSearchResults(userId, results);
        await SendSearchResultsAsync(bot, userId, results, MediaConstants.CallbackYouTubePrefix, ct);
    }

    private async Task HandlePinterestSearchAsync(
        ITelegramBotClient bot,
        long userId,
        string query,
        CancellationToken ct)
    {
        conversationState.SetState(userId, MediaConversationState.Idle);
        await fileSender.SendTextAsync(bot, userId, "در حال جستجو…", ct);

        var results = await pinterestSearch.SearchAsync(query, ct);
        if (results.Count == 0)
        {
            await fileSender.SendTextAsync(bot, userId,
                "نتیجه‌ای یافت نشد. عبارت دیگری امتحان کنید یا دکمه «جستجوی پینترست» را بزنید.", ct);
            return;
        }

        conversationState.SetSearchResults(userId, results);
        await SendSearchResultsAsync(bot, userId, results, MediaConstants.CallbackPinterestPrefix, ct);
    }

    private async Task SendSearchResultsAsync(
        ITelegramBotClient bot,
        long userId,
        IReadOnlyList<MediaSearchResultItem> results,
        string callbackPrefix,
        CancellationToken ct)
    {
        await fileSender.SendTextAsync(bot, userId, "یک مورد را انتخاب کنید:", ct);

        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            var caption = TruncateTitle($"{i + 1}. {item.Title}", 1024);
            var markup = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("دانلود", $"{callbackPrefix}{i}"));

            try
            {
                if (!string.IsNullOrWhiteSpace(item.ThumbnailUrl))
                {
                    var sent = await bot.SendPhoto(
                        userId,
                        InputFile.FromUri(item.ThumbnailUrl),
                        caption: caption,
                        replyMarkup: markup,
                        cancellationToken: ct);
                    await chatStorage.SaveOutgoingAsync(userId, caption, sent.MessageId, ct);
                    continue;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send search thumbnail for user {UserId}", userId);
            }

            var fallback = await bot.SendMessage(
                userId,
                caption,
                replyMarkup: markup,
                cancellationToken: ct);
            await chatStorage.SaveOutgoingAsync(userId, caption, fallback.MessageId, ct);
        }
    }

    private static string TruncateTitle(string title, int maxLength) =>
        title.Length <= maxLength ? title : title[..(maxLength - 1)] + "…";

    private async Task TryEnqueueDownloadAsync(
        ITelegramBotClient bot,
        long userId,
        string url,
        DetectedMediaPlatform platform,
        MediaDownloadSource source,
        CancellationToken ct)
    {
        if (!await userAccess.IsPrivilegedUserAsync(userId, ct))
        {
            var kind = MediaPlatformMapper.ToKind(platform);
            var (allowed, message) = await quotaService.CanDownloadAsync(userId, kind, ct);
            if (!allowed)
            {
                await fileSender.SendTextAsync(bot, userId,
                    $"{message}\nاز «{SubscriptionBotHandler.ExtraQuotaButtonText}» یا «{SubscriptionBotHandler.UpgradeButtonText}» استفاده کنید.",
                    ct);
                return;
            }
        }

        await fileSender.SendTextAsync(bot, userId, "در حال دانلود…", ct);
        await downloadQueue.EnqueueAsync(new MediaDownloadJob(userId, url, platform, source), ct);
    }
}
