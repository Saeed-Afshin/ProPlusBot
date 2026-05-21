using System.Runtime.InteropServices;

namespace ProPlusBot.Services.Media;

internal readonly record struct FfmpegDownloadAsset(
    Uri DownloadUrl,
    string ArchiveFileName,
    FfmpegArchiveKind ArchiveKind);

internal enum FfmpegArchiveKind
{
    Zip,
    TarXz
}

internal static class FfmpegPlatformAssets
{
    private const string ReleaseBase =
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/";

    public static bool TryGetAsset(out FfmpegDownloadAsset? asset)
    {
        asset = null;

        if (OperatingSystem.IsWindows())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "ffmpeg-master-latest-winarm64-gpl.zip",
                _ => "ffmpeg-master-latest-win64-gpl.zip"
            };
            asset = new FfmpegDownloadAsset(new Uri(ReleaseBase + name), name, FfmpegArchiveKind.Zip);
            return true;
        }

        if (OperatingSystem.IsLinux())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "ffmpeg-master-latest-linuxarm64-gpl.tar.xz",
                _ => "ffmpeg-master-latest-linux64-gpl.tar.xz"
            };
            asset = new FfmpegDownloadAsset(new Uri(ReleaseBase + name), name, FfmpegArchiveKind.TarXz);
            return true;
        }

        if (OperatingSystem.IsMacOS())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "ffmpeg-master-latest-macosarm64-gpl.zip",
                _ => "ffmpeg-master-latest-macos64-gpl.zip"
            };
            asset = new FfmpegDownloadAsset(new Uri(ReleaseBase + name), name, FfmpegArchiveKind.Zip);
            return true;
        }

        return false;
    }

    public static string ExecutableName =>
        OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
}
