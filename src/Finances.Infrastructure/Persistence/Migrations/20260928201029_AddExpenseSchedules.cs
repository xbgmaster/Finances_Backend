using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Finances.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpenseScheduleId",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: false),
                    PayFrequency = table.Column<int>(type: "integer", nullable: false),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    SecondDayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    AutoPost = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    LastPostedPeriod = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseSchedules_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpenseSchedules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseSchedules_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseScheduleId",
                table: "Expenses",
                column: "ExpenseScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSchedules_CategoryId",
                table: "ExpenseSchedules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSchedules_PaymentMethodId",
                table: "ExpenseSchedules",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSchedules_UserId",
                table: "ExpenseSchedules",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ExpenseSchedules_ExpenseScheduleId",
                table: "Expenses",
                column: "ExpenseScheduleId",
                principalTable: "ExpenseSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ExpenseSchedules_ExpenseScheduleId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "ExpenseSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ExpenseScheduleId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ExpenseScheduleId",
                table: "Expenses");
        }
    }
}
