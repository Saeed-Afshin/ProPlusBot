using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot.Types;

namespace ProPlusBot.Services;

public class ChatStorageService(AppDbContext db, TrialSettingsService trialSettings)
{
    public async Task<BotUser> EnsureUserAsync(User user, CancellationToken ct = default)
    {
        var entity = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == user.Id, ct);
        if (entity is null)
        {
            var trialDays = (await trialSettings.GetAsync(ct)).DurationDays;
            entity = new BotUser
            {
                TelegramUserId = user.Id,
                Plan = SubscriptionPlan.Free,
                PlanExpiresAt = DateTime.UtcNow.AddDays(trialDays),
                QuotaPeriodStartAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.BotUsers.Add(entity);
        }

        entity.Username = user.Username;
        entity.FirstName = user.FirstName;
        entity.LastName = user.LastName;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<long?> SaveIncomingAsync(Message message, CancellationToken ct = default)
    {
        if (message.From is null)
            return null;

        await EnsureUserAsync(message.From, ct);
        var text = message.Text ?? message.Caption;
        var type = message.Type.ToString().ToLowerInvariant();

        var entity = new ChatMessage
        {
            TelegramUserId = message.From.Id,
            Direction = MessageDirection.Incoming,
            Text = text,
            MessageType = type,
            TelegramMessageId = message.MessageId,
            RawPayload = JsonSerializer.Serialize(message),
            CreatedAt = DateTime.UtcNow
        };
        db.ChatMessages.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task SaveOutgoingAsync(
        long telegramUserId,
        string text,
        int? telegramMessageId,
        CancellationToken ct = default)
    {
        db.ChatMessages.Add(new ChatMessage
        {
            TelegramUserId = telegramUserId,
            Direction = MessageDirection.Outgoing,
            Text = text,
            MessageType = "text",
            TelegramMessageId = telegramMessageId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
