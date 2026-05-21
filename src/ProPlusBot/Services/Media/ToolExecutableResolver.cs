using System.IO.Compression;
using System.Runtime.InteropServices;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace ProPlusBot.Services.Media;

internal static class ToolExecutableResolver
{
    public static string ResolveToolsDirectory(IHostEnvironment hostEnvironment, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(hostEnvironment.ContentRootPath, configured);
        }

        return Path.Combine(hostEnvironment.ContentRootPath, "tools");
    }

    public static async Task<string?> ResolveExecutableAsync(
        string? configuredPath,
        string bundledFullPath,
        string pathName,
        bool autoDownload,
        Func<Task<string?>> downloadAsync,
        ILogger logger,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = await TryResolveFileAsync(configuredPath, ct);
            if (configured is not null)
                return configured;

            logger.LogWarning(
                "Configured tool path {Path} is not usable; trying bundled install and PATH",
                configuredPath);
        }

        var bundled = await TryResolveFileAsync(bundledFullPath, ct);
        if (bundled is not null)
            return bundled;

        var onPath = await TryResolveFileAsync(pathName, ct);
        if (onPath is not null)
            return onPath;

        if (!autoDownload)
            return null;

        var downloaded = await downloadAsync();
        if (downloaded is not null)
            return downloaded;

        logger.LogError(
            "Automatic download failed for {Tool}. Check outbound HTTPS and write permissions for the tools directory.",
            pathName);

        return null;
    }

    public static async Task<string?> TryResolveFileAsync(string pathOrName, CancellationToken ct)
    {
        if (File.Exists(pathOrName))
            return Path.GetFullPath(pathOrName);

        if (!Path.IsPathRooted(pathOrName))
        {
            var onPath = await WhichAsync(pathOrName, ct);
            if (onPath is not null)
                return onPath;
        }

        return null;
    }

    public static async Task<string?> TryResolveDirectoryAsync(string pathOrName, CancellationToken ct)
    {
        if (File.Exists(pathOrName))
            return Path.GetDirectoryName(Path.GetFullPath(pathOrName));

        if (!Path.IsPathRooted(pathOrName))
        {
            var onPath = await WhichAsync(pathOrName, ct);
            if (onPath is not null)
                return Path.GetDirectoryName(onPath);
        }

        return null;
    }

    public static async Task DownloadFileAsync(
        HttpClient client,
        Uri url,
        string destinationPath,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(destinationPath);
        await stream.CopyToAsync(file, ct);
    }

    public static void MakeExecutable(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(filePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // best effort
        }
    }

    public static async Task ExtractArchiveAsync(
        string archivePath,
        string destination,
        FfmpegArchiveKind kind,
        CancellationToken ct)
    {
        if (kind == FfmpegArchiveKind.Zip)
        {
            ZipFile.ExtractToDirectory(archivePath, destination);
            return;
        }

        await ExtractTarXzAsync(archivePath, destination, ct);
    }

    private static async Task ExtractTarXzAsync(string archivePath, string destination, CancellationToken ct)
    {
        Directory.CreateDirectory(destination);

        try
        {
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(archivePath);
                using var archive = ArchiveFactory.OpenArchive(stream);
                archive.WriteToDirectory(
                    destination,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
            }, ct);
            return;
        }
        catch (Exception managedEx)
        {
            var result = await ProcessRunner.RunAsync(
                "tar",
                ["-xJf", archivePath, "-C", destination],
                null,
                120,
                ct);

            if (result.Success)
                return;

            throw new InvalidOperationException(
                $"Failed to extract archive (managed: {managedEx.Message}; tar: {result.StdErr})",
                managedEx);
        }
    }

    private static async Task<string?> WhichAsync(string executable, CancellationToken ct)
    {
        var fileName = OperatingSystem.IsWindows() ? "where" : "which";
        var result = await ProcessRunner.RunAsync(fileName, [executable], null, 10, ct);
        if (!result.Success)
            return null;

        var line = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return line is not null && File.Exists(line) ? line : null;
    }
}
