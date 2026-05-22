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
        var limits = await db.PlanPlatformLimits.AsNoTracking()
            .Where(l => l.Plan == plan
                && (l.Period == UsagePeriod.Monthly || l.LimitKind == QuotaLimitKind.MaxFileBytes))
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"📦 پلن {MediaPlatformMapper.ToDisplayName(plan)}");
        sb.AppendLine();
        sb.AppendLine($"مدت اشتراک: {_paymentOptions.PlanDurationDays} روز");
        sb.AppendLine($"تازه‌سازی سهمیه ماهانه: اول هر ماه به وقت تهران");

        if (offerPriceToman is not null)
        {
            var priceLine = mode == PlanOfferMode.Upgrade
                ? $"مبلغ ارتقا: {TomanCurrency.FormatToman(offerPriceToman.Value)}"
                : $"قیمت: {TomanCurrency.FormatToman(offerPriceToman.Value)}";
            sb.AppendLine(priceLine);
        }

        if (mode == PlanOfferMode.Buy)
            sb.AppendLine("در صورت داشتن پلن فعال، پس از خرید در صف رزرو قرار می‌گیرد.");
        else
            sb.AppendLine("پس از پرداخت، در صورت پلن فعال بلافاصله اعمال می‌شود.");

        sb.AppendLine();

        foreach (var platform in Enum.GetValues<MediaPlatformKind>())
        {
            sb.AppendLine($"▫️ {MediaPlatformMapper.ToDisplayName(platform)}");

            var downloadCount = GetLimit(limits, platform, UsagePeriod.Monthly, QuotaLimitKind.DownloadCount);
            var downloadBytes = GetLimit(limits, platform, UsagePeriod.Monthly, QuotaLimitKind.DownloadBytes);
            var searchCount = GetLimit(limits, platform, UsagePeriod.Monthly, QuotaLimitKind.SearchCount);
            var maxFile = GetLimit(limits, platform, UsagePeriod.Daily, QuotaLimitKind.MaxFileBytes);
            if (maxFile == 0)
                maxFile = GetLimit(limits, platform, UsagePeriod.Monthly, QuotaLimitKind.MaxFileBytes);

            sb.AppendLine($"  • دانلود ماهانه: {downloadCount} عدد");
            sb.AppendLine($"  • حجم ماهانه: {ByteUnits.FormatMegabytes(downloadBytes)}");
            sb.AppendLine($"  • جستجو ماهانه: {searchCount} عدد");
            sb.AppendLine($"  • حداکثر هر فایل: {ByteUnits.FormatMegabytes(maxFile)}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static long GetLimit(
        IReadOnlyList<PlanPlatformLimit> limits,
        MediaPlatformKind platform,
        UsagePeriod period,
        QuotaLimitKind kind) =>
        limits.FirstOrDefault(l => l.Platform == platform && l.Period == period && l.LimitKind == kind)
            ?.LimitValue
        ?? 0;
}
