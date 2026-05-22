namespace ProPlusBot.Services.Media;

public static class MediaConstants
{
    /// <summary>Maximum grid cells (5×5). Actual page size comes from bot settings.</summary>
    public const int MaxSearchResultsPerPage = 25;

    public const string SearchButtonText = "🔍 جستجو";
    public const string YouTubeSearchButtonText = "▶️ جستجوی یوتیوب";
    public const string PinterestSearchButtonText = "📌 جستجوی پینترست";

    public const string DirectLinkHelpTitle = "🔗 ارسال لینک";

    public const string CallbackSearchYouTube = "search:yt";
    public const string CallbackSearchPinterest = "search:pin";

    public const string CallbackYouTubePrefix = "yt:";
    public const string CallbackPinterestPrefix = "pin:";

    public const string CallbackNextPage = "next";
    public const string NextPageButtonText = "صفحه بعد ▶";
}
