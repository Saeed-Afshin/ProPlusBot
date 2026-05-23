using Npgsql;

namespace ProPlusBot.Configuration;

public static class ConfigurationValidation
{
    public static void ValidateRequiredSettings(IConfiguration configuration, IHostEnvironment environment)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
            missing.Add("ConnectionStrings:DefaultConnection");

        if (string.IsNullOrWhiteSpace(configuration["Bot:Token"]))
            missing.Add("Bot:Token");

        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
            missing.Add("Jwt:Secret (min 32 characters)");

        if (missing.Count == 0)
            return;

        var hint = environment.IsDevelopment()
            ? "Copy appsettings.Development.json.example to appsettings.Development.json and fill in your dev bot token and database."
            : "Set environment variables ConnectionStrings__DefaultConnection and Bot__Token (or use appsettings.Production.local.json on the server).";

        throw new InvalidOperationException(
            $"Required configuration missing for environment '{environment.EnvironmentName}': {string.Join(", ", missing)}. {hint}");
    }

    public static string DescribeConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "(not set)";

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return $"{builder.Host}:{builder.Port}/{builder.Database}";
        }
        catch
        {
            return "(invalid connection string)";
        }
    }

    public static string MaskBotToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "(not set)";

        return token.Length <= 8
            ? "***"
            : $"{token[..4]}…{token[^4..]}";
    }
}
