using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBanned",
                table: "BotUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Plan",
                table: "BotUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanExpiresAt",
                table: "BotUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DownloadUsageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadUsageLogs_BotUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "BotUsers",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtraQuotaPackSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PriceIrr = table.Column<long>(type: "bigint", nullable: false),
                    ExtraDownloadCount = table.Column<int>(type: "integer", nullable: false),
                    ExtraDownloadBytes = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraQuotaPackSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AmountIrr = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Payload = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FromPlan = table.Column<int>(type: "integer", nullable: true),
                    ToPlan = table.Column<int>(type: "integer", nullable: true),
                    Platform = table.Column<int>(type: "integer", nullable: true),
                    ProviderPaymentChargeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderTelegramPaymentChargeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_BotUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "BotUsers",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanPlatformLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    LimitKind = table.Column<int>(type: "integer", nullable: false),
                    LimitValue = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanPlatformLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanPricings",
                columns: table => new
                {
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    MonthlyPriceIrr = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanPricings", x => x.Plan);
                });

            migrationBuilder.CreateTable(
                name: "UserQuotaAdjustments",
                columns: table => new
                {
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    ExtraDownloadCount = table.Column<int>(type: "integer", nullable: false),
                    ExtraDownloadBytes = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQuotaAdjustments", x => new { x.TelegramUserId, x.Platform });
                    table.ForeignKey(
                        name: "FK_UserQuotaAdjustments_BotUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "BotUsers",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadUsageLogs_TelegramUserId_CreatedAt",
                table: "DownloadUsageLogs",
                columns: new[] { "TelegramUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadUsageLogs_TelegramUserId_Platform_CreatedAt",
                table: "DownloadUsageLogs",
                columns: new[] { "TelegramUserId", "Platform", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_CreatedAt",
                table: "PaymentRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_Status",
                table: "PaymentRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_TelegramUserId",
                table: "PaymentRecords",
                column: "TelegramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanPlatformLimits_Plan_Platform_Period_LimitKind",
                table: "PlanPlatformLimits",
                columns: new[] { "Plan", "Platform", "Period", "LimitKind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadUsageLogs");

            migrationBuilder.DropTable(
                name: "ExtraQuotaPackSettings");

            migrationBuilder.DropTable(
                name: "PaymentRecords");

            migrationBuilder.DropTable(
                name: "PlanPlatformLimits");

            migrationBuilder.DropTable(
                name: "PlanPricings");

            migrationBuilder.DropTable(
                name: "UserQuotaAdjustments");

            migrationBuilder.DropColumn(
                name: "IsBanned",
                table: "BotUsers");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "BotUsers");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "BotUsers");
        }
    }
}
