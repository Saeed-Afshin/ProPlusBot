using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYouTubeCookiesToBotSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "YouTubeCookiesContent",
                table: "BotSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "YouTubeCookiesUpdatedAt",
                table: "BotSettings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YouTubeCookiesContent",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "YouTubeCookiesUpdatedAt",
                table: "BotSettings");
        }
    }
}
