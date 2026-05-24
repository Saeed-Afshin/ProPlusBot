using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using ProPlusBot.Auth;
using ProPlusBot.Configuration;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;
using ProPlusBot.Services.Tickets;
using ProPlusBot.Data;
using ProPlusBot.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection(BotOptions.SectionName));
builder.Services.Configure<SuperAdminOptions>(builder.Configuration.GetSection(SuperAdminOptions.SectionName));
builder.Services.Configure<MediaDownloadOptions>(builder.Configuration.GetSection(MediaDownloadOptions.SectionName));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<ConversationStateOptions>(builder.Configuration.GetSection(ConversationStateOptions.SectionName));
builder.Services.Configure<AppDataProtectionOptions>(builder.Configuration.GetSection(AppDataProtectionOptions.SectionName));

var dataProtectionOptions = builder.Configuration
    .GetSection(AppDataProtectionOptions.SectionName)
    .Get<AppDataProtectionOptions>() ?? new AppDataProtectionOptions();

var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName(
        string.IsNullOrWhiteSpace(dataProtectionOptions.ApplicationName)
            ? "ProPlusBot"
            : dataProtectionOptions.ApplicationName);

if (!string.IsNullOrWhiteSpace(dataProtectionOptions.KeysPath))
{
    var keysDir = new DirectoryInfo(dataProtectionOptions.KeysPath);
    keysDir.Create();
    dataProtectionBuilder.PersistKeysToFileSystem(keysDir);
}

builder.Services.AddMemoryCache();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/", AuthConstants.Scheme);
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/LoginVerify");
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret must be at least 32 characters. Set Jwt__Secret in environment or user secrets so admin sessions survive redeploy.");
}

builder.Services.AddSingleton<AdminJwtTokenService>();

builder.Services.AddAuthentication(AuthConstants.Scheme)
    .AddJwtBearer(AuthConstants.Scheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthConstants.JwtCookieName, out var cookieToken)
                    && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                    return Task.CompletedTask;

                context.HandleResponse();
                context.Response.Redirect("/Login");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.Scheme, policy =>
    {
        policy.AddAuthenticationSchemes(AuthConstants.Scheme);
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.AddSingleton<BaleBotClientFactory>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<BotSettingsService>();
builder.Services.AddScoped<BotFeatureService>();
builder.Services.AddScoped<RoleResolverService>();
builder.Services.AddScoped<ChatStorageService>();
builder.Services.AddScoped<ProPlusBot.Services.Messaging.AdminMessagingService>();
builder.Services.AddScoped<ProPlusBot.Services.Messaging.BaleUserProfileSyncService>();
builder.Services.AddScoped<UserAccessService>();
builder.Services.AddScoped<BotUpdateHandler>();
builder.Services.AddScoped<QuotaService>();
builder.Services.AddScoped<PlanLifecycleService>();
builder.Services.AddScoped<UserPlanLimitService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PlanCatalogService>();
builder.Services.AddScoped<BalePaymentService>();
builder.Services.AddScoped<SubscriptionAdminService>();
builder.Services.AddScoped<PlanDefinitionService>();
builder.Services.AddScoped<AdminUserBulkQuotaService>();
builder.Services.AddScoped<TrialSettingsService>();
builder.Services.AddScoped<ErrorLogService>();
builder.Services.AddScoped<ErrorLogAdminService>();
builder.Services.AddScoped<ChatLogAdminService>();
builder.Services.AddScoped<SubscriptionBotHandler>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<TicketAdminService>();
builder.Services.AddScoped<TicketNotificationService>();
builder.Services.AddScoped<TicketBotHandler>();
builder.Services.AddScoped<MediaBotHandler>();
builder.Services.AddScoped<BaleApiFileSender>();
builder.Services.AddHttpClient(nameof(BaleApiFileSender), client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddScoped<MediaDownloadProcessor>();
builder.Services.AddScoped<MediaDownloadJobService>();
builder.Services.AddScoped<UserInteractionLogService>();
builder.Services.AddSingleton<ProPlusBot.Services.Media.State.ConversationStateBackendHolder>();
builder.Services.AddSingleton<ProPlusBot.Services.Media.State.MemoryConversationStateStore>();
builder.Services.AddSingleton<ProPlusBot.Services.Media.State.RedisConversationStateStore>();
builder.Services.AddSingleton<ProPlusBot.Services.Media.State.ConversationStateStoreProvider>();
builder.Services.AddSingleton<ConversationStateService>();
builder.Services.AddHostedService<ConversationStateBackendBootstrap>();
builder.Services.AddSingleton<MediaDownloadQueue>();
builder.Services.AddSingleton<MediaToolsLocator>();
builder.Services.AddHttpClient(nameof(MediaToolsBootstrapHostedService), client =>
{
    client.Timeout = TimeSpan.FromMinutes(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ProPlusBot/1.0");
});
builder.Services.AddHostedService<MediaToolsBootstrapHostedService>();
builder.Services.AddSingleton<YouTubeCookiesProvider>();
builder.Services.AddScoped<YtDlpService>();
builder.Services.AddSingleton<GalleryDlService>();
builder.Services.AddHttpClient(nameof(SearchResultGridComposer), client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
});
builder.Services.AddSingleton<SearchResultGridComposer>();
builder.Services.AddHttpClient<PinterestSearchService>((_, client) =>
{
    var timeoutSeconds = builder.Configuration
        .GetSection(MediaDownloadOptions.SectionName)
        .GetValue(nameof(MediaDownloadOptions.PinterestSearchTimeoutSeconds), 30);

    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
});
builder.Services.AddScoped<MediaFileSender>();
builder.Services.AddHostedService<MediaDownloadBackgroundService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<AdminManagementService>();
builder.Services.AddHostedService<PendingPaymentExpiryHostedService>();
builder.Services.AddHostedService<PlanExpiryHostedService>();
builder.Services.AddHostedService<TicketAutoCloseHostedService>();
builder.Services.AddHostedService<BotHostedService>();

var app = builder.Build();

ConfigurationValidation.ValidateRequiredSettings(app.Configuration, app.Environment);

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "Running as {Environment}. Database: {Database}. Bot: {BotToken}",
    app.Environment.EnvironmentName,
    ConfigurationValidation.DescribeConnection(app.Configuration.GetConnectionString("DefaultConnection")),
    ConfigurationValidation.MaskBotToken(app.Configuration["Bot:Token"]));

if (string.IsNullOrWhiteSpace(dataProtectionOptions.KeysPath))
{
    startupLogger.LogWarning(
        "DataProtection:KeysPath is not set. Antiforgery and TempData cookies will break after container restart. " +
        "Set DataProtection__KeysPath to a persisted directory (e.g. /app/data/dataprotection-keys).");
}
else
{
    startupLogger.LogInformation(
        "Data Protection keys persisted at {KeysPath}",
        dataProtectionOptions.KeysPath);
}

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("ProPlus Bale Bot API");
    });
}

if (app.Configuration.GetValue("EnableHttpsRedirection", app.Environment.IsDevelopment()))
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/Index"));

app.Run();
