using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncomeScheduleId",
                table: "Incomes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IncomeSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: true),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    AutoPost = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    LastPostedPeriod = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeSchedules_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncomeSchedules_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_IncomeScheduleId",
                table: "Incomes",
                column: "IncomeScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSchedules_PaymentMethodId",
                table: "IncomeSchedules",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSchedules_UserId",
                table: "IncomeSchedules",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incomes_IncomeSchedules_IncomeScheduleId",
                table: "Incomes",
                column: "IncomeScheduleId",
                principalTable: "IncomeSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incomes_IncomeSchedules_IncomeScheduleId",
                table: "Incomes");

            migrationBuilder.DropTable(
                name: "IncomeSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_IncomeScheduleId",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "IncomeScheduleId",
                table: "Incomes");
        }
    }
}
