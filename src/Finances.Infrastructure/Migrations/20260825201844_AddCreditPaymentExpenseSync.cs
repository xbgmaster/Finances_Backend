using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditPaymentExpenseSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditPaymentId",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreditPaymentId",
                table: "Expenses",
                column: "CreditPaymentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_CreditPayments_CreditPaymentId",
                table: "Expenses",
                column: "CreditPaymentId",
                principalTable: "CreditPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_CreditPayments_CreditPaymentId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CreditPaymentId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CreditPaymentId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Categories");
        }
    }
}
