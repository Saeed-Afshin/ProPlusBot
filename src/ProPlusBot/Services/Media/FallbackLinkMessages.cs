using System.Globalization;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services.Media;

internal static class FallbackLinkMessages
{
    public static string Build(long fileSizeBytes, string publicUrl, DateTime expiresAtUtc)
    {
        var date = PersianDateTimeHelper.ToShamsiDateString(expiresAtUtc);
        var time = PersianDateTimeHelper.ToTehranLocalTime(expiresAtUtc, includeSeconds: false);
        var approxHours = ApproximateHoursRemaining(expiresAtUtc);

        return
            $"فایل ({ByteUnits.FormatMegabytes(fileSizeBytes)}) از طریق لینک دانلود آماده است:\n{publicUrl}\n\n" +
            $"این لینک تا {date} ساعت {time} اعتبار دارد (حدود {approxHours} ساعت).\n" +
            "لطفاً قبل از پایان اعتبار، فایل را دانلود کنید.";
    }

    public static DateTime ComputeExpiresAtUtc(int expiryHours) =>
        DateTime.UtcNow.AddHours(UploadFallbackPresets.NormalizeExpiryHours(expiryHours));

    private static int ApproximateHoursRemaining(DateTime expiresAtUtc)
    {
        var remaining = expiresAtUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return 0;

        return Math.Max(1, (int)Math.Ceiling(remaining.TotalHours));
    }
}
