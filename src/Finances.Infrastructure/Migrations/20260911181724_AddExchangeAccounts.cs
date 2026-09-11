using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromPaymentMethodId",
                table: "CurrencyExchanges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToPaymentMethodId",
                table: "CurrencyExchanges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchanges_FromPaymentMethodId",
                table: "CurrencyExchanges",
                column: "FromPaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchanges_ToPaymentMethodId",
                table: "CurrencyExchanges",
                column: "ToPaymentMethodId");

            migrationBuilder.AddForeignKey(
                name: "FK_CurrencyExchanges_PaymentMethods_FromPaymentMethodId",
                table: "CurrencyExchanges",
                column: "FromPaymentMethodId",
                principalTable: "PaymentMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CurrencyExchanges_PaymentMethods_ToPaymentMethodId",
                table: "CurrencyExchanges",
                column: "ToPaymentMethodId",
                principalTable: "PaymentMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurrencyExchanges_PaymentMethods_FromPaymentMethodId",
                table: "CurrencyExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_CurrencyExchanges_PaymentMethods_ToPaymentMethodId",
                table: "CurrencyExchanges");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyExchanges_FromPaymentMethodId",
                table: "CurrencyExchanges");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyExchanges_ToPaymentMethodId",
                table: "CurrencyExchanges");

            migrationBuilder.DropColumn(
                name: "FromPaymentMethodId",
                table: "CurrencyExchanges");

            migrationBuilder.DropColumn(
                name: "ToPaymentMethodId",
                table: "CurrencyExchanges");
        }
    }
}
