using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBotFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PinterestEnabled",
                table: "BotSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "YouTubeEnabled",
                table: "BotSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinterestEnabled",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "YouTubeEnabled",
                table: "BotSettings");
        }
    }
}
