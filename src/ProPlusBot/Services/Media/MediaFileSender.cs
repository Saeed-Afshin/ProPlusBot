using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Services;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Media;

public class MediaFileSender(
    ChatStorageService chatStorage,
    ErrorLogService errorLog,
    BaleApiFileSender baleFileSender,
    BotSettingsService botSettings,
    FallbackUploadService fallbackUploads,
    ArvanCloudStorageService arvanStorage,
    IOptions<MediaDownloadOptions> options,
    IOptions<BotOptions> botOptions,
    ILogger<MediaFileSender> logger)
{
    private readonly MediaDownloadOptions _options = options.Value;
    private readonly bool _useBaleApi =
        botOptions.Value.BaleApiBaseUrl.Contains("bale", StringComparison.OrdinalIgnoreCase);

    public Task<int?> SendTextAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken ct) =>
        SendTextAsync(bot, chatId, text, replyMarkup: null, ct);

    public async Task<int?> SendTextAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup,
        CancellationToken ct)
    {
        var sent = await bot.SendMessage(chatId, text, replyMarkup: replyMarkup, cancellationToken: ct);
        await chatStorage.SaveOutgoingAsync(chatId, text, sent.MessageId, ct);
        return sent.MessageId;
    }

    public async Task TryRemoveInlineKeyboardAsync(
        ITelegramBotClient bot,
        long chatId,
        int messageId,
        CancellationToken ct)
    {
        try
        {
            await bot.EditMessageReplyMarkup(chatId, messageId, replyMarkup: null, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not remove inline keyboard from message {MessageId} in chat {ChatId}", messageId, chatId);
        }
    }

    /// <summary>Sends or edits one search-grid photo with caption and inline keypad.</summary>
    public async Task<int?> PublishSearchGridAsync(
        ITelegramBotClient bot,
        long chatId,
        int? existingMessageId,
        byte[] imageBytes,
        string caption,
        InlineKeyboardMarkup replyMarkup,
        CancellationToken ct)
    {
        if (existingMessageId is int messageId
            && await TryUpdateSearchGridAsync(bot, chatId, messageId, imageBytes, caption, replyMarkup, ct))
        {
            await chatStorage.SaveOutgoingAsync(chatId, caption, messageId, ct);
            return messageId;
        }

        return await SendSearchGridPhotoAsync(bot, chatId, imageBytes, caption, replyMarkup, ct);
    }

    /// <summary>Sends the search preview grid (multipart on Bale — stream upload fails there).</summary>
    public async Task<int?> SendSearchGridPhotoAsync(
        ITelegramBotClient bot,
        long chatId,
        byte[] imageBytes,
        string caption,
        InlineKeyboardMarkup? replyMarkup,
        CancellationToken ct)
    {
        try
        {
            Message sent = _useBaleApi
                ? await baleFileSender.SendPhotoAsync(chatId, imageBytes, "search-grid.jpg", caption, replyMarkup, ct)
                : await SendSearchGridViaTelegramBotAsync(bot, chatId, imageBytes, caption, replyMarkup, ct);

            await chatStorage.SaveOutgoingAsync(chatId, caption, sent.MessageId, ct);
            return sent.MessageId;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send search grid photo to {ChatId}", chatId);
            return null;
        }
    }

    private async Task<bool> TryUpdateSearchGridAsync(
        ITelegramBotClient bot,
        long chatId,
        int messageId,
        byte[] imageBytes,
        string caption,
        InlineKeyboardMarkup replyMarkup,
        CancellationToken ct)
    {
        try
        {
            if (_useBaleApi)
            {
                return await baleFileSender.TryEditPhotoMessageAsync(
                    chatId, messageId, imageBytes, "search-grid.jpg", caption, replyMarkup, ct);
            }

            await using var stream = new MemoryStream(imageBytes);
            var media = new InputMediaPhoto(InputFile.FromStream(stream, "search-grid.jpg"))
            {
                Caption = caption,
                ParseMode = ParseMode.None
            };
            await bot.EditMessageMedia(
                chatId,
                messageId,
                media,
                replyMarkup: replyMarkup,
                cancellationToken: ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to edit search grid message {MessageId} in chat {ChatId}",
                messageId,
                chatId);
            return false;
        }
    }

    private static async Task<Message> SendSearchGridViaTelegramBotAsync(
        ITelegramBotClient bot,
        long chatId,
        byte[] imageBytes,
        string caption,
        InlineKeyboardMarkup? replyMarkup,
        CancellationToken ct)
    {
        await using var stream = new MemoryStream(imageBytes);
        return await bot.SendPhoto(
            chatId,
            InputFile.FromStream(stream, "search-grid.jpg"),
            caption: caption,
            replyMarkup: replyMarkup,
            cancellationToken: ct);
    }

    public async Task<MediaDeliveryResult> DeliverFileAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
        string sourceUrl,
        DetectedMediaPlatform platform,
        string? youTubeFormatId,
        string? service,
        CancellationToken ct)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            return new MediaDeliveryResult(false, null, 0, null);

        var settings = await botSettings.GetAsync(ct);
        var contentKey = FallbackUploadContentKey.Compute(sourceUrl, platform, youTubeFormatId);
        var useFallbackForSize = fileInfo.Length >= settings.UploadFallbackMinBytes;
        var exceedsMessengerLimit = fileInfo.Length > _options.MaxUploadBytes;

        if (!useFallbackForSize && !exceedsMessengerLimit)
        {
            var messengerResult = await TrySendViaMessengerAsync(bot, chatId, filePath, fileInfo, service, ct);
            if (messengerResult.Success)
                return messengerResult;
        }

        if (!settings.UploadFallbackEnabled)
        {
            if (useFallbackForSize || exceedsMessengerLimit)
            {
                await SendTextAsync(bot, chatId,
                    "فایل برای ارسال در پیام‌رسان بزرگ است. لطفاً لینک دیگری یا کیفیت پایین‌تر امتحان کنید.",
                    ct);
            }
            else
            {
                await SendTextAsync(bot, chatId, "ارسال فایل با خطا مواجه شد.", ct);
            }

            return new MediaDeliveryResult(false, null, fileInfo.Length, null);
        }

        return await DeliverViaFallbackAsync(
            bot,
            chatId,
            filePath,
            fileInfo,
            sourceUrl,
            platform,
            youTubeFormatId,
            contentKey,
            settings.UploadFallbackExpiryHours,
            service,
            ct);
    }

    public async Task<bool> SendFallbackLinkAsync(
        ITelegramBotClient bot,
        long chatId,
        string publicUrl,
        long fileSizeBytes,
        CancellationToken ct)
    {
        var message =
            $"فایل ({ByteUnits.FormatMegabytes(fileSizeBytes)}) از طریق لینک دانلود آماده است:\n{publicUrl}\n\n" +
            "این لینک پس از مدتی منقضی می‌شود.";
        await SendTextAsync(bot, chatId, message, ct);
        return true;
    }

    private async Task<MediaDeliveryResult> TrySendViaMessengerAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
        FileInfo fileInfo,
        string? service,
        CancellationToken ct)
    {
        try
        {
            Message sent = _useBaleApi
                ? await baleFileSender.SendFileAsync(chatId, filePath, ct)
                : await SendViaTelegramBotAsync(bot, chatId, filePath, fileInfo, ct);

            await chatStorage.SaveOutgoingAsync(chatId, $"[media:{fileInfo.Name}]", sent.MessageId, ct);
            return new MediaDeliveryResult(true, MediaDeliveryMethod.Messenger, fileInfo.Length, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send file {File} to {ChatId}", filePath, chatId);
            await errorLog.LogExceptionAsync(
                chatId,
                "خطا در ارسال فایل به کاربر",
                ex,
                nameof(MediaFileSender),
                service,
                ct);
            return new MediaDeliveryResult(false, null, fileInfo.Length, null);
        }
    }

    private async Task<MediaDeliveryResult> DeliverViaFallbackAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
        FileInfo fileInfo,
        string sourceUrl,
        DetectedMediaPlatform platform,
        string? youTubeFormatId,
        string contentKey,
        int expiryHours,
        string? service,
        CancellationToken ct)
    {
        if (!arvanStorage.IsConfigured)
        {
            logger.LogError("Upload fallback enabled but ArvanCloud storage is not configured");
            await errorLog.LogAsync(
                chatId,
                "آپلود جایگزین فعال است اما ArvanCloud پیکربندی نشده",
                "",
                nameof(MediaFileSender),
                service,
                ct);
            await SendTextAsync(bot, chatId, "ارسال فایل با خطا مواجه شد.", ct);
            return new MediaDeliveryResult(false, null, fileInfo.Length, null);
        }

        var existing = await fallbackUploads.FindActiveByContentKeyAsync(contentKey, ct);
        if (existing is not null)
        {
            await fallbackUploads.RenewExpiryAsync(existing.Id, expiryHours, ct);
            await SendFallbackLinkAsync(bot, chatId, existing.PublicUrl, existing.FileSizeBytes, ct);
            return new MediaDeliveryResult(true, MediaDeliveryMethod.FallbackLink, existing.FileSizeBytes, existing.PublicUrl);
        }

        try
        {
            var ext = fileInfo.Extension.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                ext = ".bin";

            var objectFileName = $"{Guid.NewGuid():N}{ext}";
            var storageKey = arvanStorage.BuildObjectKey(objectFileName);
            var contentType = ArvanCloudStorageService.GuessContentType(ext);
            var publicUrl = await arvanStorage.UploadPublicAsync(filePath, objectFileName, contentType, ct);

            await fallbackUploads.RegisterAsync(
                contentKey,
                sourceUrl,
                platform,
                youTubeFormatId,
                storageKey,
                publicUrl,
                fileInfo.Length,
                contentType,
                expiryHours,
                ct);

            await SendFallbackLinkAsync(bot, chatId, publicUrl, fileInfo.Length, ct);
            return new MediaDeliveryResult(true, MediaDeliveryMethod.FallbackLink, fileInfo.Length, publicUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallback upload failed for {File} to {ChatId}", filePath, chatId);
            await errorLog.LogExceptionAsync(
                chatId,
                "خطا در آپلود فایل به ArvanCloud",
                ex,
                nameof(MediaFileSender),
                service,
                ct);
            await SendTextAsync(bot, chatId, "ارسال فایل با خطا مواجه شد.", ct);
            return new MediaDeliveryResult(false, null, fileInfo.Length, null);
        }
    }

    private static async Task<Message> SendViaTelegramBotAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
        FileInfo fileInfo,
        CancellationToken ct)
    {
        var ext = fileInfo.Extension.ToLowerInvariant();

        if (ext is ".mp4" or ".mkv" or ".webm" or ".mov")
            return await SendWithStreamAsync(bot, chatId, filePath,
                stream => bot.SendVideo(chatId, InputFile.FromStream(stream, fileInfo.Name), cancellationToken: ct));

        if (ext is ".mp3" or ".m4a" or ".ogg" or ".opus" or ".wav")
            return await SendWithStreamAsync(bot, chatId, filePath,
                stream => bot.SendAudio(chatId, InputFile.FromStream(stream, fileInfo.Name), cancellationToken: ct));

        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")
            return await SendWithStreamAsync(bot, chatId, filePath,
                stream => bot.SendPhoto(chatId, InputFile.FromStream(stream, fileInfo.Name), cancellationToken: ct));

        return await SendWithStreamAsync(bot, chatId, filePath,
            stream => bot.SendDocument(chatId, InputFile.FromStream(stream, fileInfo.Name), cancellationToken: ct));
    }

    private static async Task<Message> SendWithStreamAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
        Func<Stream, Task<Message>> sendAsync)
    {
        await using var stream = File.OpenRead(filePath);
        return await sendAsync(stream);
    }
}
