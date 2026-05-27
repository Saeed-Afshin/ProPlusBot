using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadFallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UploadFallbackEnabled",
                table: "BotSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UploadFallbackExpiryHours",
                table: "BotSettings",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.CreateTable(
                name: "FallbackUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    YouTubeFormatId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StorageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackUploads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FallbackUploads_ContentKey",
                table: "FallbackUploads",
                column: "ContentKey");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackUploads_ExpiresAt",
                table: "FallbackUploads",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FallbackUploads");

            migrationBuilder.DropColumn(
                name: "UploadFallbackEnabled",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "UploadFallbackExpiryHours",
                table: "BotSettings");
        }
    }
}
