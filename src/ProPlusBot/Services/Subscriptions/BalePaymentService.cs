using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;

namespace ProPlusBot.Services.Subscriptions;

public class BalePaymentService(
    BaleBotClientFactory clientFactory,
    BotSettingsService botSettingsService,
    UserAccessService userAccess,
    SubscriptionService subscriptionService,
    AppDbContext db,
    IOptions<PaymentOptions> paymentOptions,
    ILogger<BalePaymentService> logger)
{
    private readonly PaymentOptions _paymentOptions = paymentOptions.Value;

    public async Task SendUpgradeInvoiceAsync(long chatId, SubscriptionPlan targetPlan, CancellationToken ct = default)
    {
        await EnsureNotBannedAsync(chatId, ct);
        var payment = await subscriptionService.CreateUpgradePaymentAsync(chatId, targetPlan, ct);
        await SendInvoiceAsync(
            chatId,
            $"ارتقا به {MediaPlatformMapper.ToDisplayName(targetPlan)}",
            $"پرداخت اختلاف قیمت برای ارتقا از بسته فعلی به {MediaPlatformMapper.ToDisplayName(targetPlan)}.",
            payment,
            ct);
    }

    public async Task SendPlanPurchaseInvoiceAsync(long chatId, SubscriptionPlan targetPlan, CancellationToken ct = default)
    {
        await EnsureNotBannedAsync(chatId, ct);
        var payment = await subscriptionService.CreatePlanPurchasePaymentAsync(chatId, targetPlan, ct);
        await SendInvoiceAsync(
            chatId,
            $"خرید بسته {MediaPlatformMapper.ToDisplayName(targetPlan)}",
            $"خرید یک ماه بسته {MediaPlatformMapper.ToDisplayName(targetPlan)}. در صورت داشتن بسته فعال، در صف رزرو قرار می‌گیرد.",
            payment,
            ct);
    }

    public async Task SendExtraQuotaInvoiceAsync(long chatId, MediaPlatformKind platform, CancellationToken ct = default)
    {
        await EnsureNotBannedAsync(chatId, ct);
        var payment = await subscriptionService.CreateExtraQuotaPaymentAsync(chatId, platform, ct);
        var pack = await db.ExtraQuotaPackSettings.AsNoTracking().FirstAsync(ct);
        await SendInvoiceAsync(
            chatId,
            $"سهمیه اضافه {MediaPlatformMapper.ToDisplayName(platform)}",
            $"افزودن {pack.ExtraDownloadCount} دانلود و {ByteUnits.FormatMegabytes(pack.ExtraDownloadBytes)} حجم.",
            payment,
            ct);
    }

    private async Task SendInvoiceAsync(
        long chatId,
        string title,
        string description,
        PaymentRecord payment,
        CancellationToken ct)
    {
        var bot = clientFactory.CreateClient();
        var token = await GetProviderTokenAsync(ct);
        var payload = JsonSerializer.Serialize(new { pid = payment.Id });

        await bot.SendInvoice(
            chatId,
            title: title,
            description: description,
            payload: payload,
            currency: payment.Currency,
            prices: [new LabeledPrice("مبلغ", TomanCurrency.ToInvoiceAmount(payment.AmountToman))],
            providerToken: token,
            cancellationToken: ct);
    }

    public async Task HandlePreCheckoutQueryAsync(PreCheckoutQuery query, CancellationToken ct = default)
    {
        var bot = clientFactory.CreateClient();

        var settings = await botSettingsService.GetAsync(ct);
        if (!settings.IsActive)
        {
            await AnswerPreCheckoutAsync(bot, query.Id, ok: false, "ربات موقتاً غیرفعال است.", ct);
            return;
        }

        if (!await userAccess.CanReceiveBotResponseAsync(query.From.Id, ct))
        {
            await AnswerPreCheckoutAsync(
                bot,
                query.Id,
                ok: false,
                "در حالت تست فقط کاربران مجاز می‌توانند پرداخت کنند.",
                ct);
            return;
        }

        var ok = false;
        var message = "پرداخت نامعتبر است.";

        try
        {
            await subscriptionService.ExpireStalePendingPaymentsAsync(query.From.Id, ct);

            if (TryParsePaymentId(query.InvoicePayload, out var paymentId))
            {
                var payment = await db.PaymentRecords.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

                if (payment is { Status: PaymentStatus.Pending }
                    && payment.TelegramUserId == query.From.Id
                    && payment.AmountRials == query.TotalAmount)
                {
                    ok = true;
                }
                else if (payment is not null)
                {
                    logger.LogWarning(
                        "PreCheckout rejected for {PaymentId}: status={Status}, userMatch={UserMatch}, amountExpected={Expected}, amountGot={Got}",
                        paymentId,
                        payment.Status,
                        payment.TelegramUserId == query.From.Id,
                        payment.AmountRials,
                        query.TotalAmount);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PreCheckout validation failed");
        }

        await AnswerPreCheckoutAsync(bot, query.Id, ok, ok ? null : message, ct);
    }

    private static Task AnswerPreCheckoutAsync(
        ITelegramBotClient bot,
        string preCheckoutQueryId,
        bool ok,
        string? errorMessage,
        CancellationToken ct) =>
        bot.SendRequest(
            new AnswerPreCheckoutQueryRequest
            {
                PreCheckoutQueryId = preCheckoutQueryId,
                Ok = ok,
                ErrorMessage = ok ? null : errorMessage ?? "پرداخت نامعتبر است."
            },
            ct);

    public async Task HandleSuccessfulPaymentAsync(Message message, CancellationToken ct = default)
    {
        if (message.From is null || message.SuccessfulPayment is null)
            return;

        if (!TryParsePaymentId(message.SuccessfulPayment.InvoicePayload, out var paymentId))
            return;

        var payment = await db.PaymentRecords.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
            return;

        if (payment.TelegramUserId != message.From.Id)
            return;

        if (payment.AmountRials != message.SuccessfulPayment.TotalAmount)
        {
            logger.LogWarning(
                "Payment amount mismatch for {PaymentId}: expected {Expected} Rials, got {Actual}",
                paymentId, payment.AmountRials, message.SuccessfulPayment.TotalAmount);
            return;
        }

        var fulfillment = await subscriptionService.CompletePaymentAsync(
            payment,
            message.SuccessfulPayment.ProviderPaymentChargeId,
            message.SuccessfulPayment.TelegramPaymentChargeId,
            ct);

        var bot = clientFactory.CreateClient();
        var text = BuildPaymentSuccessMessage(payment, fulfillment);
        await bot.SendMessage(message.From.Id, text, cancellationToken: ct);
    }

    private static string BuildPaymentSuccessMessage(PaymentRecord payment, PlanFulfillmentResult? fulfillment)
    {
        if (payment.Type == PaymentType.ExtraQuota)
            return "پرداخت موفق بود. سهمیه اضافه شما فعال شد.";

        var planName = payment.ToPlan is not null
            ? MediaPlatformMapper.ToDisplayName(payment.ToPlan.Value)
            : "بسته";

        return fulfillment switch
        {
            PlanFulfillmentResult.Reserved =>
                $"پرداخت موفق بود. بسته «{planName}» در صف رزرو شما قرار گرفت. از «{SubscriptionBotHandler.AccountButtonText}» می‌توانید آن را فعال کنید.",
            PlanFulfillmentResult.Upgraded =>
                $"پرداخت موفق بود. بسته شما به «{planName}» ارتقا یافت.",
            PlanFulfillmentResult.Activated =>
                $"پرداخت موفق بود. بسته «{planName}» برای شما فعال شد.",
            _ => $"پرداخت موفق بود. بسته «{planName}» ثبت شد."
        };
    }

    private async Task EnsureNotBannedAsync(long chatId, CancellationToken ct)
    {
        var banned = await db.BotUsers.AsNoTracking()
            .AnyAsync(u => u.TelegramUserId == chatId && u.IsBanned, ct);

        if (banned)
            throw new InvalidOperationException("حساب شما مسدود شده است.");
    }

    private async Task<string> GetProviderTokenAsync(CancellationToken ct)
    {
        var settings = await botSettingsService.GetAsync(ct);
        return settings.Mode == BotMode.Test
            ? _paymentOptions.TestProviderToken
            : _paymentOptions.LiveProviderToken;
    }

    private static bool TryParsePaymentId(string? payload, out Guid paymentId)
    {
        paymentId = default;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("pid", out var pid)
                && Guid.TryParse(pid.GetString(), out paymentId))
                return true;
        }
        catch
        {
            // ignored
        }

        return false;
    }
}
