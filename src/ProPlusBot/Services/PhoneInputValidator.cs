namespace ProPlusBot.Services;

public static class PhoneInputValidator
{
    public static bool LooksLikePhoneNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.StartsWith('/'))
            return false;

        var digits = text.Count(char.IsDigit);
        return digits >= 10 && digits >= text.Trim().Length * 0.6;
    }
}
