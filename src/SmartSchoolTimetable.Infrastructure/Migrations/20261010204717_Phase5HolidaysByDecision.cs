using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5HolidaysByDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ByDecision",
                table: "CalendarDays",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ByDecision",
                table: "CalendarDays");
        }
    }
}
