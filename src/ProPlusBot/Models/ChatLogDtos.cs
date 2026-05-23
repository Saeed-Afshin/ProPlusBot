using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record ChatUserSummaryDto(
    long TelegramUserId,
    string? Username,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    int MessageCount,
    DateTime LastMessageAt,
    string? LastMessagePreview,
    MessageDirection LastMessageDirection);

public record ChatUserHeaderDto(
    long TelegramUserId,
    string? Username,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    int MessageCount);
