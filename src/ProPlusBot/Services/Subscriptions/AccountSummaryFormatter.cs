using ProPlusBot.Models;

namespace ProPlusBot.Services.Subscriptions;

public static class AccountSummaryFormatter
{
    public static IEnumerable<string> FormatPlanHeader(UserAccountSummaryDto summary)
    {
        yield return $"بسته فعال: {MediaPlatformMapper.ToDisplayName(summary.EffectivePlan)}";

        if (summary.StoredPlan != summary.EffectivePlan)
            yield return $"بسته ثبت‌شده: {MediaPlatformMapper.ToDisplayName(summary.StoredPlan)}";

        yield return summary.PlanExpiresAt is null
            ? "انقضا: —"
            : $"انقضا: {PersianDateTimeHelper.ToShamsiDateString(summary.PlanExpiresAt)}";

        if (!summary.HasSubscriptionAccess)
            yield return "وضعیت: دوره آزمایشی پایان یافته";
        else
            yield return summary.IsBanned
                ? "وضعیت: مسدود"
                : summary.IsTrialActive ? "وضعیت: آزمایشی فعال" : "وضعیت: فعال";

        yield return $"شروع دوره سهمیه: {PersianDateTimeHelper.ToShamsiDateString(summary.QuotaPeriodStartAt)}";
    }

    public static IEnumerable<string> FormatMonthlyQuota(SharedQuotaUsageDto q, long maxFileBytes)
    {
        yield return string.Empty;
        yield return "سهمیه ماهانه (یوتیوب + پینترست)";
        yield return $"▫️ تعداد دانلود: {PersianTextHelper.UsedOf(q.MonthlyDownloadCountUsed, q.MonthlyDownloadCountLimit)}";
        yield return $"▫️ حجم دانلود: {ByteUnits.FormatVolumeUsedOf(q.MonthlyBytesUsed, q.MonthlyBytesLimit)}";
        yield return $"▫️ جستجو: {PersianTextHelper.UsedOf(q.MonthlySearchUsed, q.MonthlySearchLimit)}";

        if (q.ExtraDownloadCountBonus > 0)
            yield return $"▫️ سهمیه اضافه (تعداد): +{PersianTextHelper.IsolateLtr(q.ExtraDownloadCountBonus.ToString())}";

        if (q.ExtraDownloadBytesBonus > 0)
        {
            var extra = PersianTextHelper.IsolateLtr(ByteUnits.FormatVolume(q.ExtraDownloadBytesBonus));
            yield return $"▫️ سهمیه اضافه (حجم): +{extra}";
        }

        if (q.ExtraSearchCountBonus > 0)
            yield return $"▫️ سهمیه اضافه (جستجو): +{PersianTextHelper.IsolateLtr(q.ExtraSearchCountBonus.ToString())}";

        if (maxFileBytes > 0)
        {
            var maxFile = PersianTextHelper.IsolateLtr(ByteUnits.FormatVolume(maxFileBytes));
            yield return $"▫️ حداکثر هر فایل: {maxFile}";
        }
    }
}
