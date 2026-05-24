using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Media;

/// <summary>
/// Sends local files to Bale via multipart/form-data (Telegram.Bot stream upload is treated as invalid URL on Bale).
/// </summary>
public class BaleApiFileSender(
    IOptions<BotOptions> botOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<BaleApiFileSender> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly BotOptions _botOptions = botOptions.Value;

    public async Task<Message> SendFileAsync(long chatId, string filePath, CancellationToken ct)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = fileInfo.Extension.ToLowerInvariant();

        var (method, fieldName) = ext switch
        {
            ".mp4" or ".mkv" or ".webm" or ".mov" => ("sendVideo", "video"),
            ".mp3" or ".m4a" or ".ogg" or ".opus" or ".wav" => ("sendAudio", "audio"),
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => ("sendPhoto", "photo"),
            _ => ("sendDocument", "document")
        };

        return await SendMultipartAsync(chatId, filePath, fileInfo, method, fieldName, ct);
    }

    public Task<Message> SendPhotoAsync(
        long chatId,
        byte[] imageBytes,
        string fileName,
        string? caption,
        InlineKeyboardMarkup? replyMarkup,
        CancellationToken ct) =>
        SendMultipartBytesAsync(
            chatId,
            imageBytes,
            fileName,
            "image/jpeg",
            "sendPhoto",
            "photo",
            caption,
            replyMarkup,
            ct);

    public async Task<bool> TryEditPhotoMessageAsync(
        long chatId,
        int messageId,
        byte[] imageBytes,
        string fileName,
        string? caption,
        InlineKeyboardMarkup? replyMarkup,
        CancellationToken ct)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId.ToString()), "chat_id");
            content.Add(new StringContent(messageId.ToString()), "message_id");

            var mediaJson = JsonSerializer.Serialize(new
            {
                type = "photo",
                media = "attach://photo",
                caption
            });
            content.Add(new StringContent(mediaJson), "media");

            if (replyMarkup is not null)
                content.Add(new StringContent(ToReplyMarkupJson(replyMarkup)), "reply_markup");

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "photo", fileName);

            await PostBaleApiAsync("editMessageMedia", content, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Bale editMessageMedia failed for chat {ChatId} message {MessageId}",
                chatId,
                messageId);
            return false;
        }
    }

    private async Task<Message> SendMultipartAsync(
        long chatId,
        string filePath,
        FileInfo fileInfo,
        string method,
        string fieldName,
        CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(chatId.ToString()), "chat_id");

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileInfo.Extension));
        content.Add(fileContent, fieldName, fileInfo.Name);

        return await PostBaleApiAsync(method, content, ct);
    }

    private async Task<Message> SendMultipartBytesAsync(
        long chatId,
        byte[] bytes,
        string fileName,
        string mimeType,
        string method,
        string fieldName,
        string? caption,
        InlineKeyboardMarkup? replyMarkup,
        CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(chatId.ToString()), "chat_id");

        if (!string.IsNullOrEmpty(caption))
            content.Add(new StringContent(caption), "caption");

        if (replyMarkup is not null)
            content.Add(new StringContent(ToReplyMarkupJson(replyMarkup)), "reply_markup");

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        content.Add(fileContent, fieldName, fileName);

        return await PostBaleApiAsync(method, content, ct);
    }

    private async Task<Message> PostBaleApiAsync(
        string method,
        HttpContent content,
        CancellationToken ct)
    {
        var url = $"{_botOptions.BaleApiBaseUrl.TrimEnd('/')}/bot{_botOptions.Token}/{method}";
        var client = httpClientFactory.CreateClient(nameof(BaleApiFileSender));

        using var response = await client.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Bale {Method} HTTP {Status}: {Body}", method, response.StatusCode, body);
            throw new InvalidOperationException($"Bale API {method} failed: {body}");
        }

        var apiResponse = JsonSerializer.Deserialize<BaleApiResponse<Message>>(body, JsonOptions);
        if (apiResponse?.Ok != true || apiResponse.Result is null)
        {
            logger.LogError("Bale {Method} returned error: {Body}", method, body);
            throw new InvalidOperationException($"Bale API {method} returned ok=false");
        }

        return apiResponse.Result;
    }

    private static string ToReplyMarkupJson(InlineKeyboardMarkup markup) =>
        JsonSerializer.Serialize(new
        {
            inline_keyboard = markup.InlineKeyboard
                .Select(row => row.Select(btn => new
                {
                    text = btn.Text,
                    callback_data = btn.CallbackData
                }))
        });

    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mkv" => "video/x-matroska",
        ".mov" => "video/quicktime",
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        ".opus" => "audio/opus",
        ".wav" => "audio/wav",
        _ => "application/octet-stream"
    };

    private sealed class BaleApiResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }
    }
}
