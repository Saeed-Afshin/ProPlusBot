using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;
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

    public async Task<bool> HasSubscriptionAccessAsync(long telegramUserId, CancellationToken ct = default)
    {
        if (await IsStaffUserAsync(telegramUserId, ct))
            return true;

        var user = await db.BotUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        return user is not null && PlanLifecycleService.HasSubscriptionAccess(user);
    }

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
        bool joined;
        try
        {
            joined = await IsChannelMemberAsync(
                bot, new ChatId(_botOptions.RequiredChannelUsername), telegramUserId, ct);
        }
        catch
        {
            try
            {
                joined = await IsChannelMemberAsync(
                    bot, new ChatId(_botOptions.RequiredChannelId), telegramUserId, ct);
            }
            catch
            {
                joined = false;
            }
        }

        await PersistChannelMembershipAsync(telegramUserId, joined, ct);
        return joined;
    }

    private static async Task<bool> IsChannelMemberAsync(
        ITelegramBotClient bot,
        ChatId chatId,
        long telegramUserId,
        CancellationToken ct)
    {
        var member = await bot.GetChatMember(chatId, telegramUserId, ct);
        return IsActiveChannelMember(member);
    }

    private static bool IsActiveChannelMember(ChatMember member) =>
        member.Status is ChatMemberStatus.Creator
            or ChatMemberStatus.Administrator
            or ChatMemberStatus.Member
            or ChatMemberStatus.Restricted;

    private async Task PersistChannelMembershipAsync(
        long telegramUserId,
        bool joined,
        CancellationToken ct)
    {
        var user = await db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        if (user is null)
            return;

        user.HasJoinedChannel = joined;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public const string SharePhoneButtonText = "📱 اشتراک شماره تماس";
    public const string RestartButtonText = "🔄 شروع مجدد";

    public static bool IsStartCommand(string? text) =>
        !string.IsNullOrWhiteSpace(text) && (
            text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/status", StringComparison.OrdinalIgnoreCase) ||
            text.Trim() == RestartButtonText);

    public static ReplyKeyboardMarkup PhoneRequestKeyboard() =>
        new([[KeyboardButton.WithRequestContact(SharePhoneButtonText)]])
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

    public static ReplyKeyboardMarkup RestartKeyboard() =>
        new([[new KeyboardButton(RestartButtonText)]])
        {
            ResizeKeyboard = true
        };

    public string OnboardingMessage()
    {
        var joinUrl = GetChannelJoinUrl();
        var channelStep = joinUrl is not null
            ? $"سپس از لینک کانال عضو شوید:\n{joinUrl}"
            : $"سپس در کانال {_botOptions.RequiredChannelUsername} عضو شوید.";
        return $"برای استفاده از ربات، شماره تماس را فقط با دکمه «{SharePhoneButtonText}» ارسال کنید (ارسال متنی پذیرفته نیست)، {channelStep}";
    }

    public string ChannelJoinPromptMessage()
    {
        var joinUrl = GetChannelJoinUrl();
        if (joinUrl is not null)
        {
            return $"برای ادامه در کانال عضو شوید:\n{joinUrl}\n\nبعد از عضویت «{RestartButtonText}» را بزنید.";
        }

        return $"لطفاً در کانال {_botOptions.RequiredChannelUsername} عضو شوید و «{RestartButtonText}» را بزنید.";
    }

    public string? GetChannelJoinUrl() => NormalizeJoinUrl(_botOptions.RequiredChannelJoinUrl);

    private static string? NormalizeJoinUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return url;
    }
}
