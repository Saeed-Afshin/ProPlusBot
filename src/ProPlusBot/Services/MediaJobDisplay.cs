using ProPlusBot.Entities;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Services;

public static class MediaJobDisplay
{
    public static string ToStatusName(MediaDownloadJobStatus status) =>
        status switch
        {
            MediaDownloadJobStatus.Pending => "در صف",
            MediaDownloadJobStatus.Processing => "در حال پردازش",
            MediaDownloadJobStatus.Completed => "موفق",
            MediaDownloadJobStatus.Failed => "ناموفق",
            _ => status.ToString()
        };

    public static string ToKindName(UserInteractionKind kind) =>
        kind switch
        {
            UserInteractionKind.LinkDetected => "شناسایی لینک",
            UserInteractionKind.SearchQuery => "جستجو",
            UserInteractionKind.SearchResultPick => "انتخاب نتیجه جستجو",
            UserInteractionKind.FormatListShown => "لیست کیفیت",
            UserInteractionKind.FormatListFailed => "خطای لیست کیفیت",
            UserInteractionKind.FormatSelected => "انتخاب کیفیت",
            UserInteractionKind.DownloadQueued => "صف دانلود",
            UserInteractionKind.DownloadCompleted => "دانلود موفق",
            UserInteractionKind.DownloadFailed => "دانلود ناموفق",
            UserInteractionKind.QuotaOrAccessBlocked => "محدودیت دسترسی",
            UserInteractionKind.CallbackAction => "دکمه",
            _ => "سایر"
        };

    public static string ToInteractionStatusName(UserInteractionStatus status) =>
        status switch
        {
            UserInteractionStatus.Info => "اطلاع",
            UserInteractionStatus.Pending => "در انتظار",
            UserInteractionStatus.Success => "موفق",
            UserInteractionStatus.Failed => "ناموفق",
            _ => status.ToString()
        };

    public static string ToPlatformName(DetectedMediaPlatform platform) =>
        platform switch
        {
            DetectedMediaPlatform.YouTube => "یوتیوب",
            DetectedMediaPlatform.Pinterest => "پینترست",
            _ => platform.ToString()
        };

    public static string ToSourceName(MediaDownloadSource source) =>
        source switch
        {
            MediaDownloadSource.Url => "لینک مستقیم",
            MediaDownloadSource.YouTubeSearch => "جستجوی یوتیوب",
            MediaDownloadSource.PinterestSearch => "جستجوی پینترست",
            _ => source.ToString()
        };
}
