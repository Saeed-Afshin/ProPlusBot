namespace ProPlusBot.Services.Media;

public enum MediaDeliveryMethod
{
    Messenger,
    FallbackLink
}

public sealed record MediaDeliveryResult(
    bool Success,
    MediaDeliveryMethod? Method,
    long FileSizeBytes,
    string? FallbackUrl);
