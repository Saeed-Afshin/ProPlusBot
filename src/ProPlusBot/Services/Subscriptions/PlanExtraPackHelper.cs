using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public static class PlanExtraPackHelper
{
    public static long GetPackPriceToman(PlanPricing plan) =>
        plan.ExtraDownloadCountPriceToman > 0
            ? plan.ExtraDownloadCountPriceToman
            : plan.ExtraDownloadBytesPriceToman;

    public static bool IsPackAvailable(PlanPricing plan) =>
        plan.ExtraDownloadCountPack > 0
        && plan.ExtraDownloadBytesPack > 0
        && GetPackPriceToman(plan) > 0;

    public static string BuildOfferText(PlanPricing plan) =>
        $"می‌توانید {plan.ExtraDownloadCountPack} دانلود با حجم {ByteUnits.FormatVolume(plan.ExtraDownloadBytesPack)} " +
        $"را به قیمت {TomanCurrency.FormatToman(GetPackPriceToman(plan))} تهیه کنید.";

    public static long GetUnifiedMaxFileBytes(PlanPricing plan) =>
        plan.MaxFileBytesYouTube > 0 ? plan.MaxFileBytesYouTube : plan.MaxFileBytesPinterest;
}
