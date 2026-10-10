using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5RepairedVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions");

            migrationBuilder.AddColumn<bool>(
                name: "IsRepair",
                table: "GenerationRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions",
                sql: "\"Source\" BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions");

            migrationBuilder.DropColumn(
                name: "IsRepair",
                table: "GenerationRuns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions",
                sql: "\"Source\" BETWEEN 1 AND 3");
        }
    }
}
