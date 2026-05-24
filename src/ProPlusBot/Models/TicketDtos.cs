using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record SupportTicketListItemDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? PhoneNumber,
    SupportTicketStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastAdminReplyAt,
    string? LastAdminDisplayName,
    string Preview);

public record SupportTicketMessageDto(
    long Id,
    SupportTicketSender Sender,
    string Body,
    string? AdminDisplayName,
    DateTime CreatedAt);

public record SupportTicketDetailDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? PhoneNumber,
    SupportTicketStatus Status,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    DateTime? LastAdminReplyAt,
    string? LastAdminDisplayName,
    IReadOnlyList<SupportTicketMessageDto> Messages);
