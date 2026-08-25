using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryCurrencyBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Budgets",
                table: "Categories",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Budgets",
                table: "Categories");
        }
    }
}
