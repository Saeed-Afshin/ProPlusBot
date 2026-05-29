using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using System.Text;

namespace ProPlusBot.Services.Subscriptions;

public enum PlanOfferMode
{
    Buy,
    Upgrade
}

public class PlanCatalogService(AppDbContext db, IOptions<PaymentOptions> paymentOptions)
{
    private readonly PaymentOptions _paymentOptions = paymentOptions.Value;

    public async Task<string> BuildPlanDetailsMessageAsync(
        SubscriptionPlan plan,
        PlanOfferMode mode,
        long? offerPriceToman,
        CancellationToken ct = default)
    {
        var row = await db.PlanPricings.AsNoTracking().FirstAsync(p => p.Plan == plan, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"📦 بسته {MediaPlatformMapper.ToDisplayName(plan)}");
        sb.AppendLine();
        sb.AppendLine($"مدت اشتراک: {_paymentOptions.PlanDurationDays} روز");
        sb.AppendLine("تازه‌سازی سهمیه: از زمان فعال‌سازی بسته");

        if (offerPriceToman is not null)
        {
            var priceLine = mode == PlanOfferMode.Upgrade
                ? $"مبلغ ارتقا: {TomanCurrency.FormatToman(offerPriceToman.Value)}"
                : $"قیمت: {TomanCurrency.FormatToman(offerPriceToman.Value)}";
            sb.AppendLine(priceLine);
        }

        if (mode == PlanOfferMode.Buy)
            sb.AppendLine("در صورت داشتن بسته فعال، پس از خرید در صف رزرو قرار می‌گیرد.");
        else
            sb.AppendLine("پس از پرداخت، در صورت بسته فعال بلافاصله اعمال می‌شود.");

        sb.AppendLine();
        sb.AppendLine("سهمیه ماهانه (مشترک یوتیوب و پینترست):");
        sb.AppendLine($"  • دانلود: {row.MonthlyDownloadCount} عدد");
        sb.AppendLine($"  • حجم: {ByteUnits.FormatVolume(row.MonthlyDownloadBytes)}");
        sb.AppendLine($"  • جستجو: {row.MonthlySearchCount} عدد");
        if (row.MonthlyTicketLimit > 0)
            sb.AppendLine($"  • تیکت پشتیبانی: {row.MonthlyTicketLimit} عدد");
        sb.AppendLine();
        sb.AppendLine($"  حداکثر هر فایل: {ByteUnits.FormatVolume(row.MaxFileBytes)}");
        if (PlanExtraPackHelper.IsPackAvailable(row))
            sb.AppendLine($"  سهمیه اضافه: {PlanExtraPackHelper.BuildOfferText(row)}");

        return sb.ToString().TrimEnd();
    }
}
