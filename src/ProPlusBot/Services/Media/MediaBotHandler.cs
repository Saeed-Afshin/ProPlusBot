using System.Globalization;
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
    SearchResultGridComposer gridComposer,
    MediaFileSender fileSender,
    BaleBotClientFactory clientFactory,
    ChatStorageService chatStorage,
    QuotaService quotaService,
    UserAccessService userAccess,
    BotFeatureService botFeatures,
    BotSettingsService botSettingsService,
    ILogger<MediaBotHandler> logger)
{
    public async Task<bool> HandleCallbackQueryAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.From is null || callback.Data is null)
            return false;

        var userId = callback.From.Id;
        var bot = clientFactory.CreateClient();

        if (TryParseSearchNextCallback(callback.Data, out var nextPrefix))
        {
            var session = conversationState.GetSearchSession(userId);
            if (session is null || !string.Equals(session.CallbackPrefix, nextPrefix, StringComparison.Ordinal))
            {
                conversationState.Clear(userId);
                await fileSender.SendTextAsync(bot, userId,
                    "نتایج جستجو منقضی شده است. دوباره جستجو کنید.", ct);
                return true;
            }

            if (!session.HasMoreResults)
            {
                await fileSender.SendTextAsync(bot, userId, "نتیجهٔ بیشتری وجود ندارد.", ct);
                return true;
            }

            if (!await CanUsePlatformAsync(bot, userId, session.Platform, ct))
                return true;

            if (!await CheckSearchQuotaAsync(bot, userId, session.Platform, ct))
                return true;

            await LoadAndSendSearchPageAsync(bot, userId, session, session.Page + 1, ct);
            return true;
        }

        if (callback.Data is $"{MediaConstants.CallbackYouTubePrefix}x"
            or $"{MediaConstants.CallbackPinterestPrefix}x")
        {
            return true;
        }

        if (!TryParseSearchSelectCallback(callback.Data, out var selectPrefix, out var index))
            return false;

        var searchSession = conversationState.GetSearchSession(userId);
        if (searchSession is null
            || !string.Equals(searchSession.CallbackPrefix, selectPrefix, StringComparison.Ordinal)
            || index < 0
            || index >= searchSession.Results.Count)
        {
            conversationState.Clear(userId);
            await fileSender.SendTextAsync(bot, userId,
                "نتایج جستجو منقضی شده است. دوباره جستجو کنید.", ct);
            return true;
        }

        if (!await CanUsePlatformAsync(bot, userId, searchSession.Platform, ct))
            return true;

        conversationState.RefreshSearchSession(userId);

        var source = searchSession.Platform == DetectedMediaPlatform.YouTube
            ? MediaDownloadSource.YouTubeSearch
            : MediaDownloadSource.PinterestSearch;

        await TryEnqueueDownloadAsync(bot, userId, searchSession.Results[index].Url, searchSession.Platform, source, ct);
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
            if (!await botFeatures.CanUseYouTubeAsync(userId, ct))
            {
                await fileSender.SendTextAsync(bot, userId, BotFeatureService.YouTubeDisabledMessage, ct);
                return true;
            }

            conversationState.SetState(userId, MediaConversationState.AwaitingYouTubeQuery);
            await fileSender.SendTextAsync(bot, userId,
                "عبارت جستجو را برای یوتیوب بفرستید:", ct);
            return true;
        }

        if (text == MediaConstants.PinterestSearchButtonText)
        {
            if (!await botFeatures.CanUsePinterestAsync(userId, ct))
            {
                await fileSender.SendTextAsync(bot, userId, BotFeatureService.PinterestDisabledMessage, ct);
                return true;
            }

            conversationState.SetState(userId, MediaConversationState.AwaitingPinterestQuery);
            await fileSender.SendTextAsync(bot, userId,
                "عبارت جستجو را برای پینترست بفرستید:", ct);
            return true;
        }

        var detected = MediaUrlDetector.TryDetect(text);
        if (detected is not null)
        {
            if (!await botFeatures.CanUsePlatformAsync(userId, detected.Value.Platform, ct))
            {
                await fileSender.SendTextAsync(
                    bot, userId, BotFeatureService.DisabledMessage(detected.Value.Platform), ct);
                return true;
            }

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
            if (!await botFeatures.CanUseYouTubeAsync(userId, ct))
            {
                conversationState.Clear(userId);
                await fileSender.SendTextAsync(bot, userId, BotFeatureService.YouTubeDisabledMessage, ct);
                return true;
            }

            await StartSearchAsync(bot, userId, text!, DetectedMediaPlatform.YouTube, ct);
            return true;
        }

        if (state == MediaConversationState.AwaitingPinterestQuery)
        {
            if (!await botFeatures.CanUsePinterestAsync(userId, ct))
            {
                conversationState.Clear(userId);
                await fileSender.SendTextAsync(bot, userId, BotFeatureService.PinterestDisabledMessage, ct);
                return true;
            }

            await StartSearchAsync(bot, userId, text!, DetectedMediaPlatform.Pinterest, ct);
            return true;
        }

        conversationState.Clear(userId);
        await fileSender.SendTextAsync(bot, userId, BotConversationHelper.StateResetMessage, ct);
        return true;
    }

    private async Task StartSearchAsync(
        ITelegramBotClient bot,
        long userId,
        string query,
        DetectedMediaPlatform platform,
        CancellationToken ct)
    {
        if (!await CheckSearchQuotaAsync(bot, userId, platform, ct))
            return;

        conversationState.SetState(userId, MediaConversationState.Idle);
        await fileSender.SendTextAsync(bot, userId, "در حال جستجو…", ct);

        var gridLayout = await GetSearchGridLayoutAsync(ct);
        var session = await FetchSearchPageAsync(query, platform, page: 0, pinterestBookmark: null, gridLayout, ct);
        if (session is null || session.Results.Count == 0)
        {
            var platformName = platform == DetectedMediaPlatform.YouTube ? "یوتیوب" : "پینترست";
            await fileSender.SendTextAsync(bot, userId,
                $"نتیجه‌ای یافت نشد. عبارت دیگری امتحان کنید یا دکمه «جستجوی {platformName}» را بزنید.", ct);
            return;
        }

        conversationState.SetSearchSession(userId, session);
        await SendSearchResultsPageAsync(bot, userId, session, ct);
    }

    private async Task LoadAndSendSearchPageAsync(
        ITelegramBotClient bot,
        long userId,
        MediaSearchSession current,
        int nextPage,
        CancellationToken ct)
    {
        await fileSender.SendTextAsync(bot, userId, "در حال جستجو…", ct);

        var session = await FetchSearchPageAsync(
            current.Query,
            current.Platform,
            nextPage,
            current.PinterestBookmark,
            current.GridLayout,
            ct);

        if (session is null || session.Results.Count == 0)
        {
            await fileSender.SendTextAsync(bot, userId, "نتیجهٔ بیشتری وجود ندارد.", ct);
            return;
        }

        conversationState.SetSearchSession(userId, session);
        await SendSearchResultsPageAsync(bot, userId, session, ct);
    }

    private async Task<MediaSearchSession?> FetchSearchPageAsync(
        string query,
        DetectedMediaPlatform platform,
        int page,
        string? pinterestBookmark,
        SearchGridLayout gridLayout,
        CancellationToken ct)
    {
        var pageSize = gridLayout.PageSize;

        if (platform == DetectedMediaPlatform.YouTube)
        {
            var results = await ytDlp.SearchYouTubeAsync(query, page, pageSize, ct);
            return new MediaSearchSession(
                query,
                page,
                results,
                HasMoreResults: results.Count == pageSize,
                MediaConstants.CallbackYouTubePrefix,
                platform,
                PinterestBookmark: null,
                gridLayout);
        }

        if (platform == DetectedMediaPlatform.Pinterest)
        {
            var pinPage = await pinterestSearch.SearchPageAsync(query, pinterestBookmark, pageSize, ct);
            var hasMore = pinPage.Items.Count == pageSize && !string.IsNullOrWhiteSpace(pinPage.NextBookmark);
            return new MediaSearchSession(
                query,
                page,
                pinPage.Items,
                hasMore,
                MediaConstants.CallbackPinterestPrefix,
                platform,
                pinPage.NextBookmark,
                gridLayout);
        }

        return null;
    }

    private async Task<SearchGridLayout> GetSearchGridLayoutAsync(CancellationToken ct)
    {
        var settings = await botSettingsService.GetAsync(ct);
        var (cols, rows) = SearchGridPresets.Normalize(settings.SearchGridColumns, settings.SearchGridRows);
        return new SearchGridLayout(cols, rows, settings.SearchGridJpegQuality);
    }

    private async Task<bool> CheckSearchQuotaAsync(
        ITelegramBotClient bot,
        long userId,
        DetectedMediaPlatform platform,
        CancellationToken ct)
    {
        if (!await userAccess.HasSubscriptionAccessAsync(userId, ct))
        {
            await fileSender.SendTextAsync(bot, userId, SubscriptionMessages.TrialExpired, ct);
            return false;
        }

        if (await userAccess.IsPrivilegedUserAsync(userId, ct))
            return true;

        var kind = MediaPlatformMapper.ToKind(platform);
        var (allowed, message) = await quotaService.CanSearchAsync(userId, kind, ct);
        if (allowed)
        {
            await quotaService.RecordSearchAsync(userId, kind, ct);
            return true;
        }

        await fileSender.SendTextAsync(bot, userId, message ?? "امکان جستجو وجود ندارد.", ct);
        return false;
    }

    private async Task SendSearchResultsPageAsync(
        ITelegramBotClient bot,
        long userId,
        MediaSearchSession session,
        CancellationToken ct)
    {
        var caption = session.Page == 0
            ? "نتایج جستجو — یک شماره را انتخاب کنید:"
            : $"نتایج جستجو — صفحه {session.Page + 1}\nیک شماره را انتخاب کنید:";

        var markup = BuildSearchKeyboard(session);

        var gridJpeg = await gridComposer.CreateGridJpegAsync(session.Results, session.GridLayout, ct);
        if (gridJpeg is not null
            && await fileSender.SendSearchGridPhotoAsync(bot, userId, gridJpeg, caption, markup, ct))
        {
            return;
        }

        if (gridJpeg is null)
            logger.LogWarning("Search grid image was not created for user {UserId}", userId);

        var fallback = await bot.SendMessage(userId, caption, replyMarkup: markup, cancellationToken: ct);
        await chatStorage.SaveOutgoingAsync(userId, caption, fallback.MessageId, ct);
    }

    private static InlineKeyboardMarkup BuildSearchKeyboard(MediaSearchSession session)
    {
        var keyboardRows = new List<InlineKeyboardButton[]>();
        var itemCount = session.Results.Count;
        if (itemCount == 0)
            return new InlineKeyboardMarkup(keyboardRows);

        var columns = session.GridLayout.Columns;
        var usedRows = (itemCount + columns - 1) / columns;

        for (var row = 0; row < usedRows; row++)
        {
            var buttonRow = new List<InlineKeyboardButton>();
            for (var col = 0; col < columns; col++)
            {
                var slot = row * columns + col;
                if (slot >= itemCount)
                    continue;

                buttonRow.Add(InlineKeyboardButton.WithCallbackData(
                    (slot + 1).ToString(CultureInfo.InvariantCulture),
                    $"{session.CallbackPrefix}{slot}"));
            }

            if (buttonRow.Count > 0)
                keyboardRows.Add(buttonRow.ToArray());
        }

        if (session.HasMoreResults)
        {
            keyboardRows.Add(
            [
                InlineKeyboardButton.WithCallbackData(
                    MediaConstants.NextPageButtonText,
                    $"{session.CallbackPrefix}{MediaConstants.CallbackNextPage}")
            ]);
        }

        return new InlineKeyboardMarkup(keyboardRows);
    }

    private static bool TryParseSearchNextCallback(string data, out string prefix)
    {
        if (data == MediaConstants.CallbackYouTubePrefix + MediaConstants.CallbackNextPage)
        {
            prefix = MediaConstants.CallbackYouTubePrefix;
            return true;
        }

        if (data == MediaConstants.CallbackPinterestPrefix + MediaConstants.CallbackNextPage)
        {
            prefix = MediaConstants.CallbackPinterestPrefix;
            return true;
        }

        prefix = string.Empty;
        return false;
    }

    private static bool TryParseSearchSelectCallback(string data, out string prefix, out int index)
    {
        prefix = string.Empty;
        index = -1;

        if (data.StartsWith(MediaConstants.CallbackYouTubePrefix, StringComparison.Ordinal)
            && data != MediaConstants.CallbackYouTubePrefix + MediaConstants.CallbackNextPage)
        {
            prefix = MediaConstants.CallbackYouTubePrefix;
            var suffix = data[prefix.Length..];
            return suffix.Length > 0
                && suffix != "x"
                && int.TryParse(suffix, out index);
        }

        if (data.StartsWith(MediaConstants.CallbackPinterestPrefix, StringComparison.Ordinal)
            && data != MediaConstants.CallbackPinterestPrefix + MediaConstants.CallbackNextPage)
        {
            prefix = MediaConstants.CallbackPinterestPrefix;
            var suffix = data[prefix.Length..];
            return suffix.Length > 0
                && suffix != "x"
                && int.TryParse(suffix, out index);
        }

        return false;
    }

    private async Task<bool> CanUsePlatformAsync(
        ITelegramBotClient bot,
        long userId,
        DetectedMediaPlatform platform,
        CancellationToken ct)
    {
        var allowed = platform switch
        {
            DetectedMediaPlatform.YouTube => await botFeatures.CanUseYouTubeAsync(userId, ct),
            DetectedMediaPlatform.Pinterest => await botFeatures.CanUsePinterestAsync(userId, ct),
            _ => false
        };

        if (allowed)
            return true;

        conversationState.Clear(userId);
        await fileSender.SendTextAsync(bot, userId, BotFeatureService.DisabledMessage(platform), ct);
        return false;
    }

    private async Task TryEnqueueDownloadAsync(
        ITelegramBotClient bot,
        long userId,
        string url,
        DetectedMediaPlatform platform,
        MediaDownloadSource source,
        CancellationToken ct)
    {
        if (!await botFeatures.CanUsePlatformAsync(userId, platform, ct))
        {
            await fileSender.SendTextAsync(bot, userId, BotFeatureService.DisabledMessage(platform), ct);
            return;
        }

        if (!await userAccess.HasSubscriptionAccessAsync(userId, ct))
        {
            await fileSender.SendTextAsync(bot, userId, SubscriptionMessages.TrialExpired, ct);
            return;
        }

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
