using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public static class PlanLimitDisplay
{
    public static string PlatformLabel(MediaPlatformKind platform) =>
        MediaPlatformMapper.ToDisplayName(platform);

    public static string MonthlyLimitLabel(QuotaLimitKind kind) => kind switch
    {
        QuotaLimitKind.DownloadCount => "تعداد دانلود (عدد)",
        QuotaLimitKind.DownloadBytes => "حجم کل (مگابایت)",
        QuotaLimitKind.MaxFileBytes => "حداکثر فایل (مگابایت)",
        QuotaLimitKind.SearchCount => "تعداد جستجو (عدد)",
        _ => kind.ToString()
    };

    public static decimal ToDisplayValue(QuotaLimitKind kind, long bytesOrCount) =>
        ByteUnits.IsByteLimitKind(kind) ? ByteUnits.ToMegabytes(bytesOrCount) : bytesOrCount;
}
