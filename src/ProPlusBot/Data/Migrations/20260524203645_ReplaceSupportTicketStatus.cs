using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSupportTicketStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy: Open = 0, Closed = 1 → Created/WaitingForAdmin/WaitingForUser/Closed (0–3)
            migrationBuilder.Sql("""
                UPDATE "SupportTickets" SET "Status" = 3 WHERE "Status" = 1;

                UPDATE "SupportTickets" t
                SET "Status" = 2
                WHERE t."Status" = 0
                  AND (
                    SELECT m."Sender"
                    FROM "SupportTicketMessages" m
                    WHERE m."TicketId" = t."Id"
                    ORDER BY m."CreatedAt" DESC
                    LIMIT 1
                  ) = 1;

                UPDATE "SupportTickets" t
                SET "Status" = 1
                WHERE t."Status" = 0
                  AND EXISTS (
                    SELECT 1 FROM "SupportTicketMessages" m
                    WHERE m."TicketId" = t."Id" AND m."Sender" = 1
                  )
                  AND (
                    SELECT m."Sender"
                    FROM "SupportTicketMessages" m
                    WHERE m."TicketId" = t."Id"
                    ORDER BY m."CreatedAt" DESC
                    LIMIT 1
                  ) = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SupportTickets" SET "Status" = 1 WHERE "Status" = 3;
                UPDATE "SupportTickets" SET "Status" = 0 WHERE "Status" IN (1, 2);
                """);
        }
    }
}
