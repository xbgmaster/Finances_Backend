using System;
using Finances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    [DbContext(typeof(FinanceDbContext))]
    [Migration("20261120170000_AddPaymentLag")]
    public class AddPaymentLag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentLagBusinessDays",
                table: "IncomeSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayCutDate",
                table: "Incomes",
                type: "timestamp without time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PaymentLagBusinessDays", table: "IncomeSchedules");
            migrationBuilder.DropColumn(name: "PayCutDate", table: "Incomes");
        }
    }
}
