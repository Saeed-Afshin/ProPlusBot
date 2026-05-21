using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services;

public class BotFeatureService(BotSettingsService settingsService, UserAccessService userAccess)
{
    public const string YouTubeDisabledMessage = "قابلیت یوتیوب موقتاً غیرفعال است.";
    public const string PinterestDisabledMessage = "قابلیت پینترست موقتاً غیرفعال است.";

    public async Task<bool> CanUseYouTubeAsync(long telegramUserId, CancellationToken ct = default)
    {
        var settings = await settingsService.GetAsync(ct);
        return settings.YouTubeEnabled
            || await userAccess.IsPrivilegedUserAsync(telegramUserId, ct);
    }

    public async Task<bool> CanUsePinterestAsync(long telegramUserId, CancellationToken ct = default)
    {
        var settings = await settingsService.GetAsync(ct);
        return settings.PinterestEnabled
            || await userAccess.IsPrivilegedUserAsync(telegramUserId, ct);
    }

    public async Task<bool> CanUsePlatformAsync(
        long telegramUserId,
        DetectedMediaPlatform platform,
        CancellationToken ct = default) =>
        platform switch
        {
            DetectedMediaPlatform.YouTube => await CanUseYouTubeAsync(telegramUserId, ct),
            DetectedMediaPlatform.Pinterest => await CanUsePinterestAsync(telegramUserId, ct),
            _ => false
        };

    public static string DisabledMessage(DetectedMediaPlatform platform) =>
        platform == DetectedMediaPlatform.YouTube
            ? YouTubeDisabledMessage
            : PinterestDisabledMessage;

    public async Task<ReplyKeyboardMarkup> BuildMainMenuKeyboardAsync(
        long telegramUserId,
        CancellationToken ct = default)
    {
        var rows = new List<KeyboardButton[]>();

        var mediaButtons = new List<KeyboardButton>();
        if (await CanUseYouTubeAsync(telegramUserId, ct))
            mediaButtons.Add(new KeyboardButton(MediaConstants.YouTubeSearchButtonText));
        if (await CanUsePinterestAsync(telegramUserId, ct))
            mediaButtons.Add(new KeyboardButton(MediaConstants.PinterestSearchButtonText));

        if (mediaButtons.Count > 0)
            rows.Add(mediaButtons.ToArray());

        rows.Add(
        [
            new KeyboardButton(SubscriptionBotHandler.AccountButtonText),
            new KeyboardButton(SubscriptionBotHandler.UpgradeButtonText)
        ]);
        rows.Add(
        [
            new KeyboardButton(SubscriptionBotHandler.BuyPlanButtonText),
            new KeyboardButton(SubscriptionBotHandler.ExtraQuotaButtonText)
        ]);
        rows.Add([new KeyboardButton(UserAccessService.RestartButtonText)]);

        return new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true };
    }

    public async Task<string> BuildReadyMessageAsync(long telegramUserId, CancellationToken ct = default)
    {
        var youtube = await CanUseYouTubeAsync(telegramUserId, ct);
        var pinterest = await CanUsePinterestAsync(telegramUserId, ct);

        return (youtube, pinterest) switch
        {
            (true, true) =>
                "همه شرایط تکمیل است. لینک یوتیوب یا پینترست بفرستید، یا از دکمه‌های جستجو استفاده کنید.",
            (true, false) =>
                "همه شرایط تکمیل است. لینک یوتیوب بفرستید یا از دکمه «جستجوی یوتیوب» استفاده کنید.",
            (false, true) =>
                "همه شرایط تکمیل است. لینک پینترست بفرستید یا از دکمه «جستجوی پینترست» استفاده کنید.",
            _ => "همه شرایط تکمیل است. از منوی حساب و پلن‌ها استفاده کنید."
        };
    }
}
