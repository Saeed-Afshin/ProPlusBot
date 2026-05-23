using System.Text;

namespace ProPlusBot.Services.Media;

public static class YouTubeCookiesContentParser
{
    public static bool TryNormalize(string input, out string netscapeContent, out string? error)
    {
        netscapeContent = string.Empty;
        error = null;

        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            error = "محتوای کوکی خالی است.";
            return false;
        }

        if (LooksLikeNetscape(trimmed))
        {
            netscapeContent = trimmed;
            return ValidateYouTube(netscapeContent, out error);
        }

        var compact = trimmed.Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal);
        if (TryDecodeBase64(compact, out var decoded))
        {
            netscapeContent = decoded;
            return ValidateYouTube(netscapeContent, out error);
        }

        error = "فرمت نامعتبر است. فایل Netscape cookies.txt یا رشته Base64 یک‌خطی بچسبانید.";
        return false;
    }

    private static bool LooksLikeNetscape(string value) =>
        value.Contains('\t', StringComparison.Ordinal)
        || value.Contains(".youtube.com", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("# Netscape", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("# HTTP Cookie File", StringComparison.OrdinalIgnoreCase);

    private static bool TryDecodeBase64(string compact, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            var bytes = Convert.FromBase64String(compact);
            decoded = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool ValidateYouTube(string content, out string? error)
    {
        error = null;
        if (content.Contains(".youtube.com", StringComparison.OrdinalIgnoreCase)
            || content.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        error = "هیچ کوکی مربوط به YouTube در محتوا یافت نشد.";
        return false;
    }

    /// <summary>Logged-in session markers required for most YouTube bot checks.</summary>
    public static bool HasLoggedInSessionCookies(string content) =>
        content.Contains("LOGIN_INFO", StringComparison.OrdinalIgnoreCase)
        || content.Contains("\tSID\t", StringComparison.Ordinal)
        || content.Contains("\tSSID\t", StringComparison.Ordinal)
        || content.Contains("\tSAPISID\t", StringComparison.Ordinal)
        || content.Contains("\t__Secure-1PSID\t", StringComparison.Ordinal);

    public static string? GetWeakSessionWarning(string netscapeContent) =>
        HasLoggedInSessionCookies(netscapeContent)
            ? null
            : "کوکی ذخیره شد، اما کوکی‌های ورود Google (SID، LOGIN_INFO و …) دیده نشد — دانلود یوتیوب احتمالاً ناموفق می‌ماند. "
              + "در مرورگر عادی (نه ناشناسِ بدون لاگین) به accounts.google.com و youtube.com وارد شوید، یک ویدیو پخش کنید، دوباره export کنید و ذخیره کنید.";
}
