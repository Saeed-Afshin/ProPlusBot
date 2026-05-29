using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateMaxFileBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPlanPlatformLimits");

            migrationBuilder.AddColumn<long>(
                name: "MaxFileBytes",
                table: "PlanPricings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE "PlanPricings"
                SET "MaxFileBytes" = CASE
                    WHEN "MaxFileBytesYouTube" > 0 THEN "MaxFileBytesYouTube"
                    ELSE "MaxFileBytesPinterest"
                END;
                """);

            migrationBuilder.DropColumn(
                name: "MaxFileBytesPinterest",
                table: "PlanPricings");

            migrationBuilder.DropColumn(
                name: "MaxFileBytesYouTube",
                table: "PlanPricings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MaxFileBytesYouTube",
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

            migrationBuilder.Sql("""
                UPDATE "PlanPricings"
                SET "MaxFileBytesYouTube" = "MaxFileBytes",
                    "MaxFileBytesPinterest" = "MaxFileBytes";
                """);

            migrationBuilder.DropColumn(
                name: "MaxFileBytes",
                table: "PlanPricings");

            migrationBuilder.CreateTable(
                name: "UserPlanPlatformLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    LimitKind = table.Column<int>(type: "integer", nullable: false),
                    LimitValue = table.Column<long>(type: "bigint", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlanPlatformLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPlanPlatformLimits_BotUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "BotUsers",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlanPlatformLimits_TelegramUserId_Platform_Period_Limit~",
                table: "UserPlanPlatformLimits",
                columns: new[] { "TelegramUserId", "Platform", "Period", "LimitKind" },
                unique: true);
        }
    }
}
