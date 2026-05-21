using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services;

public class UserAccessService(
    AppDbContext db,
    IOptions<BotOptions> botOptions,
    BotSettingsService settingsService,
    RoleResolverService roleResolver)
{
    private readonly BotOptions _botOptions = botOptions.Value;

    public async Task<bool> CanReceiveBotResponseAsync(long telegramUserId, CancellationToken ct = default)
    {
        var settings = await settingsService.GetAsync(ct);
        if (!settings.IsActive)
            return false;

        if (settings.Mode == BotMode.Live)
            return true;

        return await IsStaffUserAsync(telegramUserId, ct);
    }

    public Task<bool> IsPrivilegedUserAsync(long telegramUserId, CancellationToken ct = default) =>
        IsStaffUserAsync(telegramUserId, ct);

    public async Task<bool> IsStaffUserAsync(long telegramUserId, CancellationToken ct = default)
    {
        if (await db.AdminUsers.AsNoTracking().AnyAsync(
                a => a.TelegramUserId == telegramUserId
                     && a.IsActive
                     && (a.Role == AdminRole.SuperAdmin
                         || a.Role == AdminRole.Admin
                         || a.Role == AdminRole.Tester),
                ct))
            return true;

        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user?.ResolvedRole is AdminRole.SuperAdmin or AdminRole.Admin or AdminRole.Tester)
            return true;

        if (user is { HasSharedPhone: true, PhoneNumber: not null and not "" })
        {
            var role = await roleResolver.ResolveRoleAsync(telegramUserId, user.PhoneNumber, ct);
            if (role is AdminRole.SuperAdmin or AdminRole.Admin or AdminRole.Tester)
                return true;
        }

        return false;
    }

    public async Task<bool> HasCompletedOnboardingAsync(long telegramUserId, CancellationToken ct = default)
    {
        var settings = await settingsService.GetAsync(ct);
        if (settings.Mode == BotMode.Test && await IsStaffUserAsync(telegramUserId, ct))
            return true;

        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        return user is { HasSharedPhone: true, HasJoinedChannel: true };
    }

    public async Task UpdatePhoneAsync(long telegramUserId, string phoneNumber, CancellationToken ct = default)
    {
        var normalized = PhoneNormalizer.Normalize(phoneNumber);
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.PhoneNumber = normalized;
        user.HasSharedPhone = true;
        user.ResolvedRole = await roleResolver.ResolveRoleAsync(telegramUserId, phoneNumber, ct);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> CheckChannelMembershipAsync(
        ITelegramBotClient bot,
        long telegramUserId,
        CancellationToken ct = default)
    {
        try
        {
            var chatId = new ChatId(_botOptions.RequiredChannelUsername);
            var member = await bot.GetChatMember(chatId, telegramUserId, ct);
            var joined = member.Status is ChatMemberStatus.Creator
                or ChatMemberStatus.Administrator
                or ChatMemberStatus.Member
                or ChatMemberStatus.Restricted;

            var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
            if (user is not null)
            {
                user.HasJoinedChannel = joined;
                user.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return joined;
        }
        catch
        {
            try
            {
                var chatId = new ChatId(_botOptions.RequiredChannelId);
                var member = await bot.GetChatMember(chatId, telegramUserId, ct);
                return member.Status is ChatMemberStatus.Creator
                    or ChatMemberStatus.Administrator
                    or ChatMemberStatus.Member
                    or ChatMemberStatus.Restricted;
            }
            catch
            {
                return false;
            }
        }
    }

    public const string RestartButtonText = "شروع مجدد";

    public static bool IsStartCommand(string? text) =>
        !string.IsNullOrWhiteSpace(text) && (
            text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/status", StringComparison.OrdinalIgnoreCase) ||
            text.Trim() == RestartButtonText);

    public static ReplyKeyboardMarkup PhoneRequestKeyboard() =>
        new([[KeyboardButton.WithRequestContact("اشتراک شماره تماس")]])
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

    public static ReplyKeyboardMarkup RestartKeyboard() =>
        new([[new KeyboardButton(RestartButtonText)]])
        {
            ResizeKeyboard = true
        };

    public string OnboardingMessage() =>
        $"برای استفاده از ربات، شماره تماس را فقط با دکمه «اشتراک شماره تماس» ارسال کنید (ارسال متنی پذیرفته نیست)، سپس در کانال {_botOptions.RequiredChannelUsername} عضو شوید.";
}
