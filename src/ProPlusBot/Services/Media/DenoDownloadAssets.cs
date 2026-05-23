using System.Runtime.InteropServices;

namespace ProPlusBot.Services.Media;

internal readonly record struct DenoDownloadAsset(Uri DownloadUrl, string ArchiveFileName, string ExecutableName);

internal static class DenoDownloadAssets
{
    private const string LatestBase = "https://github.com/denoland/deno/releases/latest/download/";

    public static string InstalledName =>
        OperatingSystem.IsWindows() ? "deno.exe" : "deno";

    public static bool TryGetAsset(out DenoDownloadAsset? asset)
    {
        asset = null;

        if (OperatingSystem.IsWindows())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "deno-x86_64-pc-windows-msvc.zip",
                Architecture.Arm64 => "deno-aarch64-pc-windows-msvc.zip",
                _ => "deno-x86_64-pc-windows-msvc.zip"
            };
            asset = new DenoDownloadAsset(new Uri(LatestBase + name), name, "deno.exe");
            return true;
        }

        if (OperatingSystem.IsLinux())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "deno-aarch64-unknown-linux-gnu.zip",
                _ => "deno-x86_64-unknown-linux-gnu.zip"
            };
            asset = new DenoDownloadAsset(new Uri(LatestBase + name), name, "deno");
            return true;
        }

        if (OperatingSystem.IsMacOS())
        {
            var name = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "deno-aarch64-apple-darwin.zip",
                _ => "deno-x86_64-apple-darwin.zip"
            };
            asset = new DenoDownloadAsset(new Uri(LatestBase + name), name, "deno");
            return true;
        }

        return false;
    }
}
