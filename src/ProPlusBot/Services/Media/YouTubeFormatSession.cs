namespace ProPlusBot.Services.Media;

public sealed record YouTubeFormatSession(
    string Url,
    IReadOnlyList<YouTubeFormatOption> Formats,
    MediaDownloadSource Source,
    long MaxFileBytesForPlan,
    int Page = 0,
    long? IncomingChatMessageId = null,
    int? FormatMenuMessageId = null)
{
    public const int FormatsPerPage = MediaConstants.YouTubeFormatsPerPage;
    public int PageCount => Math.Max(1, (Formats.Count + FormatsPerPage - 1) / FormatsPerPage);

    public IReadOnlyList<YouTubeFormatOption> GetPageFormats()
    {
        var skip = Page * FormatsPerPage;
        return Formats.Skip(skip).Take(FormatsPerPage).ToList();
    }

    public int ToGlobalIndex(int pageLocalIndex) => Page * FormatsPerPage + pageLocalIndex;
}
