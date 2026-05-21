using Microsoft.EntityFrameworkCore;
using ProPlusBot.Entities;

namespace ProPlusBot.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<BotUser> BotUsers => Set<BotUser>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<BotSetting> BotSettings => Set<BotSetting>();
    public DbSet<OtpSession> OtpSessions => Set<OtpSession>();
    public DbSet<PlanPlatformLimit> PlanPlatformLimits => Set<PlanPlatformLimit>();
    public DbSet<PlanPricing> PlanPricings => Set<PlanPricing>();
    public DbSet<ExtraQuotaPackSettings> ExtraQuotaPackSettings => Set<ExtraQuotaPackSettings>();
    public DbSet<DownloadUsageLog> DownloadUsageLogs => Set<DownloadUsageLog>();
    public DbSet<UserQuotaAdjustment> UserQuotaAdjustments => Set<UserQuotaAdjustment>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<UserReservedPlan> UserReservedPlans => Set<UserReservedPlan>();
    public DbSet<UserPlanPlatformLimit> UserPlanPlatformLimits => Set<UserPlanPlatformLimit>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<TrialSettings> TrialSettings => Set<TrialSettings>();
    public DbSet<SearchUsageLog> SearchUsageLogs => Set<SearchUsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TelegramUserId).IsUnique();
            e.HasIndex(x => x.PhoneNumber).IsUnique();
            e.Property(x => x.PhoneNumber).HasMaxLength(32);
            e.Property(x => x.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<BotUser>(e =>
        {
            e.HasKey(x => x.TelegramUserId);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.PhoneNumber).HasMaxLength(32);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TelegramUserId);
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.MessageType).HasMaxLength(32);
            e.HasOne(x => x.User)
                .WithMany(u => u.Messages)
                .HasForeignKey(x => x.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BotSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.WebhookUrl).HasMaxLength(512);
            e.Property(x => x.SearchGridColumns).HasDefaultValue(3);
            e.Property(x => x.SearchGridRows).HasDefaultValue(3);
            e.Property(x => x.SearchGridJpegQuality).HasDefaultValue(85);
        });

        modelBuilder.Entity<TrialSettings>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<SearchUsageLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TelegramUserId, x.Platform, x.CreatedAt });
            e.HasOne(x => x.User)
                .WithMany(u => u.SearchUsages)
                .HasForeignKey(x => x.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OtpSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PhoneNumber);
            e.Property(x => x.PhoneNumber).HasMaxLength(32);
            e.Property(x => x.Code).HasMaxLength(8);
        });

        modelBuilder.Entity<PlanPlatformLimit>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Plan, x.Platform, x.Period, x.LimitKind }).IsUnique();
        });

        modelBuilder.Entity<PlanPricing>(e =>
        {
            e.HasKey(x => x.Plan);
        });

        modelBuilder.Entity<ExtraQuotaPackSettings>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<DownloadUsageLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TelegramUserId, x.CreatedAt });
            e.HasIndex(x => new { x.TelegramUserId, x.Platform, x.CreatedAt });
            e.HasOne(x => x.User)
                .WithMany(u => u.DownloadUsages)
                .HasForeignKey(x => x.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserQuotaAdjustment>(e =>
        {
            e.HasKey(x => new { x.TelegramUserId, x.Platform });
            e.HasOne(x => x.User)
                .WithMany(u => u.QuotaAdjustments)
                .HasForeignKey(x => x.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserReservedPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TelegramUserId, x.CreatedAt });
            e.HasOne(x => x.User)
                .WithMany(u => u.ReservedPlans)
                .HasForeignKey(x => x.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPlanPlatformLimit>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TelegramUserId, x.Platform, x.Period, x.LimitKind }).IsUnique();
            e.HasOne(x => x.User)
                .WithMany(u => u.PlanPlatformLimits)
                .HasForeignKey(x => x.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ErrorLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.TelegramUserId);
            e.Property(x => x.Title).HasMaxLength(256);
            e.Property(x => x.PhoneNumber).HasMaxLength(32);
            e.Property(x => x.Source).HasMaxLength(128);
        });

        modelBuilder.Entity<PaymentRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TelegramUserId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Payload).HasMaxLength(512);
            e.Property(x => x.ProviderPaymentChargeId).HasMaxLength(128);
            e.Property(x => x.ProviderTelegramPaymentChargeId).HasMaxLength(128);
            e.Property(x => x.Note).HasMaxLength(512);
            e.HasOne(x => x.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(x => x.TelegramUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
