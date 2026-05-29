using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveUploadFallbackToPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FallbackLinkExpiryHours",
                table: "PlanPricings",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<bool>(
                name: "FallbackOnBaleFailure",
                table: "PlanPricings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FallbackOnSizeExceed",
                table: "PlanPricings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Plan enum: Free=0, Bronze=1, Silver=2, Golden=3
            migrationBuilder.Sql("""
                UPDATE "PlanPricings" p SET "FallbackOnSizeExceed" = TRUE
                FROM "BotSettings" b
                WHERE b."Id" = 1 AND p."Plan" = 2
                  AND b."UploadFallbackSizeExceedPlansJson"::jsonb @> '2';

                UPDATE "PlanPricings" p SET "FallbackOnSizeExceed" = TRUE
                FROM "BotSettings" b
                WHERE b."Id" = 1 AND p."Plan" = 3
                  AND b."UploadFallbackSizeExceedPlansJson"::jsonb @> '3';

                UPDATE "PlanPricings" p SET "FallbackOnBaleFailure" = TRUE
                FROM "BotSettings" b
                WHERE b."Id" = 1 AND p."Plan" = 1
                  AND b."UploadFallbackBaleFailurePlansJson"::jsonb @> '1';

                UPDATE "PlanPricings" p SET "FallbackOnBaleFailure" = TRUE
                FROM "BotSettings" b
                WHERE b."Id" = 1 AND p."Plan" = 2
                  AND b."UploadFallbackBaleFailurePlansJson"::jsonb @> '2';

                UPDATE "PlanPricings" p SET "FallbackOnBaleFailure" = TRUE
                FROM "BotSettings" b
                WHERE b."Id" = 1 AND p."Plan" = 3
                  AND b."UploadFallbackBaleFailurePlansJson"::jsonb @> '3';
                """);

            migrationBuilder.DropColumn(
                name: "UploadFallbackBaleFailurePlansJson",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "UploadFallbackExpiryHours",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "UploadFallbackSizeExceedPlansJson",
                table: "BotSettings");

            migrationBuilder.RenameColumn(
                name: "UploadFallbackMinBytes",
                table: "BotSettings",
                newName: "BaleDirectArvanThresholdBytes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FallbackLinkExpiryHours",
                table: "PlanPricings");

            migrationBuilder.DropColumn(
                name: "FallbackOnBaleFailure",
                table: "PlanPricings");

            migrationBuilder.DropColumn(
                name: "FallbackOnSizeExceed",
                table: "PlanPricings");

            migrationBuilder.RenameColumn(
                name: "BaleDirectArvanThresholdBytes",
                table: "BotSettings",
                newName: "UploadFallbackMinBytes");

            migrationBuilder.AddColumn<string>(
                name: "UploadFallbackBaleFailurePlansJson",
                table: "BotSettings",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "UploadFallbackExpiryHours",
                table: "BotSettings",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<string>(
                name: "UploadFallbackSizeExceedPlansJson",
                table: "BotSettings",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }
    }
}
