using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record AuthenticatedAdmin(
    Guid? Id,
    long TelegramUserId,
    string PhoneNumber,
    string? DisplayName,
    AdminRole Role,
    bool IsConfigSuperAdmin = false);
