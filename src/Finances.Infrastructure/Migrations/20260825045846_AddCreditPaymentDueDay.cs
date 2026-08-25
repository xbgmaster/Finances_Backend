using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditPaymentDueDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentDueDay",
                table: "Credits",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Seed existing credits with a sensible due day (the day of their start date)
            // instead of leaving everyone on the 1st.
            migrationBuilder.Sql(
                "UPDATE \"Credits\" SET \"PaymentDueDay\" = EXTRACT(DAY FROM \"StartDate\")::int;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDueDay",
                table: "Credits");
        }
    }
}
