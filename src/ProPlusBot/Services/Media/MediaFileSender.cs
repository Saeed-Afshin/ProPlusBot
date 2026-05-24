using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Media;

public class MediaFileSender(
    ChatStorageService chatStorage,
    ErrorLogService errorLog,
    BaleApiFileSender baleFileSender,
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

    public async Task<bool> SendFileAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
        string? service,
        CancellationToken ct)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            return false;

        if (fileInfo.Length > _options.MaxUploadBytes)
        {
            await SendTextAsync(bot, chatId,
                "فایل برای ارسال در پیام‌رسان بزرگ است. لطفاً لینک دیگری یا کیفیت پایین‌تر امتحان کنید.",
                ct);
            return false;
        }

        try
        {
            Message sent = _useBaleApi
                ? await baleFileSender.SendFileAsync(chatId, filePath, ct)
                : await SendViaTelegramBotAsync(bot, chatId, filePath, fileInfo, ct);

            await chatStorage.SaveOutgoingAsync(chatId, $"[media:{fileInfo.Name}]", sent.MessageId, ct);
            return true;
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
            await SendTextAsync(bot, chatId, "ارسال فایل با خطا مواجه شد.", ct);
            return false;
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
