using ProPlusBot.Models;

namespace ProPlusBot.Pages.Users;

public static class BulkQuotaUi
{
    public static bool RequiresValue(AdminBulkQuotaAction action) =>
        action is not AdminBulkQuotaAction.ResetPlanExpiry
            and not AdminBulkQuotaAction.ResetDownloadCount
            and not AdminBulkQuotaAction.ResetDownloadVolume
            and not AdminBulkQuotaAction.ResetSearchCount;

    public static string ActionLabel(AdminBulkQuotaAction action) => action switch
    {
        AdminBulkQuotaAction.ExtendPlanDays => "تمدید تاریخ بسته (افزودن روز)",
        AdminBulkQuotaAction.ResetPlanExpiry => "بازنشانی تاریخ بسته (اعتبار کامل دوره)",
        AdminBulkQuotaAction.ExtendDownloadCount => "افزودن به سهمیه تعداد دانلود",
        AdminBulkQuotaAction.ResetDownloadCount => "بازنشانی سهمیه تعداد دانلود (باقی‌مانده = سقف بسته)",
        AdminBulkQuotaAction.ExtendDownloadMegabytes => "افزودن به سهمیه حجم دانلود (مگابایت)",
        AdminBulkQuotaAction.ResetDownloadVolume => "بازنشانی سهمیه حجم دانلود (باقی‌مانده = سقف بسته)",
        AdminBulkQuotaAction.ExtendSearchCount => "افزودن به سهمیه جستجو",
        AdminBulkQuotaAction.ResetSearchCount => "بازنشانی سهمیه جستجو (باقی‌مانده = سقف بسته)",
        _ => action.ToString()
    };

    public static string ValueHint(AdminBulkQuotaAction action) => action switch
    {
        AdminBulkQuotaAction.ExtendPlanDays => "تعداد روز اضافه‌شده به تاریخ انقضا",
        AdminBulkQuotaAction.ResetPlanExpiry => "نیازی به مقدار نیست — اعتبار بسته به اندازه یک دوره کامل از همین لحظه تنظیم می‌شود.",
        AdminBulkQuotaAction.ExtendDownloadCount => "تعداد دانلود اضافه می‌شود (مثلاً ۵ → سقف مؤثر ۵ واحد بیشتر).",
        AdminBulkQuotaAction.ResetDownloadCount =>
            "مصرف فعلی حفظ می‌شود و سقف مؤثر طوری تنظیم می‌شود که باقی‌مانده برابر سقف بسته شود (مثلاً ۱۴ از ۲۰ → امکان ۲۰ دانلود دیگر).",
        AdminBulkQuotaAction.ExtendDownloadMegabytes => "حجم به مگابایت به سقف اضافه می‌شود.",
        AdminBulkQuotaAction.ResetDownloadVolume =>
            "مانند بازنشانی تعداد: باقی‌مانده حجم برابر سقف بسته می‌شود، بدون صفر کردن مصرف.",
        AdminBulkQuotaAction.ExtendSearchCount => "تعداد جستجوی اضافه به سقف ماهانه.",
        AdminBulkQuotaAction.ResetSearchCount =>
            "باقی‌مانده جستجو برابر سقف بسته می‌شود؛ مصرف قبلی در شمارش باقی می‌ماند.",
        _ => ""
    };
}
