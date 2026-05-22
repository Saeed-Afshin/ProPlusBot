using Microsoft.AspNetCore.Mvc.Rendering;
using ProPlusBot.Entities;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Services.Subscriptions;

public static class MediaPlatformMapper
{
    public static MediaPlatformKind ToKind(DetectedMediaPlatform platform) =>
        platform switch
        {
            DetectedMediaPlatform.YouTube => MediaPlatformKind.YouTube,
            DetectedMediaPlatform.Pinterest => MediaPlatformKind.Pinterest,
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };

    public static string ToDisplayName(MediaPlatformKind platform) =>
        platform switch
        {
            MediaPlatformKind.YouTube => "یوتیوب",
            MediaPlatformKind.Pinterest => "پینترست",
            _ => platform.ToString()
        };

    public static string ToDisplayName(SubscriptionPlan plan) =>
        plan switch
        {
            SubscriptionPlan.Free => "آزمایشی",
            SubscriptionPlan.Bronze => "برنزی",
            SubscriptionPlan.Silver => "نقره‌ای",
            SubscriptionPlan.Golden => "طلایی",
            _ => plan.ToString()
        };

    public static IEnumerable<SelectListItem> GetPlanSelectList(SubscriptionPlan? selected = null) =>
        Enum.GetValues<SubscriptionPlan>()
            .Select(p => new SelectListItem(ToDisplayName(p), p.ToString(), selected == p));
}
