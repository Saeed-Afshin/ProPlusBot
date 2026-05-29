using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadFallbackPlanPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadFallbackBaleFailurePlansJson",
                table: "BotSettings",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "UploadFallbackSizeExceedPlansJson",
                table: "BotSettings",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            // Bronze=1, Silver=2, Golden=3 — preserve prior global enable flag
            migrationBuilder.Sql("""
                UPDATE "BotSettings"
                SET "UploadFallbackSizeExceedPlansJson" = '[1,2,3]',
                    "UploadFallbackBaleFailurePlansJson" = '[1,2,3]'
                WHERE "UploadFallbackEnabled" = TRUE;
                """);

            migrationBuilder.DropColumn(
                name: "UploadFallbackEnabled",
                table: "BotSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploadFallbackBaleFailurePlansJson",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "UploadFallbackSizeExceedPlansJson",
                table: "BotSettings");

            migrationBuilder.AddColumn<bool>(
                name: "UploadFallbackEnabled",
                table: "BotSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
