using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayOccurrenceDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PayOccurrenceOverrides",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "PayOccurrenceOverrides");
        }
    }
}
