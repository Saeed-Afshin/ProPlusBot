using ProPlusBot.Services;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services.Media;

public class MediaDownloadBackgroundService(
    MediaDownloadQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MediaDownloadBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<MediaDownloadProcessor>();
                await processor.ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Media download job failed for chat {ChatId}", job.ChatId);
                try
                {
                    using var logScope = scopeFactory.CreateScope();
                    var errorLog = logScope.ServiceProvider.GetRequiredService<ErrorLogService>();
                    await errorLog.LogExceptionAsync(
                        job.ChatId,
                        "خطا در صف دانلود رسانه",
                        ex,
                        nameof(MediaDownloadBackgroundService),
                        stoppingToken);
                }
                catch (Exception logEx)
                {
                    logger.LogWarning(logEx, "Failed to persist error log for chat {ChatId}", job.ChatId);
                }
            }
        }
    }
}

public class MediaDownloadProcessor(
    BaleBotClientFactory clientFactory,
    MediaToolsLocator tools,
    YtDlpService ytDlp,
    GalleryDlService galleryDl,
    MediaFileSender fileSender,
    QuotaService quotaService,
    UserAccessService userAccess,
    ErrorLogService errorLog,
    ILogger<MediaDownloadProcessor> logger)
{
    public async Task ProcessAsync(MediaDownloadJob job, CancellationToken ct)
    {
        var bot = clientFactory.CreateClient();
        var tempDir = Path.Combine(Path.GetTempPath(), "ProPlusBot", "media", Guid.NewGuid().ToString("N"));

        try
        {
            await tools.WaitReadyAsync(ct);
            if (!tools.CanDownload(job.Platform))
            {
                await fileSender.SendTextAsync(bot, job.ChatId,
                    "سرویس دانلود روی سرور در دسترس نیست. لطفاً به پشتیبانی اطلاع دهید.", ct);
                return;
            }

            string? filePath = job.Platform switch
            {
                DetectedMediaPlatform.YouTube => await ytDlp.DownloadAsync(job.SourceUrl, tempDir, ct),
                DetectedMediaPlatform.Pinterest => await DownloadPinterestAsync(job.SourceUrl, tempDir, ct),
                _ => null
            };

            if (filePath is null)
            {
                await fileSender.SendTextAsync(bot, job.ChatId,
                    "دانلود انجام نشد. لطفاً لینک را بررسی کنید یا بعداً دوباره تلاش کنید.", ct);
                return;
            }

            var fileSize = new FileInfo(filePath).Length;
            if (!await userAccess.IsPrivilegedUserAsync(job.ChatId, ct))
            {
                var platformKind = MediaPlatformMapper.ToKind(job.Platform);
                var (allowed, message) = await quotaService.ValidateFileSizeAsync(job.ChatId, platformKind, fileSize, ct);
                if (!allowed)
                {
                    await fileSender.SendTextAsync(bot, job.ChatId, message ?? "فایل برای بسته شما بزرگ است.", ct);
                    return;
                }
            }

            var sent = await fileSender.SendFileAsync(bot, job.ChatId, filePath, ct);
            if (sent && !await userAccess.IsPrivilegedUserAsync(job.ChatId, ct))
            {
                var platform = MediaPlatformMapper.ToKind(job.Platform);
                await quotaService.RecordDownloadAsync(job.ChatId, platform, fileSize, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing media job for {ChatId}", job.ChatId);
            await errorLog.LogExceptionAsync(
                job.ChatId,
                "خطا در پردازش دانلود رسانه",
                ex,
                nameof(MediaDownloadProcessor),
                ct);
            await fileSender.SendTextAsync(bot, job.ChatId,
                "خطایی در هنگام دانلود رخ داد.", ct);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private async Task<string?> DownloadPinterestAsync(string url, string tempDir, CancellationToken ct)
    {
        var path = await galleryDl.DownloadAsync(url, tempDir, ct);
        if (path is not null)
            return path;

        return await ytDlp.DownloadAsync(url, tempDir, ct);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
