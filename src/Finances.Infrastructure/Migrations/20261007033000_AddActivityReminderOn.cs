using System;
using Finances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finances.Infrastructure.Migrations
{
    [DbContext(typeof(FinanceDbContext))]
    [Migration("20261007033000_AddActivityReminderOn")]
    public class AddActivityReminderOn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivityReminderOn",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityReminderOn",
                table: "AspNetUsers");
        }
    }
}
