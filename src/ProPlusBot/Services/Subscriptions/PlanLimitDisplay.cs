using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public static class PlanLimitDisplay
{
    public static string PlatformLabel(MediaPlatformKind platform) =>
        MediaPlatformMapper.ToDisplayName(platform);

    public static string PeriodLabel(UsagePeriod period, QuotaLimitKind limitKind) =>
        limitKind == QuotaLimitKind.MaxFileBytes
            ? "—"
            : period == UsagePeriod.Daily ? "روزانه" : "ماهانه";

    public static string TypeLabel(QuotaLimitKind kind) => kind switch
    {
        QuotaLimitKind.DownloadCount => "تعداد دانلود",
        QuotaLimitKind.DownloadBytes => "حجم کل",
        QuotaLimitKind.MaxFileBytes => "حداکثر فایل",
        _ => kind.ToString()
    };

    public static string UnitLabel(QuotaLimitKind kind) =>
        ByteUnits.IsByteLimitKind(kind) ? "MB" : "عدد";

    public static decimal ToDisplayValue(QuotaLimitKind kind, long bytesOrCount) =>
        ByteUnits.IsByteLimitKind(kind) ? ByteUnits.ToMegabytes(bytesOrCount) : bytesOrCount;
}
