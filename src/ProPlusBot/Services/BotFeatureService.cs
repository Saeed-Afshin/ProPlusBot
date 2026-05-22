using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services;

public class BotFeatureService(
    BotSettingsService settingsService,
    UserAccessService userAccess,
    QuotaService quotaService)
{
    public const string HelpButtonText = "❓ راهنما";

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

        var youtube = await CanUseYouTubeAsync(telegramUserId, ct);
        var pinterest = await CanUsePinterestAsync(telegramUserId, ct);

        if (youtube || pinterest)
        {
            rows.Add(
            [
                new KeyboardButton(MediaConstants.YouTubeSearchButtonText),
                new KeyboardButton(MediaConstants.PinterestSearchButtonText)
            ]);
        }

        rows.Add(
        [
            new KeyboardButton(SubscriptionBotHandler.AccountButtonText),
            new KeyboardButton(SubscriptionBotHandler.PlansButtonText)
        ]);
        rows.Add(
        [
            new KeyboardButton(HelpButtonText),
            new KeyboardButton(UserAccessService.RestartButtonText)
        ]);

        return new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true };
    }

    public async Task<string> BuildHelpMessageAsync(long telegramUserId, CancellationToken ct = default)
    {
        var youtube = await CanUseYouTubeAsync(telegramUserId, ct);
        var pinterest = await CanUsePinterestAsync(telegramUserId, ct);

        var sections = new List<string>
        {
            "📖 راهنمای کوتاه ربات",
            HelpParagraph(
                "💳 بسته و دسترسی",
                await BuildSubscriptionHelpBodyAsync(telegramUserId, ct)),
            HelpParagraph(
                SubscriptionBotHandler.AccountButtonText,
                "در این بخش می‌توانید اطلاعات بسته فعال، سهمیه باقی‌مانده و پرداخت‌های اخیرتان را ببینید."),
            HelpParagraph(
                SubscriptionBotHandler.PlansButtonText,
                "از اینجا می‌توانید بسته جدید تهیه کنید، بسته فعلی را ارتقا دهید یا در صورت نیاز سهمیه اضافه بخرید.")
        };

        if (youtube)
        {
            sections.Add(HelpParagraph(
                MediaConstants.YouTubeSearchButtonText,
                "کافی است عنوان یا موضوع موردنظر خود را ارسال کنید؛ سپس از میان نتایج، گزینه دلخواهتان را انتخاب و دانلود را آغاز کنید.\n" +
                "اگر لینک ویدیو یا پست یوتیوب را دارید، آن را مستقیم ارسال کنید تا همان محتوا برای شما آماده شود."));
        }

        if (pinterest)
        {
            sections.Add(HelpParagraph(
                MediaConstants.PinterestSearchButtonText,
                "کافی است عبارت موردنظر خود را ارسال کنید؛ سپس از میان پین‌های نمایش‌داده‌شده، گزینه دلخواهتان را انتخاب کنید.\n" +
                "اگر لینک پینترست را دارید، آن را مستقیم ارسال کنید تا همان پین برای شما آماده شود."));
        }

        sections.Add(HelpParagraph(
            UserAccessService.RestartButtonText,
            "اگر منو نمایش داده نمی‌شود یا تازه در کانال عضو شده‌اید، این دکمه را بزنید تا عضویت و منو دوباره بررسی شود."));
        sections.Add(HelpParagraph(
            HelpButtonText,
            "هر زمان بخواهید، می‌توانید همین راهنما را دوباره مشاهده کنید."));

        return string.Join("\n\n", sections);
    }

    private static string HelpParagraph(string title, string body) => $"{title}\n{body}";

    private async Task<string> BuildSubscriptionHelpBodyAsync(long telegramUserId, CancellationToken ct)
    {
        var intro =
            "برای جستجو و دانلود، باید بسته فعال داشته باشید. ابتدا چند روز می‌توانید رایگان از ربات استفاده کنید؛ " +
            $"بعد از آن از بخش «{SubscriptionBotHandler.PlansButtonText}» یکی از بسته‌های پولی را خریداری کنید.";

        try
        {
            var summary = await quotaService.GetAccountSummaryAsync(telegramUserId, ct);
            var status = FormatUserPlanStatus(summary);
            return string.IsNullOrEmpty(status) ? intro : $"{intro}\n{status}";
        }
        catch (InvalidOperationException)
        {
            return intro;
        }
    }

    private static string FormatUserPlanStatus(UserAccountSummaryDto summary)
    {
        if (!summary.HasSubscriptionAccess)
        {
            return $"در حال حاضر بسته فعالی ندارید. برای ادامه، از بخش «{SubscriptionBotHandler.PlansButtonText}» یک بسته پولی خریداری کنید.";
        }

        var planLabel = summary.IsTrialActive
            ? $"{MediaPlatformMapper.ToDisplayName(SubscriptionPlan.Free)} (رایگان)"
            : MediaPlatformMapper.ToDisplayName(summary.EffectivePlan);

        if (summary.PlanExpiresAt is null)
            return $"بسته فعال شما: {planLabel}";

        var expiryDate = PersianDateTimeHelper.ToShamsiDateString(summary.PlanExpiresAt);
        var expiryTime = PersianDateTimeHelper.ToTimeString(summary.PlanExpiresAt);
        return $"بسته فعال شما: {planLabel}\nاعتبار تا {expiryDate} ساعت {expiryTime}";
    }

    public async Task<string> BuildReadyMessageAsync(long telegramUserId, CancellationToken ct = default)
    {
        var youtube = await CanUseYouTubeAsync(telegramUserId, ct);
        var pinterest = await CanUsePinterestAsync(telegramUserId, ct);

        return (youtube, pinterest) switch
        {
            (true, true) =>
                $"همه شرایط تکمیل است. لینک یوتیوب یا پینترست بفرستید، یا از دکمه‌های «{MediaConstants.YouTubeSearchButtonText}» و «{MediaConstants.PinterestSearchButtonText}» استفاده کنید.",
            (true, false) =>
                $"همه شرایط تکمیل است. لینک یوتیوب بفرستید یا از دکمه «{MediaConstants.YouTubeSearchButtonText}» استفاده کنید.",
            (false, true) =>
                $"همه شرایط تکمیل است. لینک پینترست بفرستید یا از دکمه «{MediaConstants.PinterestSearchButtonText}» استفاده کنید.",
            _ => $"همه شرایط تکمیل است. از «{SubscriptionBotHandler.AccountButtonText}» و «{SubscriptionBotHandler.PlansButtonText}» استفاده کنید."
        };
    }
}
