using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5SubjectDailyCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyCap",
                table: "Subjects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subjects_DailyCap",
                table: "Subjects",
                sql: "\"DailyCap\" IS NULL OR \"DailyCap\" BETWEEN 2 AND 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Subjects_DailyCap",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "DailyCap",
                table: "Subjects");
        }
    }
}
