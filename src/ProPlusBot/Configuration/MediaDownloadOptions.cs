namespace ProPlusBot.Configuration;

public class MediaDownloadOptions
{
    public const string SectionName = "MediaDownload";

    /// <summary>Executable name or full path to yt-dlp.</summary>
    public string YtDlpPath { get; set; } = "yt-dlp";

    /// <summary>Executable name or full path to gallery-dl (Pinterest image fallback).</summary>
    public string GalleryDlPath { get; set; } = "gallery-dl";

    /// <summary>Items fetched per search page (9; matches <see cref="MediaConstants.SearchResultsPerPage"/>).</summary>
    public int MaxSearchResults { get; set; } = 9;

    /// <summary>HTTP timeout for Pinterest unofficial search API.</summary>
    public int PinterestSearchTimeoutSeconds { get; set; } = 30;

    /// <summary>Max file size to send via Bale (bytes). Default ~49 MB.</summary>
    public long MaxUploadBytes { get; set; } = 49 * 1024 * 1024;

    public int ProcessTimeoutSeconds { get; set; } = 600;

    public string YouTubeFormat { get; set; } =
        "bv*[height<=720]+ba/b[height<=720]/bestvideo[height<=720]+bestaudio/best[height<=720]/best";

    public string YouTubeAudioFormat { get; set; } = "bestaudio/best";

    /// <summary>Optional full path to ffmpeg binary or its directory. Overrides auto-download when valid.</summary>
    public string? FfmpegPath { get; set; }

    /// <summary>Download yt-dlp into ToolsDirectory when not found on PATH.</summary>
    public bool AutoDownloadYtDlp { get; set; } = true;

    /// <summary>Download gallery-dl into ToolsDirectory when not found on PATH.</summary>
    public bool AutoDownloadGalleryDl { get; set; } = true;

    /// <summary>Download a static ffmpeg build into ToolsDirectory when not found on PATH.</summary>
    public bool AutoDownloadFfmpeg { get; set; } = true;

    /// <summary>Folder for bundled tools (relative to content root unless absolute).</summary>
    public string ToolsDirectory { get; set; } = "tools";
}
