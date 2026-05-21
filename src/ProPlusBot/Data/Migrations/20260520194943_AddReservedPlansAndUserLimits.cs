using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservedPlansAndUserLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPlanPlatformLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    LimitKind = table.Column<int>(type: "integer", nullable: false),
                    LimitValue = table.Column<long>(type: "bigint", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "UserReservedPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReservedPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReservedPlans_BotUsers_TelegramUserId",
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

            migrationBuilder.CreateIndex(
                name: "IX_UserReservedPlans_TelegramUserId_CreatedAt",
                table: "UserReservedPlans",
                columns: new[] { "TelegramUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPlanPlatformLimits");

            migrationBuilder.DropTable(
                name: "UserReservedPlans");
        }
    }
}
