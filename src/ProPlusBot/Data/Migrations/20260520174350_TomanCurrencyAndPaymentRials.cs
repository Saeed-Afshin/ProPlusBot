using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProPlusBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class TomanCurrencyAndPaymentRials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MonthlyPriceIrr",
                table: "PlanPricings",
                newName: "MonthlyPriceToman");

            migrationBuilder.RenameColumn(
                name: "AmountIrr",
                table: "PaymentRecords",
                newName: "AmountToman");

            migrationBuilder.RenameColumn(
                name: "PriceIrr",
                table: "ExtraQuotaPackSettings",
                newName: "PriceToman");

            migrationBuilder.AddColumn<long>(
                name: "AmountRials",
                table: "PaymentRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // AmountToman column still holds legacy Rial amounts until converted below.
            migrationBuilder.Sql("""
                UPDATE "PaymentRecords"
                SET "AmountRials" = "AmountToman",
                    "AmountToman" = "AmountToman" / 10
                WHERE "AmountToman" > 0;

                UPDATE "PlanPricings"
                SET "MonthlyPriceToman" = "MonthlyPriceToman" / 10
                WHERE "MonthlyPriceToman" > 100000;

                UPDATE "ExtraQuotaPackSettings"
                SET "PriceToman" = "PriceToman" / 10
                WHERE "PriceToman" > 100000;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountRials",
                table: "PaymentRecords");

            migrationBuilder.RenameColumn(
                name: "MonthlyPriceToman",
                table: "PlanPricings",
                newName: "MonthlyPriceIrr");

            migrationBuilder.RenameColumn(
                name: "AmountToman",
                table: "PaymentRecords",
                newName: "AmountIrr");

            migrationBuilder.RenameColumn(
                name: "PriceToman",
                table: "ExtraQuotaPackSettings",
                newName: "PriceIrr");
        }
    }
}
