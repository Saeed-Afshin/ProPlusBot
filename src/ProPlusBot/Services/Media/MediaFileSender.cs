using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

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

    public async Task SendTextAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken ct)
    {
        var sent = await bot.SendMessage(chatId, text, cancellationToken: ct);
        await chatStorage.SaveOutgoingAsync(chatId, text, sent.MessageId, ct);
    }

    public async Task<bool> SendFileAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
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
