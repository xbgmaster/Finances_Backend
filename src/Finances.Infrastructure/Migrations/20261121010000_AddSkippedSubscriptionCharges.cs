using Finances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    [DbContext(typeof(FinanceDbContext))]
    [Migration("20261121010000_AddSkippedSubscriptionCharges")]
    public class AddSkippedSubscriptionCharges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkippedCharges",
                table: "ExpenseSchedules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SkippedCharges", table: "ExpenseSchedules");
        }
    }
}
