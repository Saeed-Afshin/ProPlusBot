using ProPlusBot.Entities;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Services;

/// <summary>Canonical service keys stored on <see cref="Entities.ErrorLog.Service"/>.</summary>
public static class ErrorLogServices
{
    public const string YouTube = "youtube";
    public const string Pinterest = "pinterest";
    public const string Subscription = "subscription";
    public const string Bot = "bot";

    public static string? FromDetectedMediaPlatform(DetectedMediaPlatform platform) =>
        platform switch
        {
            DetectedMediaPlatform.YouTube => YouTube,
            DetectedMediaPlatform.Pinterest => Pinterest,
            _ => null
        };

    public static string? FromMediaPlatformKind(MediaPlatformKind platform) =>
        platform switch
        {
            MediaPlatformKind.YouTube => YouTube,
            MediaPlatformKind.Pinterest => Pinterest,
            _ => null
        };

    public static string ToDisplayName(string? service) =>
        service switch
        {
            YouTube => "یوتیوب",
            Pinterest => "پینترست",
            Subscription => "اشتراک",
            Bot => "ربات",
            null or "" => "—",
            _ => service
        };
}
