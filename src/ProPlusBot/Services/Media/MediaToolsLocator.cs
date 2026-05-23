namespace ProPlusBot.Services.Media;

/// <summary>
/// Resolved paths for yt-dlp, gallery-dl, and ffmpeg. Populated at startup.
/// </summary>
public sealed class MediaToolsLocator
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string? YtDlpPath { get; private set; }
    public string? GalleryDlPath { get; private set; }
    public string? FfmpegDirectory { get; private set; }

    /// <summary>Full value for yt-dlp --js-runtimes (e.g. deno:/app/tools/deno/bin/deno).</summary>
    public string? JsRuntimesArg { get; private set; }

    public string? DenoPath { get; private set; }

    public bool HasYtDlp => IsExecutable(YtDlpPath);
    public bool HasGalleryDl => IsExecutable(GalleryDlPath);
    public bool HasYouTubeJsRuntime => !string.IsNullOrWhiteSpace(JsRuntimesArg);

    internal void Complete(
        string? ytDlpPath,
        string? galleryDlPath,
        string? ffmpegDirectory,
        string? jsRuntimesArg,
        string? denoPath)
    {
        YtDlpPath = ytDlpPath;
        GalleryDlPath = galleryDlPath;
        FfmpegDirectory = ffmpegDirectory;
        JsRuntimesArg = jsRuntimesArg;
        DenoPath = denoPath;
        _ready.TrySetResult();
    }

    public bool CanDownload(DetectedMediaPlatform platform) => platform switch
    {
        DetectedMediaPlatform.YouTube => HasYtDlp,
        DetectedMediaPlatform.Pinterest => HasGalleryDl || HasYtDlp,
        _ => false
    };

    public Task WaitReadyAsync(CancellationToken ct = default) =>
        _ready.Task.WaitAsync(ct);

    private static bool IsExecutable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}
