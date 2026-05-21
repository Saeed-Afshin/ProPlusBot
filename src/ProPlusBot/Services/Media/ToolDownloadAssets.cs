using System.Runtime.InteropServices;

namespace ProPlusBot.Services.Media;

internal readonly record struct ToolDownloadAsset(Uri DownloadUrl, string FileName);

internal static class YtDlpDownloadAssets
{
    private const string LatestBase = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/";

    public static bool TryGetAsset(out ToolDownloadAsset? asset)
    {
        asset = null;

        if (OperatingSystem.IsWindows())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "yt-dlp_x86.exe",
                Architecture.Arm64 => "yt-dlp_arm64.exe",
                _ => "yt-dlp.exe"
            };
            asset = new ToolDownloadAsset(new Uri(LatestBase + name), name);
            return true;
        }

        if (OperatingSystem.IsLinux())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "yt-dlp_linux_aarch64",
                _ => "yt-dlp_linux"
            };
            asset = new ToolDownloadAsset(new Uri(LatestBase + name), name);
            return true;
        }

        if (OperatingSystem.IsMacOS())
        {
            asset = new ToolDownloadAsset(new Uri(LatestBase + "yt-dlp_macos"), "yt-dlp_macos");
            return true;
        }

        return false;
    }

    public static string InstalledName =>
        OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
}

internal static class GalleryDlDownloadAssets
{
    /// <summary>
    /// Codeberg uses /releases/download/{tag}/file, not GitHub-style /releases/latest/download/.
    /// </summary>
    private const string ReleaseTag = "v1.32.1";
    private const string ReleaseBase = "https://codeberg.org/mikf/gallery-dl/releases/download/";

    public static bool TryGetAsset(out ToolDownloadAsset? asset)
    {
        asset = null;

        if (OperatingSystem.IsWindows())
        {
            asset = new ToolDownloadAsset(
                new Uri($"{ReleaseBase}{ReleaseTag}/gallery-dl.exe"),
                "gallery-dl.exe");
            return true;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            asset = new ToolDownloadAsset(
                new Uri($"{ReleaseBase}{ReleaseTag}/gallery-dl.bin"),
                "gallery-dl.bin");
            return true;
        }

        return false;
    }

    public static string InstalledName =>
        OperatingSystem.IsWindows() ? "gallery-dl.exe" : "gallery-dl";
}
