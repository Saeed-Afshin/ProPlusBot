namespace ProPlusBot.Models;

public record ErrorLogListItemDto(
    Guid Id,
    long? TelegramUserId,
    string? PhoneNumber,
    string Title,
    DateTime CreatedAt);

public record ErrorLogDetailDto(
    Guid Id,
    long? TelegramUserId,
    string? PhoneNumber,
    string Title,
    string Detail,
    string? Source,
    DateTime CreatedAt);
