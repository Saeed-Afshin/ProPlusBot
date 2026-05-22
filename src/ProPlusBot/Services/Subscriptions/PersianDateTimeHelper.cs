using System.Globalization;
using System.Text;

namespace ProPlusBot.Services.Subscriptions;

public static class PersianDateTimeHelper
{
    private static readonly PersianCalendar Persian = new();
    private static readonly TimeZoneInfo Tehran = IranTime.TimeZone;

    public static string? ToShamsiDateString(DateTime? utc)
    {
        if (utc is null)
            return null;

        var local = ToTehranLocal(utc.Value);
        return FormatShamsiDate(local);
    }

    public static string ToShamsiMonthYearString(DateTime utc) =>
        ToShamsiMonthYearFromTehranLocal(ToTehranLocal(utc));

    public static string ToShamsiDateFromTehranLocal(DateTime tehranLocal) =>
        FormatShamsiDate(tehranLocal);

    public static string ToShamsiMonthYearFromTehranLocal(DateTime tehranLocal) =>
        $"{Persian.GetYear(tehranLocal):0000}/{Persian.GetMonth(tehranLocal):00}";

    public static string ToTimeString(DateTime? utc)
    {
        if (utc is null)
            return "00:00:00";

        var local = ToTehranLocal(utc.Value);
        return local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string FormatExpiryDisplay(DateTime? utc)
    {
        if (utc is null)
            return "—";

        return $"{ToShamsiDateString(utc)} {ToTimeString(utc)}";
    }

    /// <summary>Shamsi date and 24h time in the app time zone.</summary>
    public static string FormatTehranDisplay(DateTime utc) =>
        $"{ToShamsiDateString(utc)} {ToTimeString(utc)}";

    public static void SetShamsiDateAndTimeFromUtc(DateTime utc, out string date, out string time)
    {
        date = ToShamsiDateString(utc)!;
        time = ToTimeString(utc);
    }

    public static void SetShamsiDateAndTimeNow(out string date, out string time)
    {
        var now = IranTime.NowLocal;
        date = FormatShamsiDate(now);
        time = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static DateTime? CombineShamsiDateAndTimeToUtc(string? shamsiDate, string? time)
    {
        if (string.IsNullOrWhiteSpace(shamsiDate))
            return null;

        shamsiDate = NormalizeDigits(shamsiDate.Trim());
        var parts = shamsiDate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            throw new FormatException("تاریخ شمسی نامعتبر است. فرمت: 1404/01/15");
        }

        var timeOfDay = ParseTimeOfDay(time);
        var local = Persian.ToDateTime(year, month, day, 0, 0, 0, 0).Add(timeOfDay);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Tehran);
    }

    private static string FormatShamsiDate(DateTime tehranLocal) =>
        $"{Persian.GetYear(tehranLocal):0000}/{Persian.GetMonth(tehranLocal):00}/{Persian.GetDayOfMonth(tehranLocal):00}";

    private static DateTime ToTehranLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tehran);

    private static TimeSpan ParseTimeOfDay(string? time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return TimeSpan.Zero;

        time = NormalizeDigits(time.Trim());

        if (TimeSpan.TryParseExact(time, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, TimeSpanStyles.None, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        throw new FormatException("زمان نامعتبر است. فرمت ۲۴ ساعته: 00:00:00 تا 23:59:59");
    }

    private static string NormalizeDigits(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            sb.Append(ch switch
            {
                '۰' => '0',
                '۱' => '1',
                '۲' => '2',
                '۳' => '3',
                '۴' => '4',
                '۵' => '5',
                '۶' => '6',
                '۷' => '7',
                '۸' => '8',
                '۹' => '9',
                '٠' => '0',
                '١' => '1',
                '٢' => '2',
                '٣' => '3',
                '٤' => '4',
                '٥' => '5',
                '٦' => '6',
                '٧' => '7',
                '٨' => '8',
                '٩' => '9',
                _ => ch
            });
        }

        return sb.ToString();
    }
}
