using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDisabledFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisabledFeatures",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledFeatures",
                table: "AspNetUsers");
        }
    }
}
