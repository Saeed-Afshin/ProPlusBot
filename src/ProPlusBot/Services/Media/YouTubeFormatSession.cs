namespace ProPlusBot.Services.Media;

public sealed record YouTubeFormatSession(
    string Url,
    IReadOnlyList<YouTubeFormatOption> Formats,
    MediaDownloadSource Source,
    long MaxFileBytesForPlan,
    int Page = 0)
{
    public const int FormatsPerPage = 8;
    public int PageCount => Math.Max(1, (Formats.Count + FormatsPerPage - 1) / FormatsPerPage);

    public IReadOnlyList<YouTubeFormatOption> GetPageFormats()
    {
        var skip = Page * FormatsPerPage;
        return Formats.Skip(skip).Take(FormatsPerPage).ToList();
    }

    public int ToGlobalIndex(int pageLocalIndex) => Page * FormatsPerPage + pageLocalIndex;
}
