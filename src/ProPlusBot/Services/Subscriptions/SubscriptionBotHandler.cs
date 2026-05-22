using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services;
using ProPlusBot.Services.Media;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Subscriptions;

public class SubscriptionBotHandler(
    QuotaService quotaService,
    SubscriptionService subscriptionService,
    PlanLifecycleService planLifecycle,
    PlanCatalogService planCatalog,
    BalePaymentService paymentService,
    MediaFileSender fileSender,
    BaleBotClientFactory clientFactory,
    ErrorLogService errorLog,
    AppDbContext db)
{
    public const string AccountButtonText = "حساب من";
    public const string PlansButtonText = "پلن‌ها";
    public const string UpgradeButtonText = "ارتقا پلن";
    public const string BuyPlanButtonText = "خرید پلن";
    public const string ExtraQuotaButtonText = "سهمیه اضافه";

    public const string CallbackMenuPrefix = "sub:menu:";
    public const string CallbackUpgradePrefix = "sub:up:";
    public const string CallbackBuyPrefix = "sub:buy:";
    public const string CallbackPlanViewPrefix = "sub:pv:";
    public const string CallbackExtraPrefix = "sub:ex:";
    public const string CallbackActivateReservedPrefix = "sub:rsv:";

    public async Task<bool> TryHandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        var text = message.Text?.Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        var userId = message.From!.Id;

        if (text == AccountButtonText)
        {
            await SendAccountSummaryAsync(bot, userId, ct);
            return true;
        }

        if (text == PlansButtonText)
        {
            await SendPlansMenuAsync(bot, userId, ct);
            return true;
        }

        if (text == UpgradeButtonText)
        {
            await SendUpgradeOptionsAsync(bot, userId, ct);
            return true;
        }

        if (text == BuyPlanButtonText)
        {
            await SendBuyPlanOptionsAsync(bot, userId, ct);
            return true;
        }

        if (text == ExtraQuotaButtonText)
        {
            await SendExtraQuotaOptionsAsync(bot, userId, ct);
            return true;
        }

        return false;
    }

    public async Task<bool> HandleCallbackQueryAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.From is null || callback.Data is null)
            return false;

        if (!IsSubscriptionCallback(callback.Data))
            return false;

        var bot = clientFactory.CreateClient();

        if (callback.Data.StartsWith(CallbackMenuPrefix, StringComparison.Ordinal))
        {
            var action = callback.Data[CallbackMenuPrefix.Length..];
            switch (action)
            {
                case "upgrade":
                    await SendUpgradeOptionsAsync(bot, callback.From.Id, ct);
                    break;
                case "buy":
                    await SendBuyPlanOptionsAsync(bot, callback.From.Id, ct);
                    break;
                case "extra":
                    await SendExtraQuotaOptionsAsync(bot, callback.From.Id, ct);
                    break;
            }

            return true;
        }

        if (callback.Data.StartsWith(CallbackPlanViewPrefix, StringComparison.Ordinal))
        {
            if (!TryParsePlanViewCallback(callback.Data, out var viewPlan, out var viewMode))
                return true;

            await SendPlanOfferDetailsAsync(bot, callback.From.Id, viewPlan, viewMode, ct);
            return true;
        }

        if (callback.Data.StartsWith(CallbackUpgradePrefix, StringComparison.Ordinal))
        {
            var planStr = callback.Data[CallbackUpgradePrefix.Length..];
            if (!Enum.TryParse<SubscriptionPlan>(planStr, out var targetPlan))
                return true;

            try
            {
                await paymentService.SendUpgradeInvoiceAsync(callback.From.Id, targetPlan, ct);
            }
            catch (Exception ex)
            {
                await errorLog.LogExceptionAsync(
                    callback.From.Id,
                    "خطا در ارسال فاکتور ارتقا پلن",
                    ex,
                    nameof(SubscriptionBotHandler),
                    ct);
                await fileSender.SendTextAsync(bot, callback.From.Id, ex.Message, ct);
            }

            return true;
        }

        if (callback.Data.StartsWith(CallbackBuyPrefix, StringComparison.Ordinal))
        {
            var planStr = callback.Data[CallbackBuyPrefix.Length..];
            if (!Enum.TryParse<SubscriptionPlan>(planStr, out var targetPlan))
                return true;

            try
            {
                await paymentService.SendPlanPurchaseInvoiceAsync(callback.From.Id, targetPlan, ct);
            }
            catch (Exception ex)
            {
                await errorLog.LogExceptionAsync(
                    callback.From.Id,
                    "خطا در ارسال فاکتور خرید پلن",
                    ex,
                    nameof(SubscriptionBotHandler),
                    ct);
                await fileSender.SendTextAsync(bot, callback.From.Id, ex.Message, ct);
            }

            return true;
        }

        if (callback.Data.StartsWith(CallbackActivateReservedPrefix, StringComparison.Ordinal))
        {
            var idStr = callback.Data[CallbackActivateReservedPrefix.Length..];
            if (!Guid.TryParse(idStr, out var reservationId))
                return true;

            var activated = await planLifecycle.ActivateReservedPlanAsync(callback.From.Id, reservationId, ct);
            await fileSender.SendTextAsync(
                bot,
                callback.From.Id,
                activated
                    ? "پلن رزرو شده با موفقیت فعال شد."
                    : "فعال‌سازی ممکن نیست. پلن فعال دارید یا رزرو یافت نشد.",
                ct);
            return true;
        }

        if (callback.Data.StartsWith(CallbackExtraPrefix, StringComparison.Ordinal))
        {
            var platformStr = callback.Data[CallbackExtraPrefix.Length..];
            if (!Enum.TryParse<MediaPlatformKind>(platformStr, out var platform))
                return true;

            try
            {
                await paymentService.SendExtraQuotaInvoiceAsync(callback.From.Id, platform, ct);
            }
            catch (Exception ex)
            {
                await errorLog.LogExceptionAsync(
                    callback.From.Id,
                    "خطا در ارسال فاکتور سهمیه اضافه",
                    ex,
                    nameof(SubscriptionBotHandler),
                    ct);
                await fileSender.SendTextAsync(bot, callback.From.Id, ex.Message, ct);
            }

            return true;
        }

        return false;
    }

    private static bool IsSubscriptionCallback(string data) =>
        data.StartsWith(CallbackMenuPrefix, StringComparison.Ordinal)
        || data.StartsWith(CallbackPlanViewPrefix, StringComparison.Ordinal)
        || data.StartsWith(CallbackUpgradePrefix, StringComparison.Ordinal)
        || data.StartsWith(CallbackBuyPrefix, StringComparison.Ordinal)
        || data.StartsWith(CallbackActivateReservedPrefix, StringComparison.Ordinal)
        || data.StartsWith(CallbackExtraPrefix, StringComparison.Ordinal);

    private static string PlanViewCallback(SubscriptionPlan plan, PlanOfferMode mode) =>
        $"{CallbackPlanViewPrefix}{plan}:{mode.ToString().ToLowerInvariant()}";

    private static bool TryParsePlanViewCallback(string data, out SubscriptionPlan plan, out PlanOfferMode mode)
    {
        plan = default;
        mode = default;

        if (!data.StartsWith(CallbackPlanViewPrefix, StringComparison.Ordinal))
            return false;

        var payload = data[CallbackPlanViewPrefix.Length..];
        var colon = payload.LastIndexOf(':');
        if (colon <= 0 || colon >= payload.Length - 1)
            return false;

        var planStr = payload[..colon];
        var modeStr = payload[(colon + 1)..];

        if (!Enum.TryParse<SubscriptionPlan>(planStr, out plan))
            return false;

        return Enum.TryParse<PlanOfferMode>(modeStr, ignoreCase: true, out mode);
    }

    private async Task SendPlansMenuAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        var rows = new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(UpgradeButtonText, $"{CallbackMenuPrefix}upgrade") },
            new[] { InlineKeyboardButton.WithCallbackData(BuyPlanButtonText, $"{CallbackMenuPrefix}buy") },
            new[] { InlineKeyboardButton.WithCallbackData(ExtraQuotaButtonText, $"{CallbackMenuPrefix}extra") }
        };

        await bot.SendMessage(
            userId,
            "گزینه مورد نظر را انتخاب کنید:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    private async Task SendAccountSummaryAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        var summary = await quotaService.GetAccountSummaryAsync(userId, ct);
        var lines = new List<string>
        {
            $"پلن فعال: {MediaPlatformMapper.ToDisplayName(summary.EffectivePlan)}"
        };

        if (summary.StoredPlan != summary.EffectivePlan)
            lines.Add($"پلن ثبت‌شده: {MediaPlatformMapper.ToDisplayName(summary.StoredPlan)}");

        if (summary.PlanExpiresAt is null)
            lines.Add("انقضا: —");
        else
            lines.Add($"انقضا: {PersianDateTimeHelper.ToShamsiDateString(summary.PlanExpiresAt)} {PersianDateTimeHelper.ToTimeString(summary.PlanExpiresAt)} (تهران)");

        if (!summary.HasSubscriptionAccess)
            lines.Add("وضعیت: دوره آزمایشی پایان یافته");
        else
            lines.Add(summary.IsBanned ? "وضعیت: مسدود" : summary.IsTrialActive ? "وضعیت: آزمایشی فعال" : "وضعیت: فعال");

        lines.Add($"تازه‌سازی ماهانه: اول هر ماه به وقت تهران ({PersianDateTimeHelper.ToShamsiMonthYearFromTehranLocal(IranTime.NowLocal)})");
        lines.Add(string.Empty);

        foreach (var q in summary.Quotas)
        {
            lines.Add($"▫️ {MediaPlatformMapper.ToDisplayName(q.Platform)}");
            lines.Add($"  دانلود ماهانه: {q.MonthlyDownloadCountUsed}/{q.MonthlyDownloadCountLimit}، {ByteUnits.FormatMegabytes(q.MonthlyBytesUsed)}/{ByteUnits.FormatMegabytes(q.MonthlyBytesLimit)}");
            lines.Add($"  جستجو ماهانه: {q.MonthlySearchUsed}/{q.MonthlySearchLimit}");
            lines.Add($"  حداکثر هر فایل: {ByteUnits.FormatMegabytes(q.MaxFileBytesLimit)}");
            if (q.ExtraCountRemaining > 0 || q.ExtraBytesRemaining > 0)
                lines.Add($"  سهمیه اضافه: {q.ExtraCountRemaining} دانلود، {ByteUnits.FormatMegabytes(q.ExtraBytesRemaining)}");
            lines.Add(string.Empty);
        }

        if (summary.ReservedPlans.Count > 0)
        {
            lines.Add("پلن‌های رزرو:");
            foreach (var r in summary.ReservedPlans)
                lines.Add($"• {MediaPlatformMapper.ToDisplayName(r.Plan)} — {r.DurationDays} روز");
            lines.Add(string.Empty);
        }

        var payments = await db.PaymentRecords.AsNoTracking()
            .Where(p => p.TelegramUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        if (payments.Count > 0)
        {
            lines.Add("آخرین پرداخت‌ها:");
            foreach (var p in payments)
            {
                var type = p.Type switch
                {
                    PaymentType.PlanUpgrade => "ارتقا",
                    PaymentType.PlanPurchase => "خرید پلن",
                    _ => "سهمیه اضافه"
                };
                var status = p.Status switch
                {
                    PaymentStatus.Completed => "موفق",
                    PaymentStatus.Pending => "در انتظار",
                    PaymentStatus.Failed => "ناموفق",
                    _ => p.Status.ToString()
                };
                lines.Add($"• {PersianDateTimeHelper.ToShamsiDateString(p.CreatedAt)} — {type} — {TomanCurrency.FormatToman(p.AmountToman)} — {status}");
            }
        }

        await fileSender.SendTextAsync(bot, userId, string.Join('\n', lines), ct);

        if (summary.ReservedPlans.Count > 0 && summary.IsTrialActive)
            await SendReservedPlanButtonsAsync(bot, userId, summary.ReservedPlans, ct);
    }

    private async Task SendReservedPlanButtonsAsync(
        ITelegramBotClient bot,
        long userId,
        IReadOnlyList<Models.ReservedPlanDto> reserved,
        CancellationToken ct)
    {
        var buttons = reserved
            .Select(r => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"فعال‌سازی {MediaPlatformMapper.ToDisplayName(r.Plan)}",
                    $"{CallbackActivateReservedPrefix}{r.Id}")
            })
            .ToList();

        await bot.SendMessage(
            userId,
            "برای فعال‌سازی یک پلن رزرو، دکمه زیر را بزنید:",
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: ct);
    }

    private async Task SendUpgradeOptionsAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == userId, ct);

        if (user is null)
            return;

        if (user.IsBanned)
        {
            await fileSender.SendTextAsync(bot, userId, "حساب شما مسدود شده است.", ct);
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var plan in Enum.GetValues<SubscriptionPlan>())
        {
            if (!subscriptionService.CanUserUpgradeTo(user.Plan, plan))
                continue;

            try
            {
                var diff = await subscriptionService.GetUpgradePriceDiffAsync(userId, plan, ct);
                var label = $"{MediaPlatformMapper.ToDisplayName(plan)} — {TomanCurrency.FormatToman(diff)}";
                buttons.Add([InlineKeyboardButton.WithCallbackData(label, PlanViewCallback(plan, PlanOfferMode.Upgrade))]);
            }
            catch
            {
                // skip
            }
        }

        if (buttons.Count == 0)
        {
            await fileSender.SendTextAsync(bot, userId, "ارتقایی در دسترس نیست.", ct);
            return;
        }

        await bot.SendMessage(
            userId,
            "برای مشاهده جزئیات و پرداخت، یک پلن را انتخاب کنید (ارتقا — پرداخت اختلاف قیمت):",
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: ct);
    }

    private async Task SendBuyPlanOptionsAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == userId, ct);

        if (user is null || user.IsBanned)
        {
            await fileSender.SendTextAsync(bot, userId, "حساب شما مسدود شده است.", ct);
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var plan in Enum.GetValues<SubscriptionPlan>())
        {
            if (plan == SubscriptionPlan.Free)
                continue;

            try
            {
                var price = await subscriptionService.GetPlanPurchasePriceAsync(plan, ct);
                var label = $"{MediaPlatformMapper.ToDisplayName(plan)} — {TomanCurrency.FormatToman(price)}";
                buttons.Add([InlineKeyboardButton.WithCallbackData(label, PlanViewCallback(plan, PlanOfferMode.Buy))]);
            }
            catch
            {
                // skip
            }
        }

        await bot.SendMessage(
            userId,
            "برای مشاهده جزئیات و پرداخت، یک پلن را انتخاب کنید:",
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: ct);
    }

    private async Task SendPlanOfferDetailsAsync(
        ITelegramBotClient bot,
        long userId,
        SubscriptionPlan plan,
        PlanOfferMode mode,
        CancellationToken ct)
    {
        long? priceToman = null;
        try
        {
            priceToman = mode == PlanOfferMode.Upgrade
                ? await subscriptionService.GetUpgradePriceDiffAsync(userId, plan, ct)
                : await subscriptionService.GetPlanPurchasePriceAsync(plan, ct);
        }
        catch (Exception ex)
        {
            await fileSender.SendTextAsync(bot, userId, ex.Message, ct);
            return;
        }

        var text = await planCatalog.BuildPlanDetailsMessageAsync(plan, mode, priceToman, ct);
        var payCallback = mode == PlanOfferMode.Upgrade
            ? $"{CallbackUpgradePrefix}{plan}"
            : $"{CallbackBuyPrefix}{plan}";
        var backCallback = mode == PlanOfferMode.Upgrade
            ? $"{CallbackMenuPrefix}upgrade"
            : $"{CallbackMenuPrefix}buy";

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("پرداخت و صدور فاکتور", payCallback) },
            new[] { InlineKeyboardButton.WithCallbackData("بازگشت", backCallback) }
        };

        await bot.SendMessage(
            userId,
            text + "\n\nدر صورت تأیید، دکمه پرداخت را بزنید:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    private async Task SendExtraQuotaOptionsAsync(ITelegramBotClient bot, long userId, CancellationToken ct)
    {
        var rows = Enum.GetValues<MediaPlatformKind>()
            .Select(p => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    MediaPlatformMapper.ToDisplayName(p),
                    $"{CallbackExtraPrefix}{p}")
            })
            .ToList();

        await bot.SendMessage(
            userId,
            "سهمیه اضافه برای کدام پلتفرم؟",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }
}
