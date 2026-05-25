namespace ProPlusBot.Services;

public static class BotUserDisplay
{
    public static string? ComposeDisplayName(string? firstName, string? lastName) =>
        string.IsNullOrWhiteSpace(firstName)
            ? null
            : (firstName + (lastName != null ? " " + lastName : "")).Trim();
}
