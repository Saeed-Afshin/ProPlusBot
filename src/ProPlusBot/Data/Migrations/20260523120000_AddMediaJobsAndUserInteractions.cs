using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using ProPlusBot.Data;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaJobsAndUserInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaDownloadJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    YouTubeFormatId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResultSummary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorDetail = table.Column<string>(type: "text", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    IncomingChatMessageId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaDownloadJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaDownloadJobs_BotUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "BotUsers",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaDownloadJobs_ChatMessages_IncomingChatMessageId",
                        column: x => x.IncomingChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserInteractionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    IncomingChatMessageId = table.Column<long>(type: "bigint", nullable: true),
                    MediaDownloadJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InputSummary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ResultSummary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInteractionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInteractionLogs_BotUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "BotUsers",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserInteractionLogs_ChatMessages_IncomingChatMessageId",
                        column: x => x.IncomingChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserInteractionLogs_MediaDownloadJobs_MediaDownloadJobId",
                        column: x => x.MediaDownloadJobId,
                        principalTable: "MediaDownloadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaDownloadJobs_CreatedAt",
                table: "MediaDownloadJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaDownloadJobs_IncomingChatMessageId",
                table: "MediaDownloadJobs",
                column: "IncomingChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaDownloadJobs_Status",
                table: "MediaDownloadJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MediaDownloadJobs_TelegramUserId",
                table: "MediaDownloadJobs",
                column: "TelegramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInteractionLogs_CreatedAt",
                table: "UserInteractionLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserInteractionLogs_IncomingChatMessageId",
                table: "UserInteractionLogs",
                column: "IncomingChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInteractionLogs_MediaDownloadJobId",
                table: "UserInteractionLogs",
                column: "MediaDownloadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInteractionLogs_TelegramUserId",
                table: "UserInteractionLogs",
                column: "TelegramUserId");

            migrationBuilder.AddColumn<int>(
                name: "ConversationStateBackend",
                table: "BotSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversationStateBackend",
                table: "BotSettings");

            migrationBuilder.DropTable(
                name: "UserInteractionLogs");

            migrationBuilder.DropTable(
                name: "MediaDownloadJobs");
        }
    }
}
