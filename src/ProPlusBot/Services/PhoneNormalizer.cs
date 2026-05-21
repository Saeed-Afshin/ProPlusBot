namespace ProPlusBot.Services;

public static class PhoneNormalizer
{
    public static string Normalize(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith('0') && digits.Length == 11)
            return "+98" + digits[1..];
        if (digits.StartsWith("98") && digits.Length == 12)
            return "+" + digits;
        if (digits.Length == 10)
            return "+98" + digits;
        return phone.Trim().StartsWith('+') ? phone.Trim() : "+" + digits;
    }
}
