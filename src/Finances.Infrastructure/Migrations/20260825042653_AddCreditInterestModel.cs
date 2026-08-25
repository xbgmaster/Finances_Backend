using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditInterestModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InterestModel",
                table: "Credits",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CompoundFrench");

            migrationBuilder.AddColumn<string>(
                name: "PrepaymentEffect",
                table: "Credits",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ReduceTerm");

            migrationBuilder.AddColumn<decimal>(
                name: "PrepaymentPenaltyRate",
                table: "Credits",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PrepaymentRebateMethod",
                table: "Credits",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterestModel",
                table: "Credits");

            migrationBuilder.DropColumn(
                name: "PrepaymentEffect",
                table: "Credits");

            migrationBuilder.DropColumn(
                name: "PrepaymentPenaltyRate",
                table: "Credits");

            migrationBuilder.DropColumn(
                name: "PrepaymentRebateMethod",
                table: "Credits");
        }
    }
}
