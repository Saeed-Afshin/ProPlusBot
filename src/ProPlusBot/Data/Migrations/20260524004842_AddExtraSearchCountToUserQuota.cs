using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraSearchCountToUserQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExtraSearchCount",
                table: "UserQuotaAdjustments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraSearchCount",
                table: "UserQuotaAdjustments");
        }
    }
}
