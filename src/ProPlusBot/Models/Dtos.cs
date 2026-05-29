using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record AdminUserDto(Guid Id, long TelegramUserId, string PhoneNumber, string? DisplayName, AdminRole Role, bool IsActive);

public record CreateAdminRequest(long TelegramUserId, string PhoneNumber, string? DisplayName, AdminRole Role);

public record BotSettingsDto(
    BotMode Mode,
    BotUpdateMode UpdateMode,
    bool IsActive,
    bool YouTubeEnabled,
    bool PinterestEnabled,
    int SearchGridColumns,
    int SearchGridRows,
    int SearchGridJpegQuality,
    ConversationStateBackend ConversationStateBackend,
    long BaleDirectArvanThresholdBytes,
    string? WebhookUrl,
    DateTime UpdatedAt);

public record UpdateBotSettingsRequest(
    BotMode? Mode,
    BotUpdateMode? UpdateMode,
    bool? IsActive,
    bool? YouTubeEnabled,
    bool? PinterestEnabled,
    int? SearchGridColumns,
    int? SearchGridRows,
    int? SearchGridJpegQuality,
    ConversationStateBackend? ConversationStateBackend,
    long? BaleDirectArvanThresholdBytes,
    string? WebhookUrl);

public record ChatMessageDto(long Id, long TelegramUserId, MessageDirection Direction, string? Text, string MessageType, DateTime CreatedAt);

public record SendOtpRequest(string PhoneNumber);

public record VerifyOtpRequest(string PhoneNumber, string Code);
