using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrialSearchGridAndMonthlyLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SearchGridColumns",
                table: "BotSettings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "SearchGridJpegQuality",
                table: "BotSettings",
                type: "integer",
                nullable: false,
                defaultValue: 85);

            migrationBuilder.AddColumn<int>(
                name: "SearchGridRows",
                table: "BotSettings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "SearchUsageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchUsageLogs_BotUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "BotUsers",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrialSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchUsageLogs_TelegramUserId_Platform_CreatedAt",
                table: "SearchUsageLogs",
                columns: new[] { "TelegramUserId", "Platform", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchUsageLogs");

            migrationBuilder.DropTable(
                name: "TrialSettings");

            migrationBuilder.DropColumn(
                name: "SearchGridColumns",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "SearchGridJpegQuality",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "SearchGridRows",
                table: "BotSettings");
        }
    }
}
