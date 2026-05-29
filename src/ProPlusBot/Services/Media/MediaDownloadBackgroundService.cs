using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Entities;
using ProPlusBot.Services;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;

namespace ProPlusBot.Services.Media;

public class MediaDownloadBackgroundService(
    MediaDownloadQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MediaDownloadBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingJobsAsync(stoppingToken);

        await foreach (var jobId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<MediaDownloadJobService>();
                var processor = scope.ServiceProvider.GetRequiredService<MediaDownloadProcessor>();

                var workItem = await jobService.TryMarkProcessingAsync(jobId, stoppingToken);
                if (workItem is null)
                    continue;

                await processor.ProcessAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Media download job {JobId} failed", jobId);
                await HandleJobFailureAsync(jobId, ex, stoppingToken);
            }
        }
    }

    private async Task RecoverPendingJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var jobService = scope.ServiceProvider.GetRequiredService<MediaDownloadJobService>();
            var pendingIds = await jobService.RecoverPendingJobIdsAsync(ct);
            foreach (var id in pendingIds)
                await queue.SignalAsync(id, ct);

            if (pendingIds.Count > 0)
            {
                logger.LogInformation(
                    "Recovered {Count} pending media download job(s) after startup",
                    pendingIds.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover pending media download jobs");
        }
    }

    private async Task HandleJobFailureAsync(Guid jobId, Exception ex, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var jobService = scope.ServiceProvider.GetRequiredService<MediaDownloadJobService>();
            var interactionLog = scope.ServiceProvider.GetRequiredService<UserInteractionLogService>();
            var errorLog = scope.ServiceProvider.GetRequiredService<ErrorLogService>();

            var detail = await jobService.GetDetailAsync(jobId, ct);
            await jobService.MarkFailedAsync(
                jobId,
                "خطای داخلی هنگام پردازش دانلود",
                ex.ToString(),
                ct);
            await interactionLog.UpdateByJobIdAsync(
                jobId,
                UserInteractionStatus.Failed,
                "خطای داخلی هنگام پردازش دانلود",
                ct);

            await errorLog.LogExceptionAsync(
                detail?.TelegramUserId,
                "خطا در صف دانلود رسانه",
                ex,
                nameof(MediaDownloadBackgroundService),
                detail is null ? null : ErrorLogServices.FromDetectedMediaPlatform(detail.Platform),
                ct);
        }
        catch (Exception inner)
        {
            logger.LogWarning(inner, "Failed to persist failure for job {JobId}", jobId);
        }
    }
}

