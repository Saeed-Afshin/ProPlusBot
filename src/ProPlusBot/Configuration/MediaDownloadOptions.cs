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

    /// <summary>Netscape-format cookies.txt for YouTube (export from browser). Preferred on servers.</summary>
    public string? YouTubeCookiesFile { get; set; }

    /// <summary>yt-dlp --cookies-from-browser value (e.g. edge, chrome:Default). Dev machines only; close the browser first.</summary>
    public string? YouTubeCookiesFromBrowser { get; set; }

    /// <summary>Base64 of a Netscape cookies.txt (inline). Many hosts limit env vars to ~1–4 KB — prefer YouTubeCookiesBase64File or YouTubeCookiesFile.</summary>
    public string? YouTubeCookiesBase64 { get; set; }

    /// <summary>Path to a file containing one line of Base64 (e.g. tools/youtube-cookies.b64.txt). Short env var on the server.</summary>
    public string? YouTubeCookiesBase64File { get; set; }

    /// <summary>yt-dlp --extractor-args value. With cookies, prefer tv clients: youtube:player_client=tv,web_safari</summary>
    public string? YouTubeExtractorArgs { get; set; } = "youtube:player_client=tv,web_safari";

    /// <summary>yt-dlp --remote-components. Use ejs:github (or ejs:npm with deno/bun). Plain "ejs" is ignored by yt-dlp.</summary>
    public string? YouTubeRemoteComponents { get; set; } = "ejs:github";

    /// <summary>Download Deno into ToolsDirectory for YouTube EJS (no server install required).</summary>
    public bool AutoDownloadDeno { get; set; } = true;

    /// <summary>Optional full path to deno binary. Overrides auto-download when valid.</summary>
    public string? YouTubeDenoPath { get; set; }

    /// <summary>yt-dlp --js-runtimes. Leave empty or "deno" to use bundled/auto-downloaded Deno.</summary>
    public string? YouTubeJsRuntimes { get; set; }

    /// <summary>Optional youtube:po_token=… fragment appended to extractor-args (e.g. mweb.gvs+TOKEN). Prefer a PO Token Provider plugin when possible.</summary>
    public string? YouTubePoToken { get; set; }
}
