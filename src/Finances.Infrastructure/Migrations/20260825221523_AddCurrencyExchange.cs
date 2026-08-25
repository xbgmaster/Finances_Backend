using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyExchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "CreditPayments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "CreditPayments");

            migrationBuilder.DropColumn(
                name: "PaidCurrency",
                table: "CreditPayments");

            migrationBuilder.CreateTable(
                name: "CurrencyExchanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FromCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FromAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ToCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ToAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyExchanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurrencyExchanges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchanges_UserId",
                table: "CurrencyExchanges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrencyExchanges");

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
    }
}
