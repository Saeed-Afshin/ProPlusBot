using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidatePlanQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExtraDownloadBytesPack",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ExtraDownloadBytesPriceToman",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ExtraDownloadCountPack",
                table: "PlanPricings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ExtraDownloadCountPriceToman",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MaxFileBytesPinterest",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MaxFileBytesYouTube",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MonthlyDownloadBytes",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MonthlyDownloadCount",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MonthlySearchCount",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyTicketLimit",
                table: "PlanPricings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Merge per-platform monthly limits into PlanPricings (LimitKind: DownloadCount=0, DownloadBytes=1, SearchCount=3; Period Monthly=1; Platform YT=0, Pinterest=1; MaxFileBytes=2)
            migrationBuilder.Sql("""
                UPDATE "PlanPricings" p SET
                  "MonthlyDownloadCount" = COALESCE((
                    SELECT SUM("LimitValue") FROM "PlanPlatformLimits"
                    WHERE "Plan" = p."Plan" AND "Period" = 1 AND "LimitKind" = 0), 0),
                  "MonthlyDownloadBytes" = COALESCE((
                    SELECT SUM("LimitValue") FROM "PlanPlatformLimits"
                    WHERE "Plan" = p."Plan" AND "Period" = 1 AND "LimitKind" = 1), 0),
                  "MonthlySearchCount" = COALESCE((
                    SELECT SUM("LimitValue") FROM "PlanPlatformLimits"
                    WHERE "Plan" = p."Plan" AND "Period" = 1 AND "LimitKind" = 3), 0),
                  "MaxFileBytesYouTube" = COALESCE((
                    SELECT "LimitValue" FROM "PlanPlatformLimits"
                    WHERE "Plan" = p."Plan" AND "Platform" = 0 AND "LimitKind" = 2 LIMIT 1), 0),
                  "MaxFileBytesPinterest" = COALESCE((
                    SELECT "LimitValue" FROM "PlanPlatformLimits"
                    WHERE "Plan" = p."Plan" AND "Platform" = 1 AND "LimitKind" = 2 LIMIT 1), 0);

                UPDATE "PlanPricings" p SET
                  "MonthlyTicketLimit" = COALESCE((
                    SELECT "MonthlyTicketLimit" FROM "PlanTicketLimits" t WHERE t."Plan" = p."Plan"), 0)
                WHERE EXISTS (SELECT 1 FROM "PlanTicketLimits");

                UPDATE "PlanPricings" SET
                  "ExtraDownloadCountPriceToman" = (SELECT "PriceToman" FROM "ExtraQuotaPackSettings" WHERE "Id" = 1),
                  "ExtraDownloadCountPack" = (SELECT "ExtraDownloadCount" FROM "ExtraQuotaPackSettings" WHERE "Id" = 1),
                  "ExtraDownloadBytesPriceToman" = (SELECT "PriceToman" FROM "ExtraQuotaPackSettings" WHERE "Id" = 1),
                  "ExtraDownloadBytesPack" = (SELECT "ExtraDownloadBytes" FROM "ExtraQuotaPackSettings" WHERE "Id" = 1)
                WHERE EXISTS (SELECT 1 FROM "ExtraQuotaPackSettings" WHERE "Id" = 1);
                """);

            migrationBuilder.Sql("""
                CREATE TABLE "UserQuotaAdjustments_merged" (
                  "TelegramUserId" bigint NOT NULL,
                  "ExtraDownloadCount" integer NOT NULL,
                  "ExtraDownloadBytes" bigint NOT NULL,
                  "UpdatedAt" timestamp with time zone NOT NULL,
                  PRIMARY KEY ("TelegramUserId")
                );
                INSERT INTO "UserQuotaAdjustments_merged" ("TelegramUserId", "ExtraDownloadCount", "ExtraDownloadBytes", "UpdatedAt")
                SELECT "TelegramUserId", SUM("ExtraDownloadCount"), SUM("ExtraDownloadBytes"), MAX("UpdatedAt")
                FROM "UserQuotaAdjustments"
                GROUP BY "TelegramUserId";
                """);

            migrationBuilder.DropTable(name: "UserQuotaAdjustments");

            migrationBuilder.Sql("""
                ALTER TABLE "UserQuotaAdjustments_merged" RENAME TO "UserQuotaAdjustments";
                """);

            migrationBuilder.Sql("""
                DELETE FROM "UserPlanPlatformLimits" WHERE "LimitKind" <> 2;
                """);

            migrationBuilder.DropTable(name: "ExtraQuotaPackSettings");
            migrationBuilder.DropTable(name: "PlanPlatformLimits");
            migrationBuilder.DropTable(name: "PlanTicketLimits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtraQuotaPackSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExtraDownloadBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExtraDownloadCount = table.Column<int>(type: "integer", nullable: false),
                    PriceToman = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ExtraQuotaPackSettings", x => x.Id));

            migrationBuilder.CreateTable(
                name: "PlanPlatformLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LimitKind = table.Column<int>(type: "integer", nullable: false),
                    LimitValue = table.Column<long>(type: "bigint", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_PlanPlatformLimits", x => x.Id));

            migrationBuilder.CreateTable(
                name: "PlanTicketLimits",
                columns: table => new
                {
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    MonthlyTicketLimit = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_PlanTicketLimits", x => x.Plan));

            migrationBuilder.CreateIndex(
                name: "IX_PlanPlatformLimits_Plan_Platform_Period_LimitKind",
                table: "PlanPlatformLimits",
                columns: new[] { "Plan", "Platform", "Period", "LimitKind" },
                unique: true);

            migrationBuilder.DropColumn(name: "ExtraDownloadBytesPack", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "ExtraDownloadBytesPriceToman", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "ExtraDownloadCountPack", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "ExtraDownloadCountPriceToman", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "MaxFileBytesPinterest", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "MaxFileBytesYouTube", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "MonthlyDownloadBytes", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "MonthlyDownloadCount", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "MonthlySearchCount", table: "PlanPricings");
            migrationBuilder.DropColumn(name: "MonthlyTicketLimit", table: "PlanPricings");
        }
    }
}