public class MediaDownloadProcessor(
    BaleBotClientFactory clientFactory,
    MediaToolsLocator tools,
    YtDlpService ytDlp,
    GalleryDlService galleryDl,
    MediaFileSender fileSender,
    AdminSettingsCache adminSettingsCache,
    FallbackUploadService fallbackUploads,
    QuotaService quotaService,
    UserAccessService userAccess,
    MediaDownloadJobService jobService,
    UserInteractionLogService interactionLog,
    ErrorLogService errorLog,
    IOptions<DownloadOptions> mediaOptions,
    IHostEnvironment hostEnvironment,
    ILogger<MediaDownloadProcessor> logger)
{
    public async Task ProcessAsync(MediaDownloadWorkItem job, CancellationToken ct)
    {
        var bot = clientFactory.CreateClient();
        var mediaRoot = ToolExecutableResolver.ResolveMediaDirectory(
            hostEnvironment,
            mediaOptions.Value.MediaDirectory);
        Directory.CreateDirectory(mediaRoot);
        var tempDir = Path.Combine(mediaRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var mediaJob = new MediaDownloadJob(
            job.ChatId,
            job.SourceUrl,
            job.Platform,
            job.Source,
            job.YouTubeFormatId);

        try
        {
            if (await TryDeliverCachedFallbackAsync(bot, job, ct))
                return;

            await tools.WaitReadyAsync(ct);
            if (!tools.CanDownload(job.Platform))
            {
                const string summary = "ابزار دانلود روی سرور در دسترس نیست";
                await FailJobAsync(job.Id, summary, $"URL: {job.SourceUrl}{Environment.NewLine}Platform: {job.Platform}", ct);
                await errorLog.LogAsync(
                    job.ChatId,
                    summary,
                    $"URL: {job.SourceUrl}{Environment.NewLine}Platform: {job.Platform}",
                    nameof(MediaDownloadProcessor),
                    ErrorLogServices.FromDetectedMediaPlatform(job.Platform),
                    ct);
                await fileSender.SendTextAsync(bot, job.ChatId,
                    "سرویس دانلود روی سرور در دسترس نیست. لطفاً به پشتیبانی اطلاع دهید.", ct);
                return;
            }

            var download = job.Platform switch
            {
                DetectedMediaPlatform.YouTube => await ytDlp.DownloadAsync(
                    job.SourceUrl, tempDir, job.YouTubeFormatId, ct),
                DetectedMediaPlatform.Pinterest => await DownloadPinterestAsync(job.SourceUrl, tempDir, ct),
                _ => MediaToolDownloadResult.Failed($"Unsupported platform: {job.Platform}")
            };

            if (!download.Success)
            {
                var userMessage = download.ErrorDetail?.Contains(
                    "Requested format is not available",
                    StringComparison.OrdinalIgnoreCase) == true
                    ? "کیفیت انتخاب‌شده دیگر در دسترس نیست. لینک را دوباره بفرستید و کیفیت دیگری انتخاب کنید."
                    : "دانلود انجام نشد. لطفاً لینک را بررسی کنید یا بعداً دوباره تلاش کنید.";
                var detail = $"URL: {job.SourceUrl}{Environment.NewLine}{Environment.NewLine}{download.ErrorDetail}";
                await FailJobAsync(job.Id, userMessage, detail, ct);
                await errorLog.LogAsync(
                    job.ChatId,
                    "دانلود رسانه انجام نشد",
                    detail,
                    nameof(MediaDownloadProcessor),
                    ErrorLogServices.FromDetectedMediaPlatform(job.Platform),
                    ct);
                await fileSender.SendTextAsync(bot, job.ChatId, userMessage, ct);
                return;
            }

            var filePath = download.FilePath!;
            var fileSize = new FileInfo(filePath).Length;
            if (!await userAccess.IsPrivilegedUserAsync(job.ChatId, ct))
            {
                var platformKind = MediaPlatformMapper.ToKind(job.Platform);
                var (allowed, message) = await quotaService.ValidateFileSizeAsync(
                    job.ChatId, platformKind, fileSize, ct);
                if (!allowed)
                {
                    var summary = message ?? "فایل برای بسته شما بزرگ است.";
                    await FailJobAsync(job.Id, summary, $"File size: {fileSize}", ct);
                    await fileSender.SendTextAsync(bot, job.ChatId, summary, ct);
                    return;
                }
            }

            var delivery = await fileSender.DeliverFileAsync(
                bot,
                job.ChatId,
                filePath,
                job.SourceUrl,
                job.Platform,
                job.YouTubeFormatId,
                ErrorLogServices.FromDetectedMediaPlatform(job.Platform),
                ct);
            if (!delivery.Success)
            {
                await FailJobAsync(job.Id, "ارسال فایل به کاربر ناموفق بود", null, ct);
                return;
            }

            if (!await userAccess.IsPrivilegedUserAsync(job.ChatId, ct))
            {
                var platform = MediaPlatformMapper.ToKind(job.Platform);
                await quotaService.RecordDownloadAsync(job.ChatId, platform, delivery.FileSizeBytes, ct);
            }

            var successSummary = delivery.Method == MediaDeliveryMethod.FallbackLink
                ? $"لینک دانلود ارسال شد ({ByteUnits.FormatMegabytes(delivery.FileSizeBytes)})"
                : $"فایل ارسال شد ({ByteUnits.FormatMegabytes(delivery.FileSizeBytes)})";
            await jobService.MarkCompletedAsync(job.Id, delivery.FileSizeBytes, successSummary, ct);
            await interactionLog.UpdateByJobIdAsync(
                job.Id,
                UserInteractionStatus.Success,
                successSummary,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing media job {JobId} for {ChatId}", job.Id, job.ChatId);
            await FailJobAsync(job.Id, "خطایی در هنگام دانلود رخ داد.", ex.ToString(), ct);
            await errorLog.LogExceptionAsync(
                job.ChatId,
                "خطا در پردازش دانلود رسانه",
                ex,
                nameof(MediaDownloadProcessor),
                ErrorLogServices.FromDetectedMediaPlatform(job.Platform),
                ct);
            await fileSender.SendTextAsync(bot, job.ChatId, "خطایی در هنگام دانلود رخ داد.", ct);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private async Task<bool> TryDeliverCachedFallbackAsync(
        ITelegramBotClient bot,
        MediaDownloadWorkItem job,
        CancellationToken ct)
    {
        var config = (await adminSettingsCache.GetAsync(ct)).ToUploadFallbackConfig();
        var plan = await quotaService.GetEffectivePlanAsync(job.ChatId, ct);
        var planPolicy = config.GetPlan(plan);
        if (!planPolicy.AllowsAny())
            return false;

        var contentKey = FallbackUploadContentKey.Compute(job.SourceUrl, job.Platform, job.YouTubeFormatId);
        var cached = await fallbackUploads.FindActiveByContentKeyAsync(contentKey, ct);
        if (cached is null)
            return false;

        await fallbackUploads.RenewExpiryAsync(cached.Id, planPolicy.ExpiryHours, ct);
        var expiresAt = FallbackLinkMessages.ComputeExpiresAtUtc(planPolicy.ExpiryHours);
        await fileSender.SendFallbackLinkAsync(bot, job.ChatId, cached.PublicUrl, cached.FileSizeBytes, expiresAt, ct);

        if (!await userAccess.IsPrivilegedUserAsync(job.ChatId, ct))
        {
            var platform = MediaPlatformMapper.ToKind(job.Platform);
            await quotaService.RecordDownloadAsync(job.ChatId, platform, cached.FileSizeBytes, ct);
        }

        var summary = $"لینک دانلود ارسال شد ({ByteUnits.FormatMegabytes(cached.FileSizeBytes)})";
        await jobService.MarkCompletedAsync(job.Id, cached.FileSizeBytes, summary, ct);
        await interactionLog.UpdateByJobIdAsync(job.Id, UserInteractionStatus.Success, summary, ct);
        return true;
    }

    private async Task FailJobAsync(Guid jobId, string summary, string? errorDetail, CancellationToken ct)
    {
        await jobService.MarkFailedAsync(jobId, summary, errorDetail, ct);
        await interactionLog.UpdateByJobIdAsync(jobId, UserInteractionStatus.Failed, summary, ct);
    }

    private async Task<MediaToolDownloadResult> DownloadPinterestAsync(string url, string tempDir, CancellationToken ct)
    {
        var gallery = await galleryDl.DownloadAsync(url, tempDir, ct);
        if (gallery.Success)
            return gallery;

        var ytDlpResult = await ytDlp.DownloadAsync(url, tempDir, youtubeFormatId: null, ct);
        if (ytDlpResult.Success)
            return ytDlpResult;

        var parts = new[] { gallery.ErrorDetail, ytDlpResult.ErrorDetail }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        var combined = parts.Length == 0
            ? "Pinterest download failed with no tool output."
            : string.Join(Environment.NewLine + "---" + Environment.NewLine, parts);
        return MediaToolDownloadResult.Failed(combined);
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
