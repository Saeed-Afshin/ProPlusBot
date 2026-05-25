using ProPlusBot.Entities;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Models;

public record MediaDownloadJobListItemDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? DisplayName,
    string? PhoneNumber,
    DetectedMediaPlatform Platform,
    MediaDownloadSource Source,
    string SourceUrl,
    MediaDownloadJobStatus Status,
    string? ResultSummary,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record MediaDownloadJobDetailDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? DisplayName,
    string? PhoneNumber,
    DetectedMediaPlatform Platform,
    MediaDownloadSource Source,
    string SourceUrl,
    string? YouTubeFormatId,
    MediaDownloadJobStatus Status,
    string? ResultSummary,
    string? ErrorDetail,
    long? FileSizeBytes,
    long? IncomingChatMessageId,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record UserInteractionListItemDto(
    long Id,
    long TelegramUserId,
    string? Username,
    string? DisplayName,
    string? PhoneNumber,
    long? IncomingChatMessageId,
    Guid? MediaDownloadJobId,
    UserInteractionKind Kind,
    UserInteractionStatus Status,
    string InputSummary,
    string? ResultSummary,
    DateTime CreatedAt);
