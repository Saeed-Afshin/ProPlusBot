using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services.Media;

public static class BotConversationHelper
{
    public const string StateResetMessage =
        "متوجه وضعیت گفتگوی شما نشدم و آن را بازنشانی کردم.\n" +
        "لینک یوتیوب یا پینترست بفرستید، یا از دکمه‌های «جستجوی یوتیوب» و «جستجوی پینترست» استفاده کنید.";

    public static bool IsMenuOrCommandText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        return UserAccessService.IsStartCommand(text)
               || text is MediaConstants.SearchButtonText
                   or MediaConstants.YouTubeSearchButtonText
                   or MediaConstants.PinterestSearchButtonText
                   or SubscriptionBotHandler.AccountButtonText
                   or SubscriptionBotHandler.PlansButtonText
                   or SubscriptionBotHandler.UpgradeButtonText
                   or SubscriptionBotHandler.BuyPlanButtonText
                   or SubscriptionBotHandler.ExtraQuotaButtonText
                   or UserAccessService.RestartButtonText;
    }

    public static bool IsActiveSearchState(MediaConversationState state) =>
        state is MediaConversationState.AwaitingYouTubeQuery
            or MediaConversationState.AwaitingPinterestQuery;
}
