using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;

namespace ProPlusBot.Services.Messaging;

public sealed record AdminBulkResult(int Sent, int Failed, int Skipped, IReadOnlyList<string> Errors);

public class AdminMessagingService(
    AppDbContext db,
    BaleBotClientFactory clientFactory,
    ChatStorageService chatStorage,
    ILogger<AdminMessagingService> logger)
{
    public async Task<bool> TrySendToUserAsync(
        long telegramUserId,
        string template,
        IReadOnlyDictionary<string, string>? extraPlaceholders = null,
        CancellationToken ct = default)
    {
        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user is null)
            return false;

        if (user.IsBanned)
            return false;

        var text = MessageTemplateHelper.Render(template, user, extraPlaceholders);
        return await SendTextAsync(telegramUserId, text, ct);
    }

    public Task NotifyAdminPlanChangedAsync(long telegramUserId, CancellationToken ct = default) =>
        TrySendToUserAsync(
            telegramUserId,
            "بسته شما به «{Plan}» تغییر کرد.\nاعتبار تا {PlanExpiresDate} ساعت {PlanExpiresTime}.",
            ct: ct);

    public Task NotifyAdminPlanExtendedAsync(long telegramUserId, int extraDays, CancellationToken ct = default) =>
        TrySendToUserAsync(
            telegramUserId,
            "اشتراک شما {ExtendDays} روز تمدید شد.\nاعتبار تا {PlanExpiresDate} ساعت {PlanExpiresTime}.",
            new Dictionary<string, string> { [MessageTemplateHelper.ExtendDays] = extraDays.ToString() },
            ct);

    public async Task<AdminBulkResult> BroadcastAsync(
        IReadOnlyList<long> telegramUserIds,
        string template,
        CancellationToken ct = default)
    {
        if (telegramUserIds.Count == 0)
            return new AdminBulkResult(0, 0, 0, ["هیچ کاربری انتخاب نشده است."]);

        var users = await db.BotUsers.AsNoTracking()
            .Where(u => telegramUserIds.Contains(u.TelegramUserId))
            .ToDictionaryAsync(u => u.TelegramUserId, ct);

        var sent = 0;
        var failed = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var id in telegramUserIds.Distinct())
        {
            if (!users.TryGetValue(id, out var user))
            {
                skipped++;
                continue;
            }

            if (user.IsBanned)
            {
                skipped++;
                continue;
            }

            var text = MessageTemplateHelper.Render(template, user);
            if (await SendTextAsync(id, text, ct))
                sent++;
            else
            {
                failed++;
                if (errors.Count < 10)
                    errors.Add($"ارسال به {id} ناموفق بود.");
            }

            await Task.Delay(35, ct);
        }

        return new AdminBulkResult(sent, failed, skipped, errors);
    }

    public async Task<IReadOnlyList<long>> GetAllUserIdsAsync(CancellationToken ct = default) =>
        await db.BotUsers.AsNoTracking()
            .Select(u => u.TelegramUserId)
            .ToListAsync(ct);

    private async Task<bool> SendTextAsync(long chatId, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var bot = clientFactory.CreateClient();
            var message = await bot.SendMessage(chatId, text, cancellationToken: ct);
            await chatStorage.SaveOutgoingAsync(chatId, text, message.MessageId, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin message send failed for user {UserId}", chatId);
            return false;
        }
    }
}
