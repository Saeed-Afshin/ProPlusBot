namespace ProPlusBot.Services.Media;

public readonly record struct MediaToolDownloadResult(string? FilePath, string? ErrorDetail)
{
    public bool Success => FilePath is not null;

    public static MediaToolDownloadResult Ok(string filePath) => new(filePath, null);

    public static MediaToolDownloadResult Failed(string errorDetail) => new(null, errorDetail);
}
