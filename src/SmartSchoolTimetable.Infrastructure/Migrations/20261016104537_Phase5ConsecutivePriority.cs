using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5ConsecutivePriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MaxConsecutiveHard",
                table: "Teachers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ConsecutiveHard",
                table: "SchedulingProfile",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Data (owner decision): «أقصى حصص متتالية للمعلم» is a priority «مهم جداً» by default; the school or a teacher can make it hard.
            migrationBuilder.Sql("""
                INSERT INTO "SchedulingProfileRules" ("ProfileId", "Key", "Enabled", "Weight")
                SELECT p."Id", 'maxConsecutiveLessons', 1, 40 FROM "SchedulingProfile" p
                WHERE NOT EXISTS (SELECT 1 FROM "SchedulingProfileRules" r WHERE r."ProfileId" = p."Id" AND r."Key" = 'maxConsecutiveLessons');
                """);
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfile" SET "ProfileVersion" = "ProfileVersion" + 1, "Version" = "Version" + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SchedulingProfileRules" WHERE "Key" = 'maxConsecutiveLessons';
                """);
            migrationBuilder.DropColumn(
                name: "MaxConsecutiveHard",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ConsecutiveHard",
                table: "SchedulingProfile");
        }
    }
}
