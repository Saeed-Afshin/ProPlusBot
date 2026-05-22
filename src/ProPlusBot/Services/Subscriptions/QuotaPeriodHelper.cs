using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public static class QuotaPeriodHelper
{
    public static DateTime GetPeriodStartUtc(BotUser user) =>
        user.QuotaPeriodStartAt ?? user.CreatedAt;
}
