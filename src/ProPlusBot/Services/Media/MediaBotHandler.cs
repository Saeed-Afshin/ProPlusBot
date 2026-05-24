using System.Globalization;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Entities;
using ProPlusBot.Services;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Media;

public class MediaBotHandler(
    ConversationStateService conversationState,
    MediaDownloadQueue downloadQueue,
    UserInteractionLogService interactionLog,
    YtDlpService ytDlp,
    PinterestSearchService pinterestSearch,
    SearchResultGridComposer gridComposer,
    MediaFileSender fileSender,
    BaleBotClientFactory clientFactory,
    QuotaService quotaService,
    UserAccessService userAccess,
    BotFeatureService botFeatures,
    BotSettingsService botSettingsService,
    ErrorLogService errorLog,
    IOptions<MediaDownloadOptions> mediaOptions,
    ILogger<MediaBotHandler> logger)
{
    public async Task<bool> HandleCallbackQueryAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.From is null || callback.Data is null)
            return false;

        var userId = callback.From.Id;
        var bot = clientFactory.CreateClient();

        if (callback.Data == MediaConstants.CallbackSearchYouTube)
        {
            await StartYouTubeSearchAsync(bot, userId, ct);
            return true;
        }

        if (callback.Data == MediaConstants.CallbackSearchPinterest)
        {
            await StartPinterestSearchAsync(bot, userId, ct);
            return true;
        }

        if (TryParseSearchPageNavCallback(callback.Data, out var navPrefix, out var pageDelta))
        {
            var session = conversationState.GetSearchSession(userId);
            if (session is null || !string.Equals(session.CallbackPrefix, navPrefix, StringComparison.Ordinal))
            {
                conversationState.Clear(userId);
                await fileSender.SendTextAsync(bot, userId,
                    "نتایج جستجو منقضی شده است. دوباره جستجو کنید.", ct);
                return true;
            }

            if (pageDelta > 0 && !session.HasMoreResults)
            {
                await fileSender.SendTextAsync(bot, userId, "نتیجهٔ بیشتری وجود ندارد.", ct);
                return true;
            }

            if (pageDelta < 0 && session.Page <= 0)
            {
                await fileSender.SendTextAsync(bot, userId, "صفحهٔ قبلی وجود ندارد.", ct);
                return true;
            }

            if (!await CanUsePlatformAsync(bot, userId, session.Platform, ct))
                return true;

            if (!await CheckSearchQuotaAsync(bot, userId, session.Platform, ct))
                return true;

            await LoadAndSendSearchPageAsync(bot, userId, session, session.Page + pageDelta, ct);
            return true;
        }

        if (callback.Data is $"{MediaConstants.CallbackYouTubePrefix}x"
            or $"{MediaConstants.CallbackPinterestPrefix}x")
        {
            return true;
        }

        if (TryParseYouTubeFormatPageCallback(callback.Data, out var formatPage))
        {
            var formatSession = conversationState.GetFormatSession(userId);
            if (formatSession is null || formatPage < 0 || formatPage >= formatSession.PageCount)
            {
                await fileSender.SendTextAsync(bot, userId,
                    "لیست کیفیت منقضی شده است. لینک را دوباره بفرستید.", ct);
                return true;
            }

            var updated = formatSession with { Page = formatPage, FormatMenuMessageId = null };
            conversationState.SetFormatSession(userId, updated);
            await SendYouTubeFormatPageAsync(bot, userId, updated, ct);
            return true;
        }

        if (TryParseYouTubeFormatSelectCallback(callback.Data, out var formatIndex))
        {
            await HandleYouTubeFormatSelectedAsync(bot, userId, formatIndex, ct);
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

        await interactionLog.LogAsync(
            userId,
            UserInteractionKind.SearchResultPick,
            UserInteractionStatus.Info,
            searchSession.Results[index].Url,
            MediaJobDisplay.ToPlatformName(searchSession.Platform),
            ct: ct);
        await TryEnqueueDownloadAsync(
            bot, userId, searchSession.Results[index].Url, searchSession.Platform, source, null, ct);
        return true;
    }

    private async Task HandleYouTubeFormatSelectedAsync(
        ITelegramBotClient bot,
        long userId,
        int formatIndex,
        CancellationToken ct)
    {
        var session = conversationState.GetFormatSession(userId);
        if (session is null || formatIndex < 0 || formatIndex >= session.Formats.Count)
        {
            conversationState.ClearFormatSession(userId);
            await fileSender.SendTextAsync(bot, userId,
                "لیست کیفیت منقضی شده است. لینک را دوباره بفرستید.", ct);
            return;
        }

        conversationState.RefreshFormatSession(userId);
        conversationState.RefreshSearchSession(userId);

        if (!await userAccess.IsPrivilegedUserAsync(userId, ct))
        {
            var (canDownload, quotaMessage) = await quotaService.CanDownloadAsync(
                userId,
                MediaPlatformKind.YouTube,
                ct);
            if (!canDownload)
            {
                await interactionLog.LogAsync(
                    userId,
                    UserInteractionKind.QuotaOrAccessBlocked,
                    UserInteractionStatus.Failed,
                    session.Url,
                    quotaMessage,
                    session.IncomingChatMessageId,
                    ct: ct);
                await fileSender.SendTextAsync(bot, userId,
                    $"{quotaMessage}\nاز «{SubscriptionBotHandler.PlansButtonText}» استفاده کنید.",
                    ct);
                return;
            }
        }

        var format = session.Formats[formatIndex];
        if (format.ExceedsLimit(session.MaxFileBytesForPlan))
        {
            await fileSender.SendTextAsync(bot, userId,
                $"حجم این کیفیت ({format.SizeDisplay}) بیشتر از حد مجاز بسته شما " +
                $"({ByteUnits.FormatVolume(session.MaxFileBytesForPlan)}) است.\n" +
                $"کیفیت کوچک‌تر انتخاب کنید یا از «{SubscriptionBotHandler.PlansButtonText}» بسته بالاتر بگیرید.",
                ct);
            return;
        }

        if (session.FormatMenuMessageId is int menuMessageId)
            await fileSender.TryRemoveInlineKeyboardAsync(bot, userId, menuMessageId, ct);

        conversationState.ClearFormatSession(userId);
        await fileSender.SendTextAsync(bot, userId, $"در حال دانلود ({format.Label})…", ct);
        await interactionLog.LogAsync(
            userId,
            UserInteractionKind.FormatSelected,
            UserInteractionStatus.Info,
            session.Url,
            format.Label,
            session.IncomingChatMessageId,
            ct: ct);
        await EnqueueDownloadAsync(
            userId,
            session.Url,
            DetectedMediaPlatform.YouTube,
            session.Source,
            format.FormatId,
            session.IncomingChatMessageId,
            ct);
    }

    private async Task SendYouTubeFormatPageAsync(
        ITelegramBotClient bot,
        long userId,
        YouTubeFormatSession session,
        CancellationToken ct)
    {
        var text = YouTubeFormatPresenter.BuildMessage(session);
        var markup = YouTubeFormatPresenter.BuildKeyboard(session);
        var messageId = await fileSender.SendTextAsync(bot, userId, text, markup, ct);
        if (messageId is not null)
        {
            conversationState.SetFormatSession(
                userId,
                session with { FormatMenuMessageId = messageId });
        }
    }

    private static bool TryParseYouTubeFormatSelectCallback(string data, out int index)
    {
        index = -1;
        if (!data.StartsWith(MediaConstants.CallbackYouTubeFormatPrefix, StringComparison.Ordinal))
            return false;

        var suffix = data[MediaConstants.CallbackYouTubeFormatPrefix.Length..];
        if (suffix.Length == 0 || suffix.StartsWith(MediaConstants.CallbackFormatPage, StringComparison.Ordinal))
            return false;

        return int.TryParse(suffix, out index);
    }

    private static bool TryParseYouTubeFormatPageCallback(string data, out int page)
    {
        page = -1;
        var prefix = MediaConstants.CallbackYouTubeFormatPrefix + MediaConstants.CallbackFormatPage;
        if (!data.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        return int.TryParse(data[prefix.Length..], out page);
    }

    public async Task<bool> TryHandleMessageAsync(
        ITelegramBotClient bot,
        Message message,
        long? incomingChatMessageId,
        CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text?.Trim();

        if (text == MediaConstants.SearchButtonText)
        {
            await HandleSearchMenuRequestAsync(bot, userId, ct);
            return true;
        }

        if (text == MediaConstants.YouTubeSearchButtonText)
        {
            await StartYouTubeSearchAsync(bot, userId, ct);
            return true;
        }

        if (text == MediaConstants.PinterestSearchButtonText)
        {
            await StartPinterestSearchAsync(bot, userId, ct);
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
            await interactionLog.LogAsync(
                userId,
                UserInteractionKind.LinkDetected,
                UserInteractionStatus.Info,
                detected.Value.Url,
                MediaJobDisplay.ToPlatformName(detected.Value.Platform),
                incomingChatMessageId,
                ct: ct);
            await TryEnqueueDownloadAsync(
                bot,
                userId,
                detected.Value.Url,
                detected.Value.Platform,
                MediaDownloadSource.Url,
                incomingChatMessageId,
                ct);
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

            await interactionLog.LogAsync(
                userId,
                UserInteractionKind.SearchQuery,
                UserInteractionStatus.Info,
                text!,
                "یوتیوب",
                incomingChatMessageId,
                ct: ct);
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

            await interactionLog.LogAsync(
                userId,
                UserInteractionKind.SearchQuery,
                UserInteractionStatus.Info,
                text!,
                "پینترست",
                incomingChatMessageId,
                ct: ct);
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

        conversationState.ClearFormatSession(userId);
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
        int targetPage,
        CancellationToken ct)
    {
        var gridMessageId = current.SearchGridMessageId;
        if (gridMessageId is null)
            await fileSender.SendTextAsync(bot, userId, "در حال جستجو…", ct);

        var session = await FetchSearchPageAsync(
            current.Query,
            current.Platform,
            targetPage,
            current.PinterestBookmark,
            current.GridLayout,
            ct);

        if (session is null || session.Results.Count == 0)
        {
            var noMoreText = targetPage < current.Page
                ? "صفحهٔ قبلی در دسترس نیست."
                : "نتیجهٔ بیشتری وجود ندارد.";
            await fileSender.SendTextAsync(bot, userId, noMoreText, ct);
            return;
        }

        var merged = session with { SearchGridMessageId = gridMessageId };
        conversationState.SetSearchSession(userId, merged);
        await SendSearchResultsPageAsync(bot, userId, merged, ct);
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
            string? bookmark = null;
            PinterestSearchPage pinPage = new([], null);
            for (var p = 0; p <= page; p++)
            {
                pinPage = await pinterestSearch.SearchPageAsync(query, bookmark, pageSize, ct);
                if (pinPage.Items.Count == 0)
                    return null;

                bookmark = pinPage.NextBookmark;
            }

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
        if (gridJpeg is not null)
        {
            var messageId = await fileSender.PublishSearchGridAsync(
                bot, userId, session.SearchGridMessageId, gridJpeg, caption, markup, ct);
            if (messageId is not null)
            {
                conversationState.SetSearchSession(
                    userId,
                    session with { SearchGridMessageId = messageId });
                return;
            }

            logger.LogWarning("Search grid publish failed for user {UserId}", userId);
        }
        else
        {
            logger.LogWarning("Search grid image was not created for user {UserId}", userId);
        }

        await fileSender.SendTextAsync(bot, userId, caption, markup, ct);
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
            // RTL clients (Bale/Telegram fa) mirror each keyboard row; iterate columns
            // right-to-left so on-screen order matches the left-to-right grid image.
            for (var col = columns - 1; col >= 0; col--)
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

        var navRow = new List<InlineKeyboardButton>();
        if (session.Page > 0)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData(
                MediaConstants.PrevPageButtonText,
                $"{session.CallbackPrefix}{MediaConstants.CallbackPrevPage}"));
        }

        if (session.HasMoreResults)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData(
                MediaConstants.NextPageButtonText,
                $"{session.CallbackPrefix}{MediaConstants.CallbackNextPage}"));
        }

        if (navRow.Count > 0)
            keyboardRows.Add(navRow.ToArray());

        return new InlineKeyboardMarkup(keyboardRows);
    }

    private static bool TryParseSearchPageNavCallback(string data, out string prefix, out int pageDelta)
    {
        prefix = string.Empty;
        pageDelta = 0;

        if (data == MediaConstants.CallbackYouTubePrefix + MediaConstants.CallbackNextPage
            || data == MediaConstants.CallbackPinterestPrefix + MediaConstants.CallbackNextPage)
        {
            prefix = data.StartsWith(MediaConstants.CallbackYouTubePrefix, StringComparison.Ordinal)
                ? MediaConstants.CallbackYouTubePrefix
                : MediaConstants.CallbackPinterestPrefix;
            pageDelta = 1;
            return true;
        }

        if (data == MediaConstants.CallbackYouTubePrefix + MediaConstants.CallbackPrevPage
            || data == MediaConstants.CallbackPinterestPrefix + MediaConstants.CallbackPrevPage)
        {
            prefix = data.StartsWith(MediaConstants.CallbackYouTubePrefix, StringComparison.Ordinal)
                ? MediaConstants.CallbackYouTubePrefix
                : MediaConstants.CallbackPinterestPrefix;
            pageDelta = -1;
            return true;
        }

        return false;
    }

    private static bool TryParseSearchSelectCallback(string data, out string prefix, out int index)
    {
        prefix = string.Empty;
        index = -1;

        if (data.StartsWith(MediaConstants.CallbackYouTubeFormatPrefix, StringComparison.Ordinal))
            return false;

        if (data.StartsWith(MediaConstants.CallbackYouTubePrefix, StringComparison.Ordinal)
            && !IsSearchNavCallback(data, MediaConstants.CallbackYouTubePrefix))
        {
            prefix = MediaConstants.CallbackYouTubePrefix;
            var suffix = data[prefix.Length..];
            return suffix.Length > 0
                && suffix != "x"
                && int.TryParse(suffix, out index);
        }

        if (data.StartsWith(MediaConstants.CallbackPinterestPrefix, StringComparison.Ordinal)
            && !IsSearchNavCallback(data, MediaConstants.CallbackPinterestPrefix))
        {
            prefix = MediaConstants.CallbackPinterestPrefix;
            var suffix = data[prefix.Length..];
            return suffix.Length > 0
                && suffix != "x"
                && int.TryParse(suffix, out index);
        }

        return false;
    }

    private static bool IsSearchNavCallback(string data, string prefix) =>
        data == prefix + MediaConstants.CallbackNextPage
        || data == prefix + MediaConstants.CallbackPrevPage;

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
        long? incomingChatMessageId,
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
                await interactionLog.LogAsync(
                    userId,
                    UserInteractionKind.QuotaOrAccessBlocked,
                    UserInteractionStatus.Failed,
                    url,
                    message,
                    incomingChatMessageId,
                    ct: ct);
                await fileSender.SendTextAsync(bot, userId,
                    $"{message}\nاز «{SubscriptionBotHandler.PlansButtonText}» استفاده کنید.",
                    ct);
                return;
            }
        }

        if (platform == DetectedMediaPlatform.YouTube)
        {
            await OfferYouTubeFormatSelectionAsync(bot, userId, url, source, incomingChatMessageId, ct);
            return;
        }

        await fileSender.SendTextAsync(bot, userId, "در حال دانلود…", ct);
        await EnqueueDownloadAsync(userId, url, platform, source, null, incomingChatMessageId, ct);
    }

    private async Task OfferYouTubeFormatSelectionAsync(
        ITelegramBotClient bot,
        long userId,
        string url,
        MediaDownloadSource source,
        long? incomingChatMessageId,
        CancellationToken ct)
    {
        await fileSender.SendTextAsync(bot, userId, "در حال دریافت کیفیت‌های موجود…", ct);

        var list = await ytDlp.ListYouTubeFormatsAsync(url, ct);
        if (!list.Success || list.Formats.Count == 0)
        {
            var detail = string.IsNullOrWhiteSpace(list.ErrorDetail)
                ? "No formats returned."
                : list.ErrorDetail;
            await errorLog.LogAsync(
                userId,
                "دریافت کیفیت‌های یوتیوب ناموفق بود",
                $"URL: {url}{Environment.NewLine}{Environment.NewLine}{detail}",
                nameof(MediaBotHandler),
                ErrorLogServices.YouTube,
                ct);
            await interactionLog.LogAsync(
                userId,
                UserInteractionKind.FormatListFailed,
                UserInteractionStatus.Failed,
                url,
                "دریافت کیفیت ناموفق",
                incomingChatMessageId,
                ct: ct);
            await fileSender.SendTextAsync(bot, userId,
                "دریافت کیفیت‌ها ناموفق بود. لطفاً بعداً دوباره تلاش کنید یا لینک دیگری بفرستید.",
                ct);
            return;
        }

        var maxBytes = await GetEffectiveMaxFileBytesAsync(userId, ct);
        var session = new YouTubeFormatSession(
            url, list.Formats, source, maxBytes, IncomingChatMessageId: incomingChatMessageId);
        conversationState.SetFormatSession(userId, session);
        conversationState.RefreshSearchSession(userId);
        await interactionLog.LogAsync(
            userId,
            UserInteractionKind.FormatListShown,
            UserInteractionStatus.Success,
            url,
            $"{list.Formats.Count} کیفیت",
            incomingChatMessageId,
            ct: ct);
        await SendYouTubeFormatPageAsync(bot, userId, session, ct);
    }

    private async Task EnqueueDownloadAsync(
        long userId,
        string url,
        DetectedMediaPlatform platform,
        MediaDownloadSource source,
        string? youtubeFormatId,
        long? incomingChatMessageId,
        CancellationToken ct)
    {
        var jobId = await downloadQueue.EnqueueAsync(
            new MediaDownloadJob(userId, url, platform, source, youtubeFormatId),
            incomingChatMessageId,
            ct);
        await interactionLog.LogAsync(
            userId,
            UserInteractionKind.DownloadQueued,
            UserInteractionStatus.Pending,
            url,
            "در صف",
            incomingChatMessageId,
            jobId,
            ct);
    }

    private async Task<long> GetEffectiveMaxFileBytesAsync(long userId, CancellationToken ct)
    {
        if (await userAccess.IsPrivilegedUserAsync(userId, ct))
            return long.MaxValue;

        var planMax = await quotaService.GetMaxFileBytesAsync(
            userId,
            MediaPlatformKind.YouTube,
            ct);

        return Math.Min(planMax, mediaOptions.Value.MaxUploadBytes);
    }

    private async Task HandleSearchMenuRequestAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        var youtube = await botFeatures.CanUseYouTubeAsync(userId, ct);
        var pinterest = await botFeatures.CanUsePinterestAsync(userId, ct);

        if (!youtube && !pinterest)
        {
            await fileSender.SendTextAsync(bot, userId,
                "جستجو در حال حاضر غیرفعال است.", ct);
            return;
        }

        if (youtube && !pinterest)
        {
            await StartYouTubeSearchAsync(bot, userId, ct);
            return;
        }

        if (pinterest && !youtube)
        {
            await StartPinterestSearchAsync(bot, userId, ct);
            return;
        }

        var rows = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    MediaConstants.YouTubeSearchButtonText,
                    MediaConstants.CallbackSearchYouTube)
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    MediaConstants.PinterestSearchButtonText,
                    MediaConstants.CallbackSearchPinterest)
            }
        };

        await bot.SendMessage(
            userId,
            "پلتفرم جستجو را انتخاب کنید:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    private async Task StartYouTubeSearchAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        if (!await botFeatures.CanUseYouTubeAsync(userId, ct))
        {
            await fileSender.SendTextAsync(bot, userId, BotFeatureService.YouTubeDisabledMessage, ct);
            return;
        }

        conversationState.ClearFormatSession(userId);
        conversationState.SetState(userId, MediaConversationState.AwaitingYouTubeQuery);
        await fileSender.SendTextAsync(bot, userId, "عبارت جستجو را برای یوتیوب بفرستید:", ct);
    }

    private async Task StartPinterestSearchAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        if (!await botFeatures.CanUsePinterestAsync(userId, ct))
        {
            await fileSender.SendTextAsync(bot, userId, BotFeatureService.PinterestDisabledMessage, ct);
            return;
        }

        conversationState.ClearFormatSession(userId);
        conversationState.SetState(userId, MediaConversationState.AwaitingPinterestQuery);
        await fileSender.SendTextAsync(bot, userId, "عبارت جستجو را برای پینترست بفرستید:", ct);
    }
}
