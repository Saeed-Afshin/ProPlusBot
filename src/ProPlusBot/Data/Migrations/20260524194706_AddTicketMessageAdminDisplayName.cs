using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketMessageAdminDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminDisplayName",
                table: "SupportTicketMessages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminDisplayName",
                table: "SupportTicketMessages");
        }
    }
}
