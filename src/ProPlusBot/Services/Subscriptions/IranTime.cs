namespace ProPlusBot.Services.Subscriptions;

/// <summary>Tehran (Asia/Tehran) calendar boundaries for usage quotas.</summary>
public static class IranTime
{
    public const string WindowsTimeZoneId = "Iran Standard Time";
    public const string IanaTimeZoneId = "Asia/Tehran";

    private static readonly TimeZoneInfo Tehran = ResolveTehranTimeZone();

    public static TimeZoneInfo TimeZone => Tehran;

    public static DateTime NowLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tehran);

    /// <summary>UTC instant of today's 00:00 in Tehran.</summary>
    public static DateTime DailyPeriodStartUtc => LocalDateStartToUtc(NowLocal.Date);

    private static DateTime LocalDateStartToUtc(DateTime localDate)
    {
        var unspecified = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Tehran);
    }

    private static TimeZoneInfo ResolveTehranTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? WindowsTimeZoneId : IanaTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? IanaTimeZoneId : WindowsTimeZoneId);
        }
    }
}
