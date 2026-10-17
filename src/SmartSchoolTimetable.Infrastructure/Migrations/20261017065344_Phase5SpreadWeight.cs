using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <summary>
    /// Owner decision (2026-10-17, DECISIONS_LOG #105): «مهم جداً» weighs 80 for «توزيع حصص المادة على أيام الأسبوع» (40 for the other rules). A saved
    /// 40 (that level, or the old default lifted by <c>Phase5SpreadAllSubjects</c>) becomes 80; any other weight is kept. No schema change.
    /// </summary>
    public partial class Phase5SpreadWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfileRules" SET "Weight" = 80 WHERE "Key" = 'spreadSubjectsAcrossDays' AND "Weight" = 40;
                """);
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfile" SET "ProfileVersion" = "ProfileVersion" + 1, "Version" = "Version" + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfileRules" SET "Weight" = 40 WHERE "Key" = 'spreadSubjectsAcrossDays' AND "Weight" = 80;
                """);
        }
    }
}
