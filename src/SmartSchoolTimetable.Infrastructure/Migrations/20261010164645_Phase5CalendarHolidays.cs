using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5CalendarHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApproximate",
                table: "CalendarDays",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "CalendarDays",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CalendarDays",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "CalendarDays",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproximate",
                table: "CalendarDays");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "CalendarDays");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CalendarDays");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "CalendarDays");
        }
    }
}
