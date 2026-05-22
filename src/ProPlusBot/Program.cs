using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProPlusBot.Auth;
using ProPlusBot.Configuration;
using ProPlusBot.Services.Media;
using ProPlusBot.Services.Subscriptions;
using ProPlusBot.Data;
using ProPlusBot.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection(BotOptions.SectionName));
builder.Services.Configure<SuperAdminOptions>(builder.Configuration.GetSection(SuperAdminOptions.SectionName));
builder.Services.Configure<MediaDownloadOptions>(builder.Configuration.GetSection(MediaDownloadOptions.SectionName));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/", AuthConstants.Scheme);
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/LoginVerify");
});

builder.Services.AddAuthentication(AuthConstants.Scheme)
    .AddCookie(AuthConstants.Scheme, options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
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
builder.Services.AddScoped<UserAccessService>();
builder.Services.AddScoped<BotUpdateHandler>();
builder.Services.AddScoped<QuotaService>();
builder.Services.AddScoped<PlanLifecycleService>();
builder.Services.AddScoped<UserPlanLimitService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PlanCatalogService>();
builder.Services.AddScoped<BalePaymentService>();
builder.Services.AddScoped<SubscriptionAdminService>();
builder.Services.AddScoped<TrialSettingsService>();
builder.Services.AddScoped<ErrorLogService>();
builder.Services.AddScoped<ErrorLogAdminService>();
builder.Services.AddScoped<SubscriptionBotHandler>();
builder.Services.AddScoped<MediaBotHandler>();
builder.Services.AddScoped<BaleApiFileSender>();
builder.Services.AddHttpClient(nameof(BaleApiFileSender), client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddScoped<MediaDownloadProcessor>();
builder.Services.AddSingleton<ConversationStateService>();
builder.Services.AddSingleton<MediaDownloadQueue>();
builder.Services.AddSingleton<MediaToolsLocator>();
builder.Services.AddHttpClient(nameof(MediaToolsBootstrapHostedService), client =>
{
    client.Timeout = TimeSpan.FromMinutes(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ProPlusBot/1.0");
});
builder.Services.AddHostedService<MediaToolsBootstrapHostedService>();
builder.Services.AddSingleton<YtDlpService>();
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
builder.Services.AddHostedService<BotHostedService>();

var app = builder.Build();

ConfigurationValidation.ValidateRequiredSettings(app.Configuration, app.Environment);

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "Running as {Environment}. Database: {Database}. Bot: {BotToken}",
    app.Environment.EnvironmentName,
    ConfigurationValidation.DescribeConnection(app.Configuration.GetConnectionString("DefaultConnection")),
    ConfigurationValidation.MaskBotToken(app.Configuration["Bot:Token"]));

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/Index"));

app.Run();
