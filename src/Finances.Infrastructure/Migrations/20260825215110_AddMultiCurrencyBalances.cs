using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCurrencyBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Incomes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Expenses",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "CreditPayments",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "CreditPayments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidCurrency",
                table: "CreditPayments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "CreditPayments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "CreditPayments");

            migrationBuilder.DropColumn(
                name: "PaidCurrency",
                table: "CreditPayments");
        }
    }
}
